using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ZahSellerAI.API.Services;

/// <summary>
/// Coordinates small, bounded catalog agents. All model calls stay on the server;
/// deterministic validation remains the final authority before a listing is returned.
/// </summary>
public sealed class CatalogAgentOrchestrator
{
    private const string DefaultModel = "gemini-2.5-flash";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CatalogAgentOrchestrator> _logger;

    public CatalogAgentOrchestrator(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CatalogAgentOrchestrator> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<JsonObject> ExtractSellerFactsAsync(
        string utterance,
        JsonElement currentDraft,
        string preferredLanguage,
        CancellationToken cancellationToken)
    {
        var language = preferredLanguage == "en" ? "English" : "simple Tamil";
        var prompt = $$"""
            You are the fact-extraction agent for a product seller who may speak Tamil, Tanglish, or English.
            Extract only facts the seller explicitly said in this message. Merge no prior facts yourself; the app does that.
            Never guess a price, quantity, brand, material, warranty, origin, condition, or product feature.
            Return JSON with exactly these keys: "fields", "confidence", "clarification".
            "fields" may contain only productName, category, brand, model, price, mrp, quantity, color, size, material, ageGroup, gender, condition, warranty, connectivity, packSize, weight, countryOfOrigin, features.
            Use English values for recognized categories, colors, sizes, and condition. Keep the product name in clear English when you can translate it faithfully; otherwise put the seller's exact words in clarification and omit productName.
            A number is price only when the seller identifies it as a selling price/rate/₹ value. A number is quantity only when the seller identifies it as available stock/count. Do not confuse a size number with either.
            Set confidence to a JSON object of field-name to number from 0 to 1 for every extracted field. If the message is ambiguous, leave the fact out and ask one short clarifying question in {{language}}. Empty clarification is allowed.
            Existing draft for context (do not overwrite it): {{currentDraft.GetRawText()}}
            Seller message: {{utterance}}
            """;

        var response = await RunJsonAgentAsync("seller-fact-extractor", prompt, cancellationToken);
        return response;
    }

    public async Task<JsonObject> GenerateEnglishListingAsync(
        JsonElement draft,
        string preferredLanguage,
        CancellationToken cancellationToken)
    {
        var facts = ReadConfirmedFacts(draft);
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(ReadString(facts, "productName"))) missing.Add("product name");
        if (string.IsNullOrWhiteSpace(ReadString(facts, "category"))) missing.Add("category");
        if (ReadDecimal(facts, "price") is not > 0) missing.Add("selling price");
        if (ReadInt(facts, "quantity") is null) missing.Add("available quantity");
        if (missing.Count > 0)
        {
            throw new CatalogValidationException($"Please confirm: {string.Join(", ", missing)}.");
        }

        var sellerLanguage = preferredLanguage == "en" ? "English" : "simple Tamil";
        var writerPrompt = $$"""
            You are the English catalog writer agent for a small online seller.
            Write a clear, searchable listing using only the confirmed facts below.
            Translate the product name and every listing field into natural retail English. Preserve real brand names, model numbers, sizes, and measurements.
            Do not invent performance, quality, warranty, delivery, authenticity, popularity, origin, discount, or safety claims.
            Do not create an MRP, discount, or stock claim unless that fact is supplied. Do not add emojis.
            Keep the title concise. The long description should state the product and confirmed attributes only. Highlights should be short factual bullets.
            Also write sellerSummary in {{sellerLanguage}} explaining what the English listing says, so the seller can listen to it before publishing. sellerSummary is for the seller and is not part of the listing.
            Return JSON exactly with: productName, shortDescription, description, highlights (array), tags (array), seoTitle, seoDescription, sellerSummary.
            Confirmed facts: {{facts.ToJsonString(JsonOptions)}}
            """;
        var listing = await RunJsonAgentAsync("english-catalog-writer", writerPrompt, cancellationToken);

        var reviewerPrompt = $$"""
            You are the independent catalog quality reviewer agent.
            Compare the proposed English listing against the confirmed seller facts. Reject or report an issue for any unsupported factual claim, invented brand/model/material/warranty/origin/discount/delivery promise, missing product identity, non-English text in a listing field, or material contradiction.
            You may correct grammar and remove unsupported claims, but may not add facts. Keep sellerSummary separate; it may be in the seller's requested language and is not published.
            Return JSON exactly with: safe (boolean), issues (array of short strings), listing (the corrected listing object with the same keys).
            Confirmed facts: {{facts.ToJsonString(JsonOptions)}}
            Proposed listing: {{listing.ToJsonString(JsonOptions)}}
            """;
        var review = await RunJsonAgentAsync("catalog-quality-reviewer", reviewerPrompt, cancellationToken);
        if (review["safe"]?.GetValue<bool>() != true)
        {
            var issues = review["issues"]?.ToJsonString() ?? "[]";
            throw new CatalogValidationException($"The listing needs another review: {issues}");
        }

        var corrected = review["listing"] as JsonObject ?? listing;
        var title = ReadString(corrected, "productName");
        var shortDescription = ReadString(corrected, "shortDescription");
        var description = ReadString(corrected, "description");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(shortDescription) || string.IsNullOrWhiteSpace(description))
        {
            throw new CatalogValidationException("The English listing is incomplete. Please try again.");
        }

        var listingText = new List<string> { title, shortDescription, description };
        if (corrected["highlights"] is JsonArray highlights)
            listingText.AddRange(highlights.Select(item => item?.GetValue<string>() ?? string.Empty));
        if (listingText.Any(ContainsNonLatinLetters))
        {
            throw new CatalogValidationException("Some listing text could not be translated into English. Please clarify the product name and try again.");
        }

        corrected["sellerSummary"] ??= listing["sellerSummary"]?.DeepClone();
        corrected["category"] = facts["category"]?.DeepClone();
        corrected["brand"] = facts["brand"]?.DeepClone();
        corrected["model"] = facts["model"]?.DeepClone();
        corrected["price"] = facts["price"]?.DeepClone();
        corrected["mrp"] = facts["mrp"]?.DeepClone();
        corrected["quantity"] = facts["quantity"]?.DeepClone();
        corrected["attributes"] = new JsonObject();
        foreach (var key in new[] { "color", "size", "material", "ageGroup", "gender", "condition", "warranty", "connectivity", "packSize", "weight", "countryOfOrigin", "features" })
        {
            if (facts[key] is not null) ((JsonObject)corrected["attributes"]!)[key] = facts[key]!.DeepClone();
        }
        corrected["highlights"] ??= new JsonArray();
        corrected["tags"] ??= new JsonArray();
        return corrected;
    }

    private async Task<JsonObject> RunJsonAgentAsync(string agentName, string prompt, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["AI:Gemini:ApiKey"] ?? _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new CatalogAIUnavailableException("The AI listing service is not configured yet. Please contact support.");

        var model = _configuration["AI:Gemini:TextModel"] ?? DefaultModel;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json", temperature = 0.2 }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Catalog agent {AgentName} returned status {StatusCode}", agentName, (int)response.StatusCode);
            throw new CatalogAIUnavailableException("The AI assistant is temporarily unavailable. Your answers are still here; please try again.");
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var text = document.RootElement.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(text)) throw new JsonException("Empty model response");
            var json = JsonNode.Parse(text);
            return json as JsonObject ?? throw new JsonException("Agent response must be a JSON object");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Catalog agent {AgentName} returned invalid structured output", agentName);
            throw new CatalogAIUnavailableException("The AI response could not be checked. Please try again.");
        }
    }

    private static JsonObject ReadConfirmedFacts(JsonElement draft)
    {
        if (draft.ValueKind != JsonValueKind.Object) return new JsonObject();
        var facts = new JsonObject();
        foreach (var key in new[] { "productName", "category", "brand", "model", "price", "mrp", "quantity", "color", "size", "material", "ageGroup", "gender", "condition", "warranty", "connectivity", "packSize", "weight", "countryOfOrigin", "features" })
        {
            if (draft.TryGetProperty(key, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                facts[key] = JsonNode.Parse(value.GetRawText());
            }
        }
        return facts;
    }

    private static string ReadString(JsonObject value, string key) => value[key]?.GetValue<string>() ?? string.Empty;
    private static decimal? ReadDecimal(JsonObject value, string key) => decimal.TryParse(value[key]?.ToString(), out var parsed) ? parsed : null;
    private static int? ReadInt(JsonObject value, string key) => int.TryParse(value[key]?.ToString(), out var parsed) ? parsed : null;
    private static bool ContainsNonLatinLetters(string value) => value.EnumerateRunes().Any(rune => Rune.IsLetter(rune) && rune.Value > 0x024F);
}

public sealed class CatalogAIUnavailableException(string message) : Exception(message);
public sealed class CatalogValidationException(string message) : Exception(message);
