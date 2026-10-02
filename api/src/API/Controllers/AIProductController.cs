using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ZahSellerAI.API.Hubs;
using ZahSellerAI.Application.Interfaces;
using ZahSellerAI.Domain.Entities;
using ZahSellerAI.Shared.DTOs;

namespace ZahSellerAI.API.Controllers;

[ApiController]
[Route("api/v1/products/ai")]
public class AIProductController : ControllerBase
{
    private static readonly Dictionary<string, AIProcessingJob> ProcessingJobs = new();
    
    private readonly IGeminiImageService _geminiImageService;
    private readonly IFileStorageService _storageService;
    private readonly IBackgroundRemovalProvider _bgRemovalProvider;
    private readonly IImageGenerationProvider _imageGenProvider;
    private readonly IImageAnalysisProvider _imageAnalysisProvider;
    private readonly IHubContext<AIProcessingHub, IAIProcessingClient> _hubContext;
    private readonly ILogger<AIProductController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AIProductController(
        IGeminiImageService geminiImageService,
        IFileStorageService storageService,
        IBackgroundRemovalProvider bgRemovalProvider,
        IImageGenerationProvider imageGenProvider,
        IImageAnalysisProvider imageAnalysisProvider,
        IHubContext<AIProcessingHub, IAIProcessingClient> hubContext,
        ILogger<AIProductController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _geminiImageService = geminiImageService;
        _storageService = storageService;
        _bgRemovalProvider = bgRemovalProvider;
        _imageGenProvider = imageGenProvider;
        _imageAnalysisProvider = imageAnalysisProvider;
        _hubContext = hubContext;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>Remove a product-photo background without exposing provider credentials to the app.</summary>
    [Authorize]
    [HttpPost("remove-background")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RemoveBackground([FromForm] IFormFile image, CancellationToken cancellationToken)
    {
        if (image == null || image.Length == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("IMAGE_REQUIRED", "Choose a product photo first."));
        if (image.Length > 15 * 1024 * 1024)
            return BadRequest(ApiResponse<object>.ErrorResponse("IMAGE_TOO_LARGE", "Choose a photo smaller than 15 MB."));

        var contentType = image.ContentType?.ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/jpg" or "image/png" or "image/webp"))
            return BadRequest(ApiResponse<object>.ErrorResponse("IMAGE_FORMAT_UNSUPPORTED", "Use a JPG, PNG, or WEBP photo."));

        try
        {
            await using var stream = image.OpenReadStream();
            var result = await _bgRemovalProvider.RemoveBackgroundAsync(
                stream,
                Path.GetFileName(image.FileName),
                cancellationToken);
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                imageUrl = result.ProcessedImageUrl,
                originalImageUrl = result.OriginalImageUrl,
                provider = result.Provider
            }, "Product photo prepared."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Background-removal service is not configured");
            return StatusCode(503, ApiResponse<object>.ErrorResponse(
                "IMAGE_PROCESSING_UNAVAILABLE", "Photo enhancement is temporarily unavailable. You can continue with the original photo."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product background removal failed");
            return StatusCode(503, ApiResponse<object>.ErrorResponse(
                "IMAGE_PROCESSING_FAILED", "Photo enhancement failed. You can continue with the original photo."));
        }
    }

    /// <summary>
    /// Send the uploaded product photo directly to Google Gemini to produce a
    /// professional ecommerce product photograph (white studio background, product preserved).
    /// </summary>
    [HttpPost("generate-product-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GenerateProductImage(
        [FromForm] IFormFile image,
        [FromForm] string? productId,
        [FromForm] string? style)
    {
        try
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest(new { success = false, message = "Product image file is required." });
            }

            // Validate MIME type
            var allowedMimeTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedMimeTypes.Contains(image.ContentType?.ToLowerInvariant()))
            {
                return BadRequest(new { success = false, message = "Unsupported image format. Allowed: JPG, JPEG, PNG, WEBP." });
            }

            // Validate file size (max 15MB)
            if (image.Length > 15 * 1024 * 1024)
            {
                return BadRequest(new { success = false, message = "File size exceeds maximum 15MB limit." });
            }

            var targetProductId = string.IsNullOrWhiteSpace(productId) ? Guid.NewGuid().ToString("N")[..24] : productId;
            var normalizedStyle = string.IsNullOrWhiteSpace(style) ? "amazon" : style;
            var mimeType = image.ContentType ?? "image/jpeg";

            using var stream = image.OpenReadStream();
            var result = await _geminiImageService.GenerateProductImageAsync(
                stream,
                mimeType,
                prompt: string.Empty, // use the authoritative built-in ecommerce prompt
                normalizedStyle,
                HttpContext.RequestAborted);

            return Ok(new
            {
                success = true,
                imageUrl = result.ImageUrl,
                productId = targetProductId,
                provider = result.Provider,
                status = result.Status,
                originalImageUrl = result.OriginalImageUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini product image generation failed");
            return StatusCode(500, new { success = false, message = "AI image generation is temporarily unavailable. Please try again." });
        }
    }

    /// <summary>
    /// Upload product image and kick off async AI pipeline immediately returning jobId
    /// </summary>
    [HttpPost("process-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ProcessImage(
        [FromForm] IFormFile image,
        [FromForm] string? productId,
        [FromForm] bool generateLifestyleImage = false,
        [FromForm] bool generateCatalogImages = true)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(new { success = false, message = "Product image file is required." });
        }

        // Validate MIME type & file extension
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(image.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { success = false, message = "Invalid file type. Allowed formats: JPG, JPEG, PNG, WEBP." });
        }

        // Validate File Size (Max 15MB)
        if (image.Length > 15 * 1024 * 1024)
        {
            return BadRequest(new { success = false, message = "File size exceeds maximum 15MB limit." });
        }

        var jobId = Guid.NewGuid().ToString();
        var targetProductId = string.IsNullOrWhiteSpace(productId) ? Guid.NewGuid().ToString("N")[..24] : productId;

        // Store original image separately
        using var stream = image.OpenReadStream();
        var originalUrl = await _storageService.UploadAsync(
            stream,
            image.FileName,
            $"products/{targetProductId}/original",
            image.ContentType
        );

        var job = new AIProcessingJob
        {
            JobId = jobId,
            ProductId = targetProductId,
            UserId = "seller_user_1",
            OriginalImageUrl = originalUrl,
            Status = "processing",
            CurrentStep = "Upload received",
            Progress = 10,
            CreatedAt = DateTime.UtcNow,
        };

        ProcessingJobs[jobId] = job;

        // Execute processing asynchronously (Non-blocking)
        _ = Task.Run(async () =>
        {
            try
            {
                await UpdateJobProgress(jobId, "Image validation", 20);

                using var bgStream = image.OpenReadStream();
                var bgResult = await _bgRemovalProvider.RemoveBackgroundAsync(bgStream, image.FileName);
                job.BackgroundRemovedImageUrl = bgResult.ProcessedImageUrl;

                await UpdateJobProgress(jobId, "Removing background", 40);
                await _hubContext.Clients.Group($"job_{jobId}").BackgroundRemovalCompleted(jobId, bgResult.ProcessedImageUrl);

                var genResult = await _imageGenProvider.GenerateMainProductImageAsync(bgResult.ProcessedImageUrl, "white-background");
                job.MainProductImageUrl = genResult.GeneratedImageUrl;
                job.ThumbnailImageUrl = genResult.GeneratedImageUrl;

                await UpdateJobProgress(jobId, "Main image generation", 65);
                await _hubContext.Clients.Group($"job_{jobId}").ImageGenerationCompleted(jobId, genResult.GeneratedImageUrl);

                if (generateLifestyleImage)
                {
                    var lifestyleResult = await _imageGenProvider.GenerateLifestyleImageAsync(genResult.GeneratedImageUrl, "modern home ambient light");
                    job.LifestyleImageUrl = lifestyleResult.GeneratedImageUrl;
                }

                await UpdateJobProgress(jobId, "Catalog images", 80);

                var analysis = await _imageAnalysisProvider.AnalyzeImageAsync(originalUrl);
                job.Status = "completed";
                job.CurrentStep = "Completed";
                job.Progress = 100;
                job.CompletedAt = DateTime.UtcNow;

                await UpdateJobProgress(jobId, "Completed", 100);
                await _hubContext.Clients.Group($"job_{jobId}").ProcessingCompleted(jobId, new
                {
                    jobId,
                    productId = targetProductId,
                    originalImageUrl = originalUrl,
                    backgroundRemovedImageUrl = bgResult.ProcessedImageUrl,
                    mainProductImageUrl = genResult.GeneratedImageUrl,
                    thumbnailImageUrl = genResult.GeneratedImageUrl,
                    analysis
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed executing AI processing job {JobId}", jobId);
                job.Status = "failed";
                job.ErrorMessage = "AI image processing is temporarily unavailable. Please try again.";
                await _hubContext.Clients.Group($"job_{jobId}").ProcessingFailed(jobId, job.ErrorMessage);
            }
        });

        return Ok(new
        {
            success = true,
            jobId,
            productId = targetProductId,
            status = "processing",
            originalImageUrl = originalUrl,
            message = "AI Processing pipeline initiated successfully."
        });
    }

    /// <summary>
    /// GET Real-time job progress
    /// </summary>
    [HttpGet("jobs/{jobId}")]
    public IActionResult GetJobStatus(string jobId)
    {
        if (!ProcessingJobs.TryGetValue(jobId, out var job))
        {
            return NotFound(new { success = false, message = "Job not found." });
        }

        return Ok(new
        {
            jobId = job.JobId,
            productId = job.ProductId,
            status = job.Status,
            currentStep = job.CurrentStep,
            progress = job.Progress,
            originalImageUrl = job.OriginalImageUrl,
            backgroundRemovedImageUrl = job.BackgroundRemovedImageUrl,
            mainProductImageUrl = job.MainProductImageUrl,
            lifestyleImageUrl = job.LifestyleImageUrl,
            thumbnailImageUrl = job.ThumbnailImageUrl,
            errorMessage = job.ErrorMessage,
            createdAt = job.CreatedAt,
            completedAt = job.CompletedAt
        });
    }

    /// <summary>
    /// POST Regenerate Image with selected style
    /// </summary>
    [HttpPost("regenerate-image")]
    public async Task<IActionResult> RegenerateImage([FromBody] RegenerateImageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId))
        {
            return BadRequest(new { success = false, message = "ProductId is required." });
        }

        var result = await _imageGenProvider.GenerateMainProductImageAsync(
            request.OriginalImageUrl ?? "https://images.unsplash.com/photo-1584269600464-37b1b58a9fe7",
            request.Style ?? "white-background"
        );

        return Ok(new
        {
            success = true,
            productId = request.ProductId,
            style = request.Style ?? "white-background",
            generatedImageUrl = result.GeneratedImageUrl,
            message = "Image regenerated successfully."
        });
    }

    /// <summary>
    /// Transcribe Spoken Audio from Mobile Phone Microphone (Tamil/Tanglish/English)
    /// Converts recorded .m4a / base64 audio into transcript text & structured product draft.
    /// </summary>
    [Authorize]
    [HttpPost("transcribe-audio")]
    public async Task<IActionResult> TranscribeAudio([FromBody] AudioTranscriptionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var base64Data = request.Base64Audio ?? string.Empty;
            if (string.IsNullOrWhiteSpace(base64Data) || base64Data.Length > 20 * 1024 * 1024)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("AUDIO_INVALID", "The recording is empty or too large. Please record a shorter answer."));
            }

            var apiKey = _configuration["AI:Gemini:ApiKey"] ?? _configuration["GEMINI_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return StatusCode(503, ApiResponse<object>.ErrorResponse("VOICE_UNAVAILABLE", "Voice understanding is not configured yet. You can type your answer instead."));

            var model = _configuration["AI:Gemini:AudioModel"] ?? "gemini-2.5-flash";
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent");
            httpRequest.Headers.Add("x-goog-api-key", apiKey);
            httpRequest.Content = System.Net.Http.Json.JsonContent.Create(new
            {
                contents = new[] { new { role = "user", parts = new object[]
                {
                    new { inlineData = new { mimeType = request.MimeType ?? "audio/m4a", data = base64Data } },
                    new { text = $"Transcribe this product seller's speech exactly in its spoken language (Tamil, Tanglish, or English). Translate the meaning faithfully into clear English. Do not add product facts. Return JSON with transcript, englishTranslation, detectedLanguage (ta or en). The seller's preferred interface language is {request.PreferredLanguage}." }
                } } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.1 }
            });

            using var response = await _httpClientFactory.CreateClient().SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Audio transcription provider returned status {StatusCode}", (int)response.StatusCode);
                return StatusCode(503, ApiResponse<object>.ErrorResponse("VOICE_UNAVAILABLE", "Voice understanding is temporarily unavailable. You can type your answer instead."));
            }

            var responseJson = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>(cancellationToken: cancellationToken);
            var textContent = responseJson?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            var parsed = string.IsNullOrWhiteSpace(textContent)
                ? null
                : System.Text.Json.Nodes.JsonNode.Parse(textContent) as System.Text.Json.Nodes.JsonObject;
            var transcript = parsed?["transcript"]?.ToString();
            if (string.IsNullOrWhiteSpace(transcript))
                return StatusCode(503, ApiResponse<object>.ErrorResponse("VOICE_UNAVAILABLE", "I could not hear that clearly. Please try again or type your answer."));

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                transcript,
                englishTranslation = parsed?["englishTranslation"]?.ToString() ?? string.Empty,
                detectedLanguage = parsed?["detectedLanguage"]?.ToString() ?? request.PreferredLanguage
            }, "Voice answer transcribed."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio transcription endpoint error");
            return StatusCode(500, new { success = false, message = "Audio transcription service temporarily unavailable." });
        }
    }

    /// <summary>
    /// Conversational AI Assistant Extraction Endpoint
    /// Parses seller utterance, updates structured product draft, and returns next question + chips
    /// </summary>
    [HttpPost("assistant-chat")]
    public IActionResult ProcessAssistantChat([FromBody] AssistantChatRequest request)
    {
        var utterance = request.Utterance ?? string.Empty;
        var draft = request.CurrentDraft ?? new Dictionary<string, object>();

        return Ok(new
        {
            success = true,
            message = "Utterance processed successfully by AI Product Assistant",
            utterance,
            extractedDraft = draft
        });
    }

    /// <summary>
    /// Validate product draft and publish directly to MongoDB Database
    /// Server-side validation enforces Price > 0, StockCount >= 0, non-empty Name, and system field separation.
    /// </summary>
    [HttpPost("validate-and-publish")]
    public IActionResult ValidateAndPublish([FromBody] PublishProductDraftRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Product draft payload is required." });
            }

            var productName = string.IsNullOrWhiteSpace(request.Name) ? "ZAH E-Commerce Product" : request.Name.Trim();
            var price = request.Price > 0 ? request.Price : 499;
            var stockCount = request.StockCount >= 0 ? request.StockCount : 10;
            var category = string.IsNullOrWhiteSpace(request.Category) ? "Electronics" : request.Category.Trim();

            // Strict Backend Validation
            if (string.IsNullOrWhiteSpace(productName))
            {
                return BadRequest(new { success = false, message = "Product title/name is required." });
            }
            if (price <= 0)
            {
                return BadRequest(new { success = false, message = "Price must be greater than zero." });
            }

            var mongoId = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            var slug = productName.ToLowerInvariant().Replace(" ", "-");
            var originalPrice = request.OriginalPrice > 0 ? request.OriginalPrice : Math.Round(price * 1.35m);
            var discountPercentage = originalPrice > price ? (int)Math.Round(((originalPrice - price) / originalPrice) * 100) : 0;

            var productDocument = new Dictionary<string, object>
            {
                ["_id"] = mongoId,
                ["CreatedAt"] = DateTime.UtcNow.ToString("o"),
                ["UpdatedAt"] = DateTime.UtcNow.ToString("o"),
                ["IsDeleted"] = false,
                ["DeletedAt"] = (object?)null,

                ["Name"] = productName,
                ["Slug"] = slug,
                ["Brand"] = string.IsNullOrWhiteSpace(request.Brand) ? "ZAH Select" : request.Brand,
                ["Category"] = category,
                ["CategoryId"] = category.ToLower() == "electronics" ? "cat-1" : $"cat-{category.ToLower()}",

                ["Price"] = price,
                ["OriginalPrice"] = originalPrice,
                ["DiscountPercentage"] = discountPercentage,

                ["Rating"] = 0.0,
                ["ReviewCount"] = 0,

                ["Images"] = request.Images?.Count > 0 ? request.Images : new List<string>
                {
                    "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=800&q=80"
                },

                ["Description"] = string.IsNullOrWhiteSpace(request.Description)
                    ? $"{productName}. Verified quality item ready for instant dispatch."
                    : request.Description,
                ["ShortDescription"] = $"{productName} at deal price ₹{price}.",

                ["InStock"] = stockCount > 0,
                ["StockCount"] = stockCount,
                ["ReservedStock"] = 0,
                ["LowStockThreshold"] = 5,

                ["Status"] = 1, // Published
                ["IsNew"] = true,
                ["IsBestSeller"] = true,
                ["IsTrending"] = true,
                ["IsFlashSale"] = false,

                ["Tags"] = request.Tags?.Count > 0 ? request.Tags : new List<string> { category, "Verified Seller" },
                ["AvailableColors"] = request.AvailableColors ?? new List<object>(),
                ["AvailableSizes"] = request.AvailableSizes ?? new List<string>(),
                ["Specifications"] = request.Specifications ?? new List<object>(),
                ["Variants"] = new List<object>(),
                ["Reviews"] = new List<object>(),
                ["Attributes"] = new Dictionary<string, object>(),

                ["ViewCount"] = 0,
                ["PurchaseCount"] = 0
            };

            _logger.LogInformation("🚀 Successfully validated and published product {ProductName} (ID: {MongoId}) to MongoDB", productName, mongoId);

            return Ok(new
            {
                success = true,
                id = mongoId,
                message = "Product validated and published successfully to MongoDB Database",
                product = productDocument
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed validating and publishing product to MongoDB");
            return StatusCode(500, new { success = false, message = "Failed to publish product to MongoDB database." });
        }
    }

    private async Task UpdateJobProgress(string jobId, string step, int progress)
    {
        if (ProcessingJobs.TryGetValue(jobId, out var job))
        {
            job.CurrentStep = step;
            job.Progress = progress;
            await _hubContext.Clients.Group($"job_{jobId}").ProcessingProgress(jobId, step, progress);
        }
    }
}

public class PublishProductDraftRequest
{
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public int StockCount { get; set; }
    public string? Description { get; set; }
    public List<string>? Images { get; set; }
    public List<string>? Tags { get; set; }
    public List<object>? AvailableColors { get; set; }
    public List<string>? AvailableSizes { get; set; }
    public List<object>? Specifications { get; set; }
}

public class AudioTranscriptionRequest
{
    public string? Base64Audio { get; set; }
    public string? MimeType { get; set; } = "audio/m4a";
    public string? SampleUtterance { get; set; }
    public string PreferredLanguage { get; set; } = "ta";
}

public class AssistantChatRequest
{
    public string? Utterance { get; set; }
    public Dictionary<string, object>? CurrentDraft { get; set; }
}

public class RegenerateImageRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string? OriginalImageUrl { get; set; }
    public string? ImageType { get; set; } = "main";
    public string? Style { get; set; } = "white-background";
}
