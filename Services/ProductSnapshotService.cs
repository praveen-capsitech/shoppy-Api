using MongoDB.Bson;
using MongoDB.Driver;
using ShoppyApp.Models;

namespace ShoppyApp.Services;

/*
 * Keep product access isolated here so the cart/order feature can be adapted
 * to the Product model already present in your ShoppyApp.
 */
public sealed class ProductSnapshotService
{
    private readonly IMongoCollection<Product> _products;

    public ProductSnapshotService(IMongoDatabase database)
    {
        _products = database.GetCollection<Product>("products");
    }

    public async Task<ProductSnapshot?> GetAsync(string productId, IClientSessionHandle? session = null)
    {
        if (!ObjectId.TryParse(productId, out var objectId))
            return null;

        var filter = Builders<Product>.Filter.Eq(x => x.Id, objectId.ToString());
        var product = session is null
            ? await _products.Find(filter).FirstOrDefaultAsync()
            : await _products.Find(session, filter).FirstOrDefaultAsync();

        if (product is null) return null;

        return new ProductSnapshot(product.Id, product.Name, product.ImageUrl, product.Price, product.Stock);
    }

    public async Task<bool> TryReserveAsync(string productId, int quantity, IClientSessionHandle session)
    {
        if (!ObjectId.TryParse(productId, out var objectId) || quantity <= 0)
            return false;

        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq(x => x.Id, objectId.ToString()),
            Builders<Product>.Filter.Gte(x => x.Stock, quantity));
        var update = Builders<Product>.Update.Inc(x => x.Stock, -quantity);
        var result = await _products.UpdateOneAsync(session, filter, update);
        return result.ModifiedCount == 1;
    }

    public async Task ReleaseAsync(string productId, int quantity, IClientSessionHandle session)
    {
        if (!ObjectId.TryParse(productId, out var objectId) || quantity <= 0)
            return;

        await _products.UpdateOneAsync(
            session,
            Builders<Product>.Filter.Eq(x => x.Id, objectId.ToString()),
            Builders<Product>.Update.Inc(x => x.Stock, quantity));
    }
}

public sealed record ProductSnapshot(
    string Id,
    string Name,
    string? ImageUrl,
    decimal Price,
    int StockQuantity);
