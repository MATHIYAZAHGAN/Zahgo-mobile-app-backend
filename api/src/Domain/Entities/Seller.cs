using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ZahSellerAI.Domain.Enums;

namespace ZahSellerAI.Domain.Entities;

public class Seller
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public required string Name { get; set; }
    public string? ShopName { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public required string PasswordHash { get; set; }

    // Preferences
    public Language PreferredLanguage { get; set; } = Language.Tamil;
    public Language VoiceLanguage { get; set; } = Language.Tamil;
    public string DefaultCurrency { get; set; } = "INR";
    public bool AutoPublish { get; set; }
    public bool AutoImageEnhancement { get; set; } = true;

    // Store Information
    public string? StoreId { get; set; }
    public string? BusinessAddress { get; set; }
    public string? GSTNumber { get; set; }

    // Status
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Refresh Tokens
    public List<RefreshToken> RefreshTokens { get; set; } = new();
}

public class RefreshToken
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}
