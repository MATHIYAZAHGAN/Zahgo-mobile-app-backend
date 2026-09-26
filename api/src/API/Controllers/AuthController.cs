using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZahSellerAI.Application.DTOs;
using ZahSellerAI.Application.Interfaces;
using ZahSellerAI.Shared.DTOs;

namespace ZahSellerAI.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new seller
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        Console.WriteLine("🔵 Register endpoint called");
        Console.WriteLine($"📥 Request data: Name={request.Name}, Email={request.Email}, Phone={request.PhoneNumber}");
        
        try
        {
            var ipAddress = GetIpAddress();
            Console.WriteLine($"📍 IP Address: {ipAddress}");
            
            var result = await _authService.RegisterAsync(request, ipAddress);
            
            Console.WriteLine($"✅ Registration successful for: {request.Email}");
            _logger.LogInformation("Seller registered: {Email}", request.Email);

            return Ok(ApiResponse<LoginResponse>.SuccessResponse(
                result,
                "Registration successful"
            ));
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"❌ Registration failed (InvalidOperation): {ex.Message}");
            _logger.LogWarning(ex, "Registration failed for {Email}", request.Email);
            
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "REGISTRATION_FAILED",
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Registration failed (Exception): {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error during registration");
            
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Registration failed. Please try again."
            ));
        }
    }

    /// <summary>
    /// Login with email/phone and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var ipAddress = GetIpAddress();
            var result = await _authService.LoginAsync(request, ipAddress);

            _logger.LogInformation("Seller logged in: {EmailOrPhone}", request.EmailOrPhone);

            return Ok(ApiResponse<LoginResponse>.SuccessResponse(
                result,
                "Login successful"
            ));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse(
                "LOGIN_FAILED",
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Login failed. Please try again."
            ));
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var ipAddress = GetIpAddress();
            var result = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress);

            return Ok(ApiResponse<LoginResponse>.SuccessResponse(
                result,
                "Token refreshed successfully"
            ));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse(
                "REFRESH_FAILED",
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Token refresh failed. Please try again."
            ));
        }
    }

    /// <summary>
    /// Logout and revoke refresh token
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var ipAddress = GetIpAddress();
            await _authService.RevokeTokenAsync(request.RefreshToken, ipAddress);

            _logger.LogInformation("Seller logged out");

            return Ok(ApiResponse<object>.SuccessResponse(
                new { },
                "Logout successful"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Logout failed. Please try again."
            ));
        }
    }

    /// <summary>
    /// Get current seller profile
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SellerDto>), 200)]
    public async Task<IActionResult> GetMe()
    {
        try
        {
            var sellerId = GetSellerIdFromClaims();
            var seller = await _authService.GetSellerByIdAsync(sellerId);

            if (seller == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(
                    "SELLER_NOT_FOUND",
                    "Seller not found"
                ));
            }

            return Ok(ApiResponse<SellerDto>.SuccessResponse(seller));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting seller profile");
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Failed to get profile. Please try again."
            ));
        }
    }

    private string GetIpAddress()
    {
        if (Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            return Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string GetSellerIdFromClaims()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Seller ID not found in token");
    }
}
