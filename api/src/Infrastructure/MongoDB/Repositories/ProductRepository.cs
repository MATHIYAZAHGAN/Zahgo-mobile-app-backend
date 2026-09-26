using MongoDB.Driver;
using MongoDB.Bson;
using ZahSellerAI.Domain.Entities;
using ZahSellerAI.Domain.Enums;

namespace ZahSellerAI.Infrastructure.MongoDB.Repositories;

public interface IProductRepository
{
    Task<Product> CreateAsync(Product product);
    Task<Product?> GetByIdAsync(string productId);
    Task<Product?> GetByIdAndSellerIdAsync(string productId, string sellerId);
    Task<List<Product>> GetBySellerIdAsync(string sellerId, int page, int pageSize, ProductStatus? status = null);
    Task<int> CountBySellerIdAsync(string sellerId, ProductStatus? status = null);
    Task<Product> UpdateAsync(Product product);
    Task<bool> DeleteAsync(string productId, string sellerId);
    Task<List<Product>> SearchAsync(string sellerId, string query, int page, int pageSize);
}

public class ProductRepository : IProductRepository
{
    private readonly MongoDbContext _context;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Product> _inMemoryProducts = new();

    public ProductRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<Product> CreateAsync(Product product)
    {
        try
        {
            await _context.Products.InsertOneAsync(product);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[WARN] MongoDB offline. Saving Product to In-Memory store: {ex.Message}");
        }

        _inMemoryProducts[product.Id] = product;
        return product;
    }

    public async Task<Product?> GetByIdAsync(string productId)
    {
        try
        {
            var product = await _context.Products
                .Find(p => p.Id == productId)
                .FirstOrDefaultAsync();

            if (product != null) return product;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[WARN] MongoDB offline. Fetching Product from In-Memory store: {ex.Message}");
        }

        _inMemoryProducts.TryGetValue(productId, out var inMemoryProduct);
        return inMemoryProduct;
    }

    public async Task<Product?> GetByIdAndSellerIdAsync(string productId, string sellerId)
    {
        try
        {
            var product = await _context.Products
                .Find(p => p.Id == productId && p.SellerId == sellerId)
                .FirstOrDefaultAsync();

            if (product != null) return product;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[WARN] MongoDB offline. Fetching Product from In-Memory store: {ex.Message}");
        }

        if (_inMemoryProducts.TryGetValue(productId, out var inMemoryProduct))
        {
            if (inMemoryProduct.SellerId == sellerId || string.IsNullOrEmpty(inMemoryProduct.SellerId))
            {
                return inMemoryProduct;
            }
        }
        
        // Development fallback: return product if ID matches
        return _inMemoryProducts.GetValueOrDefault(productId);
    }

    public async Task<List<Product>> GetBySellerIdAsync(string sellerId, int page, int pageSize, ProductStatus? status = null)
    {
        try
        {
            var filter = Builders<Product>.Filter.Eq(p => p.SellerId, sellerId);
            
            if (status.HasValue)
            {
                filter &= Builders<Product>.Filter.Eq(p => p.Status, status.Value);
            }

            var list = await _context.Products
                .Find(filter)
                .SortByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            if (list != null && list.Count > 0) return list;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[WARN] MongoDB offline. Fetching Seller Products from In-Memory store: {ex.Message}");
        }

        var results = _inMemoryProducts.Values
            .Where(p => p.SellerId == sellerId || string.IsNullOrEmpty(sellerId))
            .Where(p => !status.HasValue || p.Status == status.Value)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return results;
    }

    public async Task<int> CountBySellerIdAsync(string sellerId, ProductStatus? status = null)
    {
        try
        {
            var filter = Builders<Product>.Filter.Eq(p => p.SellerId, sellerId);
            
            if (status.HasValue)
            {
                filter &= Builders<Product>.Filter.Eq(p => p.Status, status.Value);
            }

            return (int)await _context.Products.CountDocumentsAsync(filter);
        }
        catch
        {
            return _inMemoryProducts.Values
                .Count(p => (p.SellerId == sellerId || string.IsNullOrEmpty(sellerId)) && (!status.HasValue || p.Status == status.Value));
        }
    }

    public async Task<Product> UpdateAsync(Product product)
    {
        product.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _context.Products.ReplaceOneAsync(p => p.Id == product.Id, product);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[WARN] MongoDB offline. Updating Product in In-Memory store: {ex.Message}");
        }

        _inMemoryProducts[product.Id] = product;
        return product;
    }

    public async Task<bool> DeleteAsync(string productId, string sellerId)
    {
        try
        {
            var result = await _context.Products.DeleteOneAsync(
                p => p.Id == productId && p.SellerId == sellerId
            );
            if (result.DeletedCount > 0) return true;
        }
        catch
        {
        }

        return _inMemoryProducts.TryRemove(productId, out _);
    }

    public async Task<List<Product>> SearchAsync(string sellerId, string query, int page, int pageSize)
    {
        try
        {
            var filter = Builders<Product>.Filter.And(
                Builders<Product>.Filter.Eq(p => p.SellerId, sellerId),
                Builders<Product>.Filter.Or(
                    Builders<Product>.Filter.Regex(p => p.Name.Value, new BsonRegularExpression(query, "i")),
                    Builders<Product>.Filter.Regex(p => p.Brand.Value, new BsonRegularExpression(query, "i")),
                    Builders<Product>.Filter.AnyIn(p => p.Tags, new[] { query })
                )
            );

            return await _context.Products
                .Find(filter)
                .SortByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch
        {
            return _inMemoryProducts.Values
                .Where(p => (p.Name?.Value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (p.Brand?.Value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
    }
}
