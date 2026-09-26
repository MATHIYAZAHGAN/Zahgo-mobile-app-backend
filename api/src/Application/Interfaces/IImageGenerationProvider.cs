namespace ZahSellerAI.Application.Interfaces;

public class ImageGenerationResult
{
    public string GeneratedImageUrl { get; set; } = string.Empty;
    public string Style { get; set; } = "white-background";
    public int Width { get; set; } = 2000;
    public int Height { get; set; } = 2000;
    public string Provider { get; set; } = string.Empty;
}

public interface IImageGenerationProvider
{
    Task<ImageGenerationResult> GenerateMainProductImageAsync(
        string backgroundRemovedImageUrl,
        string style = "white-background",
        CancellationToken cancellationToken = default
    );

    Task<ImageGenerationResult> GenerateLifestyleImageAsync(
        string mainProductImageUrl,
        string environmentPrompt,
        CancellationToken cancellationToken = default
    );
}
