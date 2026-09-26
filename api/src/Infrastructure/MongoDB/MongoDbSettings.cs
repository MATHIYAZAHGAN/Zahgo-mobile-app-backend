namespace ZahSellerAI.Infrastructure.MongoDB;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    
    // Collection Names
    public string SellersCollection { get; set; } = "sellers";
    public string ProductsCollection { get; set; } = "products";
    public string CategoriesCollection { get; set; } = "categories";
    public string AIGenerationsCollection { get; set; } = "ai_generations";
    public string VoiceTranscriptsCollection { get; set; } = "voice_transcripts";
    public string ProductImagesCollection { get; set; } = "product_images";
    public string PublishingLogsCollection { get; set; } = "publishing_logs";
    public string AuditLogsCollection { get; set; } = "audit_logs";
}
