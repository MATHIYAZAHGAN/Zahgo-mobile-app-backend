using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ZahSellerAI.Domain.Entities;

namespace ZahSellerAI.Infrastructure.MongoDB;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbSettings _settings;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        _settings = settings.Value;
        
        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
        
        // Create indexes
        CreateIndexes();
    }

    // Collections
    public IMongoCollection<Seller> Sellers => 
        _database.GetCollection<Seller>(_settings.SellersCollection);
    
    public IMongoCollection<Product> Products => 
        _database.GetCollection<Product>(_settings.ProductsCollection);
    
    public IMongoCollection<Category> Categories => 
        _database.GetCollection<Category>(_settings.CategoriesCollection);

    private void CreateIndexes()
    {
        try
        {
            // Seller indexes
            var sellerIndexKeys = Builders<Seller>.IndexKeys
                .Ascending(s => s.Email);
            var sellerIndexOptions = new CreateIndexOptions { Unique = true };
            Sellers.Indexes.CreateOne(new CreateIndexModel<Seller>(sellerIndexKeys, sellerIndexOptions));

            var sellerPhoneIndexKeys = Builders<Seller>.IndexKeys
                .Ascending(s => s.PhoneNumber);
            var sellerPhoneIndexOptions = new CreateIndexOptions { Unique = true };
            Sellers.Indexes.CreateOne(new CreateIndexModel<Seller>(sellerPhoneIndexKeys, sellerPhoneIndexOptions));

            // Product indexes
            var productSellerIndexKeys = Builders<Product>.IndexKeys
                .Ascending(p => p.SellerId);
            Products.Indexes.CreateOne(new CreateIndexModel<Product>(productSellerIndexKeys));

            var productStatusIndexKeys = Builders<Product>.IndexKeys
                .Ascending(p => p.Status);
            Products.Indexes.CreateOne(new CreateIndexModel<Product>(productStatusIndexKeys));

            var productCreatedAtIndexKeys = Builders<Product>.IndexKeys
                .Descending(p => p.CreatedAt);
            Products.Indexes.CreateOne(new CreateIndexModel<Product>(productCreatedAtIndexKeys));

            // Compound index for seller + status
            var productCompoundIndexKeys = Builders<Product>.IndexKeys
                .Ascending(p => p.SellerId)
                .Ascending(p => p.Status);
            Products.Indexes.CreateOne(new CreateIndexModel<Product>(productCompoundIndexKeys));

            // Category indexes
            var categorySlugIndexKeys = Builders<Category>.IndexKeys
                .Ascending(c => c.Slug);
            var categorySlugIndexOptions = new CreateIndexOptions { Unique = true };
            Categories.Indexes.CreateOne(new CreateIndexModel<Category>(categorySlugIndexKeys, categorySlugIndexOptions));

            var categoryParentIndexKeys = Builders<Category>.IndexKeys
                .Ascending(c => c.ParentCategoryId);
            Categories.Indexes.CreateOne(new CreateIndexModel<Category>(categoryParentIndexKeys));
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[WARN] MongoDB index creation skipped (MongoDB offline or uninitialized): {ex.Message}");
        }
    }
}
