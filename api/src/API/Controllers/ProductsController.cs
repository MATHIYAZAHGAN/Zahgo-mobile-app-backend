using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZahSellerAI.Application.DTOs;
using ZahSellerAI.Domain.Entities;
using ZahSellerAI.Domain.Enums;
using ZahSellerAI.Domain.ValueObjects;
using ZahSellerAI.Infrastructure.MongoDB.Repositories;
using ZahSellerAI.Shared.DTOs;

namespace ZahSellerAI.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductRepository productRepository,
        ILogger<ProductsController> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    /// <summary>
    /// Create a new product draft
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateProductDraftResponse>), 200)]
    public async Task<IActionResult> CreateDraft()
    {
        try
        {
            var sellerId = GetSellerIdFromClaims();

            var product = new Product
            {
                SellerId = sellerId,
                Status = ProductStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _productRepository.CreateAsync(product);

            _logger.LogInformation("Product draft created: {ProductId} for seller: {SellerId}", 
                product.Id, sellerId);

            var response = new CreateProductDraftResponse
            {
                ProductId = product.Id,
                Status = product.Status,
                UploadUrls = new UploadUrls
                {
                    Images = $"/api/v1/products/{product.Id}/images",
                    Voice = $"/api/v1/products/{product.Id}/voice"
                }
            };

            return Ok(ApiResponse<CreateProductDraftResponse>.SuccessResponse(
                response,
                "Product draft created successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product draft");
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Failed to create product draft. Please try again."
            ));
        }
    }

    /// <summary>
    /// Get products list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProductListResponse>), 200)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ProductStatus? status = null,
        [FromQuery] string? search = null)
    {
        try
        {
            var sellerId = GetSellerIdFromClaims();

            List<Product> products;
            int total;

            if (!string.IsNullOrWhiteSpace(search))
            {
                products = await _productRepository.SearchAsync(sellerId, search, page, pageSize);
                total = products.Count; // Simplified for demo
            }
            else
            {
                products = await _productRepository.GetBySellerIdAsync(sellerId, page, pageSize, status);
                total = await _productRepository.CountBySellerIdAsync(sellerId, status);
            }

            var response = new ProductListResponse
            {
                Products = products.Select(p => new ProductSummaryDto
                {
                    Id = p.Id,
                    Name = p.Name.Value ?? "Untitled Product",
                    Brand = p.Brand.Value,
                    Price = p.Pricing.Price.Value,
                    PrimaryImageUrl = p.Images.FirstOrDefault(i => i.IsPrimary)?.ThumbnailUrl,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize,
                HasMore = total > (page * pageSize)
            };

            return Ok(ApiResponse<ProductListResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products");
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Failed to get products. Please try again."
            ));
        }
    }

    /// <summary>
    /// Get product by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<Product>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetProduct(string id)
    {
        try
        {
            var sellerId = GetSellerIdFromClaims();
            var product = await _productRepository.GetByIdAndSellerIdAsync(id, sellerId);

            if (product == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(
                    "PRODUCT_NOT_FOUND",
                    "Product not found"
                ));
            }

            return Ok(ApiResponse<Product>.SuccessResponse(product));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product {ProductId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Failed to get product. Please try again."
            ));
        }
    }

    /// <summary>
    /// Update product
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<Product>), 200)]
    public async Task<IActionResult> UpdateProduct(string id, [FromBody] Product updatedProduct)
    {
        try
        {
            var sellerId = GetSellerIdFromClaims();
            var product = await _productRepository.GetByIdAndSellerIdAsync(id, sellerId);

            if (product == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(
                    "PRODUCT_NOT_FOUND",
                    "Product not found"
                ));
            }

            // Update fields (simplified - in production, use proper mapping)
            updatedProduct.Id = product.Id;
            updatedProduct.SellerId = product.SellerId;
            updatedProduct.CreatedAt = product.CreatedAt;

            await _productRepository.UpdateAsync(updatedProduct);

            _logger.LogInformation("Product updated: {ProductId}", id);

            return Ok(ApiResponse<Product>.SuccessResponse(
                updatedProduct,
                "Product updated successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Failed to update product. Please try again."
            ));
        }
    }

    /// <summary>
    /// Publish product
    /// </summary>
    [HttpPost("{id}/publish")]
    [ProducesResponseType(typeof(ApiResponse<PublishProductResponse>), 200)]
    public async Task<IActionResult> PublishProduct(string id)
    {
        try
        {
            var sellerId = GetSellerIdFromClaims();
            var product = await _productRepository.GetByIdAndSellerIdAsync(id, sellerId);

            if (product == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(
                    "PRODUCT_NOT_FOUND",
                    "Product not found"
                ));
            }

            // Validate product is ready to publish
            if (product.Status != ProductStatus.Ready && product.Status != ProductStatus.Draft)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse(
                    "INVALID_STATUS",
                    $"Product cannot be published in {product.Status} status"
                ));
            }

            // Update status to published
            product.Status = ProductStatus.Published;
            product.PublishedAt = DateTime.UtcNow;
            await _productRepository.UpdateAsync(product);

            _logger.LogInformation("Product published: {ProductId}", id);

            var response = new PublishProductResponse
            {
                Success = true,
                ProductId = product.Id,
                PublishedUrl = $"/products/{product.Slug}", // Adjust based on your e-commerce site
                Message = "Product published successfully! 🎉"
            };

            return Ok(ApiResponse<PublishProductResponse>.SuccessResponse(
                response,
                "Product published successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing product {ProductId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Failed to publish product. Please try again."
            ));
        }
    }

    /// <summary>
    /// Delete product
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> DeleteProduct(string id)
    {
        try
        {
            var sellerId = GetSellerIdFromClaims();
            var deleted = await _productRepository.DeleteAsync(id, sellerId);

            if (!deleted)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(
                    "PRODUCT_NOT_FOUND",
                    "Product not found"
                ));
            }

            _logger.LogInformation("Product deleted: {ProductId}", id);

            return Ok(ApiResponse<object>.SuccessResponse(
                new { },
                "Product deleted successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Failed to delete product. Please try again."
            ));
        }
    }

    /// <summary>
    /// Get AI processing status
    /// </summary>
    [HttpGet("{id}/ai/status")]
    [ProducesResponseType(typeof(ApiResponse<AIProcessingStatusResponse>), 200)]
    public async Task<IActionResult> GetAIStatus(string id)
    {
        try
        {
            var sellerId = GetSellerIdFromClaims();
            var product = await _productRepository.GetByIdAndSellerIdAsync(id, sellerId);

            if (product == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(
                    "PRODUCT_NOT_FOUND",
                    "Product not found"
                ));
            }

            var response = new AIProcessingStatusResponse
            {
                ProductId = product.Id,
                Status = product.Status,
                Progress = GetProgress(product.Status),
                CurrentStage = GetStageDescription(product.Status),
                EstimatedTimeRemaining = GetEstimatedTime(product.Status)
            };

            return Ok(ApiResponse<AIProcessingStatusResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI status for product {ProductId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "INTERNAL_ERROR",
                "Failed to get AI status. Please try again."
            ));
        }
    }

    private string GetSellerIdFromClaims()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Seller ID not found in token");
    }

    private int GetProgress(ProductStatus status)
    {
        return status switch
        {
            ProductStatus.Draft => 0,
            ProductStatus.Uploading => 20,
            ProductStatus.Processing => 50,
            ProductStatus.AIReview => 80,
            ProductStatus.Ready => 100,
            ProductStatus.Published => 100,
            ProductStatus.Failed => 0,
            _ => 0
        };
    }

    private string GetStageDescription(ProductStatus status)
    {
        return status switch
        {
            ProductStatus.Draft => "Draft created",
            ProductStatus.Uploading => "Uploading media files",
            ProductStatus.Processing => "AI is analyzing your product",
            ProductStatus.AIReview => "Finalizing catalog",
            ProductStatus.Ready => "Ready for review",
            ProductStatus.Published => "Published",
            ProductStatus.Failed => "Processing failed",
            _ => "Unknown"
        };
    }

    private int? GetEstimatedTime(ProductStatus status)
    {
        return status switch
        {
            ProductStatus.Uploading => 30,
            ProductStatus.Processing => 45,
            ProductStatus.AIReview => 10,
            _ => null
        };
    }
}
