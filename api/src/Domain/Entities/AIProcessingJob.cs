using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ZahSellerAI.Domain.Entities;

public class AIProcessingJob
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string JobId { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string OriginalImageUrl { get; set; } = string.Empty;

    public string Status { get; set; } = "uploaded"; // uploaded, processing, background_removed, generating, completed, failed
    public string CurrentStep { get; set; } = "Upload received";
    public int Progress { get; set; } = 10;
    public string? ErrorMessage { get; set; }

    public string? ProcessedImageUrl { get; set; }
    public string? BackgroundRemovedImageUrl { get; set; }
    public string? MainProductImageUrl { get; set; }
    public string? LifestyleImageUrl { get; set; }
    public string? ThumbnailImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
