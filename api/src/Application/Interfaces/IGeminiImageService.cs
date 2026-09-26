namespace ZahSellerAI.Application.Interfaces;

public class GeminiImageResult
{
    public string ImageUrl { get; set; } = string.Empty;
    public string OriginalImageUrl { get; set; } = string.Empty;
    public string Provider { get; set; } = "gemini";
    public string Model { get; set; } = "gemini-3.1-flash-image";
    public string Status { get; set; } = "completed";
    public long ProcessingTimeMs { get; set; }
}

public interface IGeminiImageService
{
    Task<GeminiImageResult> GenerateProductImageAsync(
        Stream imageStream,
        string mimeType,
        string prompt,
        string? style = "amazon",
        CancellationToken cancellationToken = default
    );
}
