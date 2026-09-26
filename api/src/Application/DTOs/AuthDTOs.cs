namespace ZahSellerAI.Application.DTOs;

public class RegisterRequest
{
    public required string Name { get; set; }
    public string? ShopName { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Password { get; set; }
    public string PreferredLanguage { get; set; } = "tamil";
}

public class LoginRequest
{
    public required string EmailOrPhone { get; set; }
    public required string Password { get; set; }
}

public class LoginResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required SellerDto Seller { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class RefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}

public class SellerDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? ShopName { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public string PreferredLanguage { get; set; } = "tamil";
    public string VoiceLanguage { get; set; } = "tamil";
    public bool AutoPublish { get; set; }
    public bool AutoImageEnhancement { get; set; }
}
