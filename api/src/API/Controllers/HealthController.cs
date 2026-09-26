using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ZahSellerAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Basic health check endpoint
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            service = "ZAH Seller AI API"
        });
    }

    /// <summary>
    /// Detailed health check with dependencies
    /// </summary>
    [HttpGet("detailed")]
    public async Task<IActionResult> GetDetailed()
    {
        var health = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            service = "ZAH Seller AI API",
            checks = new
            {
                api = "healthy",
                // TODO: Add MongoDB health check
                // TODO: Add AI provider health check
                // TODO: Add storage health check
            }
        };

        return Ok(health);
    }
}
