using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ZahSellerAI.Domain.Entities;

public class Category
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public required string Name { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ParentCategoryId { get; set; }

    public int Level { get; set; } // 0 = Root, 1 = Subcategory, 2 = Product Type
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;

    // Multilingual Support
    public Dictionary<string, string> LocalizedNames { get; set; } = new();

    // Icon/Image
    public string? IconUrl { get; set; }
    public string? ImageUrl { get; set; }

    // Specification Template
    public List<CategoryAttribute> Attributes { get; set; } = new();

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CategoryAttribute
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public AttributeType Type { get; set; }
    public bool IsRequired { get; set; }
    public List<string>? AllowedValues { get; set; }
    public string? Unit { get; set; }
    public int Order { get; set; }
}

public enum AttributeType
{
    Text,
    Number,
    Dropdown,
    MultiSelect,
    Boolean,
    Measurement
}
