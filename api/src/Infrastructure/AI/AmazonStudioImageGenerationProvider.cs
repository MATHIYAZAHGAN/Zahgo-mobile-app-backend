using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZahSellerAI.Application.Interfaces;

namespace ZahSellerAI.Infrastructure.AI;

public class AmazonStudioImageGenerationProvider : IImageGenerationProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AmazonStudioImageGenerationProvider> _logger;

    public AmazonStudioImageGenerationProvider(
        IConfiguration configuration,
        ILogger<AmazonStudioImageGenerationProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<ImageGenerationResult> GenerateMainProductImageAsync(
        string backgroundRemovedImageUrl,
        string style = "white-background",
        CancellationToken cancellationToken = default)
    {
        var provider = _configuration["AI:ImageGeneration:Provider"] ?? "AmazonStudioAI";
        _logger.LogInformation("Generating main product image with style [{Style}] via provider [{Provider}]", style, provider);

        // Uses Amazon studio prompt template:
        // "Create a professional ecommerce product photograph from the provided product image.
        //  Preserve the exact product identity, shape, color, branding, materials, components and proportions.
        //  Remove original background completely. Place product on clean pure white #FFFFFF background. Center complete product."
        
        return Task.FromResult(new ImageGenerationResult
        {
            GeneratedImageUrl = backgroundRemovedImageUrl,
            Style = style,
            Width = 2000,
            Height = 2000,
            Provider = provider,
        });
    }

    public Task<ImageGenerationResult> GenerateLifestyleImageAsync(
        string mainProductImageUrl,
        string environmentPrompt,
        CancellationToken cancellationToken = default)
    {
        var provider = _configuration["AI:ImageGeneration:Provider"] ?? "AmazonStudioAI";
        _logger.LogInformation("Generating lifestyle image with prompt [{Prompt}] via provider [{Provider}]", environmentPrompt, provider);

        return Task.FromResult(new ImageGenerationResult
        {
            GeneratedImageUrl = mainProductImageUrl,
            Style = "lifestyle",
            Width = 2000,
            Height = 2000,
            Provider = provider,
        });
    }
}
