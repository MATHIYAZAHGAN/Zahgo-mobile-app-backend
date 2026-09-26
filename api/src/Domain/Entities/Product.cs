using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ZahSellerAI.Domain.Enums;
using ZahSellerAI.Domain.ValueObjects;

namespace ZahSellerAI.Domain.Entities;

public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public required string SellerId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? StoreId { get; set; }

    // Basic Information
    public SourcedValue<string> Name { get; set; } = new();
    public string Slug { get; set; } = string.Empty;
    public SourcedValue<string> Brand { get; set; } = new();
    public SourcedValue<string> Model { get; set; } = new();

    // Category
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CategoryId { get; set; }
    public SourcedValue<string> CategoryName { get; set; } = new();
    public SourcedValue<string> SubCategoryName { get; set; } = new();
    public SourcedValue<string> ProductType { get; set; } = new();

    // Description
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = new();

    // Images
    public List<ProductImage> Images { get; set; } = new();

    // AI product image references (original + AI-generated stored separately)
    public ProductImages? ImageSet { get; set; }

    // AI metadata (provider, model, processing info)
    public ProductAIInfo? AI { get; set; }

    // Specifications (Dynamic)
    public List<ProductSpecification> Specifications { get; set; } = new();

    // Variants
    public List<ProductVariant> Variants { get; set; } = new();

    // Pricing
    public ProductPricing Pricing { get; set; } = new();

    // Inventory
    public ProductInventory Inventory { get; set; } = new();

    // SEO
    public ProductSEO SEO { get; set; } = new();

    // AI Metadata
    public AIMetadata? AIMetadata { get; set; }

    // Status
    public ProductStatus Status { get; set; } = ProductStatus.Draft;

    // Tags
    public List<string> Tags { get; set; } = new();

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
}

public class ProductImage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OriginalUrl { get; set; } = string.Empty;
    public string OptimizedUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string? ProcessedUrl { get; set; }
    public bool IsPrimary { get; set; }
    public int Order { get; set; }
    public string AltText { get; set; } = string.Empty;
    public ImageMetadata Metadata { get; set; } = new();
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class ImageMetadata
{
    public int Width { get; set; }
    public int Height { get; set; }
    public long SizeInBytes { get; set; }
    public string Format { get; set; } = string.Empty;
    public bool IsBlurry { get; set; }
    public bool IsLowLight { get; set; }
    public bool HasBackground { get; set; }
    public double QualityScore { get; set; }
}

public class ProductImages
{
    public string? Original { get; set; }
    public string? AiProductImage { get; set; }
    public string? Thumbnail { get; set; }
}

public class ProductAIInfo
{
    public string Provider { get; set; } = "gemini";
    public string Model { get; set; } = "gemini-3.1-flash-image";
    public bool Processed { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class ProductSpecification
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public SourcedValue<string> Value { get; set; } = new();
    public string? Unit { get; set; }
    public int Order { get; set; }
}

public class ProductVariant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new(); // e.g., { "color": "Red", "size": "M" }
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class ProductPricing
{
    public SourcedValue<decimal> Price { get; set; } = new();
    public SourcedValue<decimal> CompareAtPrice { get; set; } = new();
    public SourcedValue<decimal> MRP { get; set; } = new();
    public string Currency { get; set; } = "INR";
    public decimal? CostPrice { get; set; }
    public bool IsTaxInclusive { get; set; } = true;
    public decimal TaxPercentage { get; set; }
}

public class ProductInventory
{
    public SourcedValue<int> StockQuantity { get; set; } = new();
    public string? SKU { get; set; }
    public string? Barcode { get; set; }
    public bool TrackInventory { get; set; } = true;
    public bool AllowBackorder { get; set; }
    public int? LowStockThreshold { get; set; }
}

public class ProductSEO
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
}

public class AIMetadata
{
    public string GenerationId { get; set; } = Guid.NewGuid().ToString();
    public string Model { get; set; } = string.Empty;
    public double OverallConfidence { get; set; }
    public VoiceTranscript? VoiceTranscript { get; set; }
    public List<string> MissingInformation { get; set; } = new();
    public List<AIQuestion> Questions { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, double> FieldConfidence { get; set; } = new();
}

public class VoiceTranscript
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OriginalText { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public Language Language { get; set; }
    public string? TranslatedText { get; set; }
    public double Confidence { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime TranscribedAt { get; set; } = DateTime.UtcNow;
}

public class AIQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Question { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public List<string>? SuggestedAnswers { get; set; }
    public string? Answer { get; set; }
    public bool IsRequired { get; set; }
}

public enum QuestionType
{
    Text,
    Number,
    SingleChoice,
    MultipleChoice,
    Voice
}
