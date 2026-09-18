using MongoDB.Driver;
using ShoppyApp.Data;
using ShoppyApp.DTOs;
using ShoppyApp.Models;

namespace ShoppyApp.Services;

public sealed class ProductService
{
    private readonly IMongoCollection<Product> _products;
    private readonly IMongoCollection<User> _users;

    public ProductService(MongoDbContext database)
    {
        _products = database.Products;
        _users = database.Users;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync()
    {
        return await _products.Find(_ => true)
            .SortByDescending(product => product.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Product>> GetManagedAsync(string userId, bool isAdmin)
    {
        var filter = isAdmin
            ? Builders<Product>.Filter.Empty
            : Builders<Product>.Filter.Eq(product => product.OwnerId, userId);

        return await _products.Find(filter)
            .SortByDescending(product => product.CreatedAt)
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        return await _products.Find(product => product.Id == id).FirstOrDefaultAsync();
    }

    public async Task<ProductDetailsResponse?> GetDetailsAsync(string id)
    {
        var product = await GetByIdAsync(id);
        if (product is null) return null;

        var owner = await _users.Find(user => user.Id == product.OwnerId).FirstOrDefaultAsync();

        return new ProductDetailsResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Stock,
            product.Category,
            product.ImageUrl,
            product.CreatedAt,
            owner?.Name ?? "Unknown owner");
    }

    public async Task<Product> CreateAsync(string ownerId, CreateProductRequest request)
    {
        Validate(request.Name, request.Description, request.Category, request.ImageUrl, request.Price, request.Stock);

        var product = new Product
        {
            OwnerId = ownerId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            Category = request.Category,
            ImageUrl = request.ImageUrl
        };

        await _products.InsertOneAsync(product);
        return product;
    }

    public async Task<Product?> UpdateAsync(string id, string userId, bool isAdmin, UpdateProductRequest request)
    {
        Validate(request.Name, request.Description, request.Category, request.ImageUrl, request.Price, request.Stock);

        var existing = await GetByIdAsync(id);
        if (existing is null) return null;
        EnsureCanManage(existing, userId, isAdmin);

        var update = Builders<Product>.Update
            .Set(product => product.Name, request.Name)
            .Set(product => product.Description, request.Description)
            .Set(product => product.Price, request.Price)
            .Set(product => product.Stock, request.Stock)
            .Set(product => product.Category, request.Category)
            .Set(product => product.ImageUrl, request.ImageUrl);

        return await _products.FindOneAndUpdateAsync(
            product => product.Id == id,
            update,
            new FindOneAndUpdateOptions<Product> { ReturnDocument = ReturnDocument.After });
    }

    public async Task<bool> DeleteAsync(string id, string userId, bool isAdmin)
    {
        var existing = await GetByIdAsync(id);
        if (existing is null) return false;
        EnsureCanManage(existing, userId, isAdmin);

        var result = await _products.DeleteOneAsync(product => product.Id == id);
        return result.DeletedCount > 0;
    }

    private static void EnsureCanManage(Product product, string userId, bool isAdmin)
    {
        if (!isAdmin && product.OwnerId != userId)
            throw new UnauthorizedAccessException();
    }

    private static void Validate(
        string name,
        string description,
        string category,
        string imageUrl,
        decimal price,
        int stock)
    {
        var valid = !string.IsNullOrWhiteSpace(name) && name.Length <= 200 &&
                    !string.IsNullOrWhiteSpace(description) && description.Length <= 2000 &&
                    !string.IsNullOrWhiteSpace(category) && category.Length <= 100 &&
                    !string.IsNullOrWhiteSpace(imageUrl) && imageUrl.Length <= 2000 &&
                    Uri.TryCreate(imageUrl, UriKind.Absolute, out _) &&
                    price >= 0 && stock >= 0;

        if (!valid)
            throw new ArgumentException("Product fields are invalid.");
    }
}