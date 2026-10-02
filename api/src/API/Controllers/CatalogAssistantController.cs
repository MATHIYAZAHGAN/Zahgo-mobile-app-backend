using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZahSellerAI.API.Services;
using ZahSellerAI.Shared.DTOs;

namespace ZahSellerAI.API.Controllers;

[ApiController]
[Route("api/v1/products/ai")]
[Authorize]
public sealed class CatalogAssistantController : ControllerBase
{
    private readonly CatalogAgentOrchestrator _agents;
    private readonly ILogger<CatalogAssistantController> _logger;

    public CatalogAssistantController(
        CatalogAgentOrchestrator agents,
        ILogger<CatalogAssistantController> logger)
    {
        _agents = agents;
        _logger = logger;
    }

    [HttpPost("assistant-turn")]
    public async Task<IActionResult> AssistantTurn([FromBody] AssistantTurnRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Utterance) || request.Utterance.Length > 4000)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("INVALID_UTTERANCE", "Please say or type a short product detail."));
        }

        try
        {
            var result = await _agents.ExtractSellerFactsAsync(
                request.Utterance.Trim(),
                request.CurrentDraft,
                request.PreferredLanguage,
                cancellationToken);
            var fields = result["fields"] as JsonObject ?? new JsonObject();
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                fields,
                confidence = result["confidence"]?.DeepClone() ?? new JsonObject(),
                clarification = result["clarification"]?.GetValue<string>() ?? string.Empty
            }));
        }
        catch (CatalogAIUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.ErrorResponse("AI_UNAVAILABLE", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seller assistant turn failed");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.ErrorResponse("ASSISTANT_FAILED", "I could not process that answer. Please try once more."));
        }
    }

    [HttpPost("generate-listing")]
    public async Task<IActionResult> GenerateListing([FromBody] GenerateListingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await _agents.GenerateEnglishListingAsync(
                request.Draft,
                request.PreferredLanguage,
                cancellationToken);
            return Ok(ApiResponse<JsonObject>.SuccessResponse(listing,
                "English listing generated and checked."));
        }
        catch (CatalogValidationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("LISTING_NEEDS_DETAILS", ex.Message));
        }
        catch (CatalogAIUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.ErrorResponse("AI_UNAVAILABLE", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "English catalog listing generation failed");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.ErrorResponse("LISTING_FAILED", "The listing could not be checked. Please try again."));
        }
    }
}

public sealed class AssistantTurnRequest
{
    public string Utterance { get; set; } = string.Empty;
    public JsonElement CurrentDraft { get; set; }
    public string PreferredLanguage { get; set; } = "ta";
}

public sealed class GenerateListingRequest
{
    public JsonElement Draft { get; set; }
    public string PreferredLanguage { get; set; } = "ta";
}
