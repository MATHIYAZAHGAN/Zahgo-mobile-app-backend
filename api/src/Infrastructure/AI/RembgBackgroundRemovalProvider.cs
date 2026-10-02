using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZahSellerAI.Application.Interfaces;

namespace ZahSellerAI.Infrastructure.AI;

public class RembgBackgroundRemovalProvider : IBackgroundRemovalProvider
{
    private readonly IConfiguration _configuration;
    private readonly IFileStorageService _storageService;
    private readonly ILogger<RembgBackgroundRemovalProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public RembgBackgroundRemovalProvider(
        IConfiguration configuration,
        IFileStorageService storageService,
        ILogger<RembgBackgroundRemovalProvider> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _storageService = storageService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<BackgroundRemovalResult> RemoveBackgroundAsync(
        Stream imageStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Executing Remove.bg background removal pipeline for file: {FileName}", fileName);

        // Upload original image to storage
        imageStream.Position = 0;
        var originalUrl = await _storageService.UploadAsync(
            imageStream,
            fileName,
            "products/original",
            "image/png",
            cancellationToken
        );

        // Get Remove.bg API key from configuration
        var apiKey = _configuration["AI:BackgroundRemoval:ApiKey"]
            ?? _configuration["REMOVEBG_API_KEY"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Remove.bg API key is not configured");
            throw new InvalidOperationException("Photo background removal is not configured");
        }

        try
        {
            // Call Remove.bg API
            imageStream.Position = 0;
            var result = await CallRemoveBgApiAsync(imageStream, fileName, apiKey, cancellationToken);

            // Upload processed image to storage
            using var processedStream = new MemoryStream(result.ImageData);
            var processedFileName = $"nobg_{Path.GetFileNameWithoutExtension(fileName)}.png";
            var processedUrl = await _storageService.UploadAsync(
                processedStream,
                processedFileName,
                "products/background-removed",
                "image/png",
                cancellationToken
            );

            stopwatch.Stop();
            _logger.LogInformation("Background removal completed successfully in {ElapsedMs}ms. Credits remaining: {Credits}", 
                stopwatch.ElapsedMilliseconds, result.CreditsRemaining);

            return new BackgroundRemovalResult
            {
                ProcessedImageUrl = processedUrl,
                TransparentImageUrl = processedUrl,
                Width = result.Width,
                Height = result.Height,
                Confidence = 0.99, // Remove.bg has very high accuracy
                Provider = "Remove.bg",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove background using Remove.bg API");
            throw;
        }
    }

    private async Task<RemoveBgResult> CallRemoveBgApiAsync(
        Stream imageStream, 
        string fileName, 
        string apiKey, 
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        
        using var content = new MultipartFormDataContent();
        
        // Add image file
        var imageContent = new StreamContent(imageStream);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image_file", fileName);

        // Add parameters for best quality product photos
        content.Add(new StringContent("auto"), "size"); // auto, hd, full - auto detects best
        content.Add(new StringContent("product"), "type"); // product type for ecommerce
        content.Add(new StringContent("FFFFFF"), "bg_color"); // Studio White (#FFFFFF) background
        content.Add(new StringContent("none"), "format"); // png with transparency
        content.Add(new StringContent("true"), "crop"); // auto crop to product
        content.Add(new StringContent("original"), "crop_margin"); // keep original margins

        // Set API key in header
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        _logger.LogInformation("Calling Remove.bg API...");
        var response = await httpClient.PostAsync(
            "https://api.remove.bg/v1.0/removebg",
            content,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Remove.bg API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Remove.bg API failed with status {response.StatusCode}: {errorContent}");
        }

        // Get credits remaining from response headers
        var creditsRemaining = 0;
        if (response.Headers.TryGetValues("X-Credits-Charged", out var chargedValues))
        {
            _logger.LogInformation("Credits charged: {Credits}", string.Join(", ", chargedValues));
        }
        if (response.Headers.TryGetValues("X-Ratelimit-Remaining", out var remainingValues))
        {
            int.TryParse(remainingValues.FirstOrDefault(), out creditsRemaining);
        }

        // Get image dimensions from response headers
        var width = 2000;
        var height = 2000;
        if (response.Headers.TryGetValues("X-Width", out var widthValues))
        {
            int.TryParse(widthValues.FirstOrDefault(), out width);
        }
        if (response.Headers.TryGetValues("X-Height", out var heightValues))
        {
            int.TryParse(heightValues.FirstOrDefault(), out height);
        }

        var imageData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return new RemoveBgResult
        {
            ImageData = imageData,
            Width = width,
            Height = height,
            CreditsRemaining = creditsRemaining
        };
    }

    private class RemoveBgResult
    {
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
        public int CreditsRemaining { get; set; }
    }
}
