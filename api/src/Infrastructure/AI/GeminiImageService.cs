using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZahSellerAI.Application.Interfaces;

namespace ZahSellerAI.Infrastructure.AI;

public class GeminiImageService : IGeminiImageService
{
    private const string DefaultModel = "gemini-3.1-flash-image";

    private readonly IConfiguration _configuration;
    private readonly IImageStorageService _storageService;
    private readonly ILogger<GeminiImageService> _logger;
    private readonly HttpClient _httpClient;

    public GeminiImageService(
        IConfiguration configuration,
        IImageStorageService storageService,
        ILogger<GeminiImageService> logger,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _storageService = storageService;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<GeminiImageResult> GenerateProductImageAsync(
        Stream imageStream,
        string mimeType,
        string prompt,
        string? style = "amazon",
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var apiKey = _configuration["GEMINI_API_KEY"] ?? _configuration["AI:Gemini:ApiKey"];
        var modelName = _configuration["GEMINI_IMAGE_MODEL"] ?? _configuration["AI:Gemini:ImageModel"] ?? DefaultModel;

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("GEMINI_API_KEY is not configured.");
        }

        _logger.LogInformation("Calling Google Gemini image editing API using model [{Model}] (style = {Style})",
            modelName, style);

        // 1. Persist the original uploaded image separately (never overwrite it).
        var originalFileName = $"original_{Guid.NewGuid():N}.bin";
        var extension = GetImageExtension(mimeType);
        if (!string.IsNullOrEmpty(extension))
        {
            originalFileName = $"original_{Guid.NewGuid():N}{extension}";
        }

        var originalUrl = await _storageService.UploadAsync(
            imageStream,
            originalFileName,
            "products/original",
            mimeType,
            cancellationToken);

        // 2. Build the ecommerce product-photography prompt (product preservation is critical).
        var authoritativePrompt = BuildPrompt(prompt, style);

        // 3. Read image bytes for the Gemini request.
        using var memoryStream = new MemoryStream();
        imageStream.Position = 0;
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();
        var base64Data = Convert.ToBase64String(imageBytes);

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = authoritativePrompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Data
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                responseModalities = new[] { "IMAGE" }
            }
        };

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Remove("x-goog-api-key");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", apiKey);

        using var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini image API returned status {Status}: {ErrorText}", response.StatusCode, responseText);
            throw BuildGeminiException(response.StatusCode, responseText);
        }

        var generatedBytes = ExtractGeneratedImage(responseText);
        if (generatedBytes == null || generatedBytes.Length == 0)
        {
            _logger.LogWarning("Gemini image API returned a response without inline image data.");
            throw new InvalidOperationException("Gemini returned an empty or malformed image response.");
        }

        // 4. Persist the AI-generated product image separately.
        var resultMime = DetectGeneratedMimeType(responseText) ?? "image/png";
        var resultExtension = GetImageExtension(resultMime) ?? ".png";
        var generatedFileName = $"ai_{Guid.NewGuid():N}{resultExtension}";
        await using var generatedStream = new MemoryStream(generatedBytes);

        var generatedUrl = await _storageService.UploadAsync(
            generatedStream,
            generatedFileName,
            "products/ai",
            resultMime,
            cancellationToken);

        stopwatch.Stop();
        return new GeminiImageResult
        {
            ImageUrl = generatedUrl,
            OriginalImageUrl = originalUrl,
            Provider = "gemini",
            Model = modelName,
            Status = "completed",
            ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
        };
    }

    private string BuildPrompt(string? customPrompt, string? style)
    {
        if (!string.IsNullOrWhiteSpace(customPrompt))
        {
            return customPrompt;
        }

        var styleInstruction = ResolveStyleInstruction(style);

        return @"Use the uploaded image as the authoritative reference for the physical product.
Create a professional ecommerce product photograph from this exact product.
Remove the entire original environment and background.
Replace it with a clean pure white #FFFFFF studio background.
Preserve the exact identity of the product:
- exact shape
- exact proportions
- exact color
- exact materials
- exact buttons
- exact ports
- exact cables
- exact handles
- exact accessories that are physically present
- exact branding
- exact logo
- exact visible text
- exact product design
Do NOT redesign the product. Do NOT create a similar product.
Do NOT invent missing components. Do NOT add accessories that are not present.
Do NOT remove components that are present. Do NOT change the product's color.
Do NOT change the product's physical structure. Do NOT alter logos or branding.
Do NOT hallucinate specifications.
Only improve the photographic presentation. " + styleInstruction + @"
Place the product naturally in the center of the frame.
Maintain realistic proportions. Use professional ecommerce studio lighting.
Use subtle realistic grounding/shadow only if it improves realism.
The result should look like a professionally photographed product listing from a major ecommerce marketplace.
The product must remain the same physical product shown in the input image.
Output only the final product image.";
    }

    private string ResolveStyleInstruction(string? style)
    {
        var normalized = (style ?? "amazon").Trim().ToLowerInvariant();
        return normalized switch
        {
            "amazon" or "marketplace" =>
                "Use a clean marketplace (Amazon-style) presentation with a pure white background and a soft, subtle drop shadow that grounds the product naturally.",
            _ => "Use a clean marketplace-style presentation on a pure white background.",
        };
    }

    private byte[]? ExtractGeneratedImage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("inlineData", out var inlineData))
                {
                    var data = inlineData.TryGetProperty("data", out var dataProp)
                        ? dataProp.GetString()
                        : null;
                    if (!string.IsNullOrEmpty(data))
                    {
                        return Convert.FromBase64String(data);
                    }
                }
            }
        }

        return null;
    }

    private string? DetectGeneratedMimeType(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var candidate in doc.RootElement.GetProperty("candidates").EnumerateArray())
            {
                foreach (var part in candidate.GetProperty("content").GetProperty("parts").EnumerateArray())
                {
                    if (part.TryGetProperty("inlineData", out var inlineData) &&
                        inlineData.TryGetProperty("mimeType", out var mimeProp))
                    {
                        return mimeProp.GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not detect generated image mime type, defaulting to png");
        }

        return null;
    }

    private string? GetImageExtension(string mimeType)
    {
        return mimeType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => null
        };
    }

    private Exception BuildGeminiException(System.Net.HttpStatusCode statusCode, string responseText)
    {
        var message = "Gemini image generation failed.";
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var msg))
            {
                message = msg.GetString() ?? message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not parse Gemini error payload");
        }

        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                new InvalidOperationException("Gemini authentication failed."),
            System.Net.HttpStatusCode.TooManyRequests =>
                new InvalidOperationException("Gemini rate limit exceeded. Please try again later."),
            _ => new InvalidOperationException(message)
        };
    }
}
