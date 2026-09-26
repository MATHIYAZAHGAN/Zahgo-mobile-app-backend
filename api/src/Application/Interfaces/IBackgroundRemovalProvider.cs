namespace ZahSellerAI.Application.Interfaces;

public class BackgroundRemovalResult
{
    public string ProcessedImageUrl { get; set; } = string.Empty;
    public string TransparentImageUrl { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double Confidence { get; set; }
    public string Provider { get; set; } = string.Empty;
    public long ProcessingTimeMs { get; set; }
}

public interface IBackgroundRemovalProvider
{
    Task<BackgroundRemovalResult> RemoveBackgroundAsync(
        Stream imageStream,
        string fileName,
        CancellationToken cancellationToken = default
    );
}
