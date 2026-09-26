using ZahSellerAI.Domain.Enums;

namespace ZahSellerAI.Application.DTOs;

public class CreateProductDraftRequest
{
    public required string SellerId { get; set; }
    public string? StoreId { get; set; }
}

public class CreateProductDraftResponse
{
    public required string ProductId { get; set; }
    public ProductStatus Status { get; set; }
    public required UploadUrls UploadUrls { get; set; }
}

public class UploadUrls
{
    public required string Images { get; set; }
    public required string Voice { get; set; }
}

public class StartAIProcessingRequest
{
    public required string ProductId { get; set; }
    public string? PreferredLanguage { get; set; }
}

public class AIProcessingStatusResponse
{
    public required string ProductId { get; set; }
    public ProductStatus Status { get; set; }
    public int Progress { get; set; } // 0-100
    public required string CurrentStage { get; set; }
    public int? EstimatedTimeRemaining { get; set; } // seconds
    public string? Error { get; set; }
}

public class ProductListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public ProductStatus? Status { get; set; }
    public string? SearchQuery { get; set; }
}

public class ProductListResponse
{
    public required List<ProductSummaryDto> Products { get; set; }
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

public class ProductSummaryDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Brand { get; set; }
    public decimal? Price { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public ProductStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PublishProductRequest
{
    public required string ProductId { get; set; }
}

public class PublishProductResponse
{
    public bool Success { get; set; }
    public required string ProductId { get; set; }
    public string? PublishedUrl { get; set; }
    public required string Message { get; set; }
}
