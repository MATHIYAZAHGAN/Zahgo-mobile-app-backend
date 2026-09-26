using ZahSellerAI.Application.DTOs;

namespace ZahSellerAI.Application.Interfaces;

/// <summary>
/// Interface for AI-powered product image analysis
/// </summary>
public interface IAIProductAnalyzer
{
    /// <summary>
    /// Analyzes product image to extract visual information
    /// </summary>
    Task<ProductImageAnalysis> AnalyzeProductImageAsync(Stream imageStream, string fileName);

    /// <summary>
    /// Checks image quality (blur, lighting, visibility)
    /// </summary>
    Task<ImageQualityCheck> CheckImageQualityAsync(Stream imageStream);

    /// <summary>
    /// Extracts text from product image (OCR for packaging, labels)
    /// </summary>
    Task<List<ExtractedText>> ExtractTextFromImageAsync(Stream imageStream);
}

public class ProductImageAnalysis
{
    public string? DetectedProduct { get; set; }
    public string? DetectedBrand { get; set; }
    public string? DetectedCategory { get; set; }
    public string? DetectedColor { get; set; }
    public Dictionary<string, string> VisualAttributes { get; set; } = new();
    public double Confidence { get; set; }
    public bool HasVisibleBarcode { get; set; }
    public string? BarCodeValue { get; set; }
}

public class ImageQualityCheck
{
    public bool IsAcceptable { get; set; }
    public bool IsBlurry { get; set; }
    public bool IsLowLight { get; set; }
    public bool IsProductVisible { get; set; }
    public bool HasBackground { get; set; }
    public double QualityScore { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class ExtractedText
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Region { get; set; } = string.Empty; // e.g., "top-left", "center"
}
