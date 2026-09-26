using ZahSellerAI.Application.DTOs;

namespace ZahSellerAI.Application.Interfaces;

public interface IAuthenticationService
{
    Task<LoginResponse> RegisterAsync(RegisterRequest request, string ipAddress);
    Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress);
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, string ipAddress);
    Task<bool> RevokeTokenAsync(string refreshToken, string ipAddress);
    Task<SellerDto?> GetSellerByIdAsync(string sellerId);
}
