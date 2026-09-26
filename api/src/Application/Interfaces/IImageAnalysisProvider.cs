namespace ZahSellerAI.Application.Interfaces;

public class ImageAnalysisResult
{
    public string DetectedTitle { get; set; } = "Not detected";
    public string DetectedBrand { get; set; } = "Not detected";
    public string DetectedCategory { get; set; } = "Not detected";
    public string DetectedSubcategory { get; set; } = "Not detected";
    public string DetectedColor { get; set; } = "Not detected";
    public string DetectedMaterial { get; set; } = "Not detected";
    public List<string> VisibleFeatures { get; set; } = new();
    public Dictionary<string, string> DetectedSpecifications { get; set; } = new();
    public double Confidence { get; set; }
}

public interface IImageAnalysisProvider
{
    Task<ImageAnalysisResult> AnalyzeImageAsync(
        string imageUrl,
        CancellationToken cancellationToken = default
    );
}
