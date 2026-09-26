using ZahSellerAI.Application.DTOs;
using ZahSellerAI.Domain.Entities;

namespace ZahSellerAI.Application.Interfaces;

/// <summary>
/// Interface for AI-powered product catalog generation
/// </summary>
public interface IAICatalogGenerator
{
    /// <summary>
    /// Generates complete product catalog from multimodal inputs
    /// </summary>
    Task<GeneratedCatalog> GenerateCatalogAsync(CatalogGenerationRequest request);

    /// <summary>
    /// Identifies missing required information
    /// </summary>
    Task<List<MissingInformation>> IdentifyMissingInformationAsync(
        Product product, 
        string? categoryId);

    /// <summary>
    /// Generates follow-up questions for missing information
    /// </summary>
    Task<List<AIQuestion>> GenerateQuestionsAsync(
        List<MissingInformation> missingInfo,
        Product product);

    /// <summary>
    /// Enhances product description
    /// </summary>
    Task<string> EnhanceDescriptionAsync(
        string currentDescription, 
        EnhancementRequest request);
}

public class CatalogGenerationRequest
{
    public ProductImageAnalysis? ImageAnalysis { get; set; }
    public SpeechTranscriptionResult? VoiceTranscript { get; set; }
    public Dictionary<string, string> ManualInputs { get; set; } = new();
    public List<ExtractedText> ExtractedTexts { get; set; } = new();
    public string? SellerId { get; set; }
    public string? PreferredLanguage { get; set; }
}

public class GeneratedCatalog
{
    public string ProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? ProductType { get; set; }
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = new();
    public Dictionary<string, CatalogValue> Specifications { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public SEOContent SEO { get; set; } = new();
    public decimal? DetectedPrice { get; set; }
    public string? PriceSource { get; set; }
    public List<MissingInformation> MissingInformation { get; set; } = new();
    public Dictionary<string, double> FieldConfidence { get; set; } = new();
    public double OverallConfidence { get; set; }
}

public class CatalogValue
{
    public string Value { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? Unit { get; set; }
}

public class SEOContent
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public string ImageAltText { get; set; } = string.Empty;
}

public class MissingInformation
{
    public string Field { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Hint { get; set; }
    public string Type { get; set; } = "text"; // text, number, dropdown, etc.
    public List<string>? SuggestedValues { get; set; }
}

public class EnhancementRequest
{
    public string Action { get; set; } = string.Empty; // "shorten", "expand", "translate", "professional"
    public string? TargetLanguage { get; set; }
    public Dictionary<string, string> AdditionalContext { get; set; } = new();
}
