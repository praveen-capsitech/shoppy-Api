using MongoDB.Driver;
using ShoppyApp.DTOs;
using ShoppyApp.Models;

namespace ShoppyApp.Services;

public sealed class CartService
{
    private readonly IMongoCollection<Cart> _carts;
    private readonly ProductSnapshotService _products;

    public CartService(IMongoDatabase database, ProductSnapshotService products)
    {
        _carts = database.GetCollection<Cart>("carts");
        _products = products;
        _carts.Indexes.CreateOne(
            new CreateIndexModel<Cart>(
                Builders<Cart>.IndexKeys.Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true }));
    }

    public async Task<CartResponse> GetAsync(string userId)
    {
        var cart = await _carts.Find(x => x.UserId == userId).FirstOrDefaultAsync()
                   ?? new Cart { UserId = userId };

        return Map(cart);
    }

    public async Task<CartResponse> AddAsync(string userId, AddToCartRequest request)
    {
        if (request.Quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.");

        var product = await _products.GetAsync(request.ProductId)
                      ?? throw new KeyNotFoundException("Product not found.");

        if (product.StockQuantity < request.Quantity)
            throw new InvalidOperationException("Requested quantity is not available.");

        var cart = await _carts.Find(x => x.UserId == userId).FirstOrDefaultAsync()
                   ?? new Cart { UserId = userId };

        var item = cart.Items.FirstOrDefault(x => x.ProductId == request.ProductId);
        if (item is null)
        {
            cart.Items.Add(new CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,
                Quantity = request.Quantity
            });
        }
        else
        {
            var newQuantity = item.Quantity + request.Quantity;
            if (newQuantity > product.StockQuantity)
                throw new InvalidOperationException("Requested quantity is not available.");
            item.Quantity = newQuantity;
            item.UnitPrice = product.Price;
            item.ProductName = product.Name;
            item.ImageUrl = product.ImageUrl;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        var originalVersion = cart.Version;
        cart.Version++;
        await _carts.ReplaceOneAsync(
            x => x.UserId == userId && x.Version == originalVersion,
            cart,
            new ReplaceOptions { IsUpsert = true });

        return Map(cart);
    }

    public async Task<CartResponse> UpdateAsync(string userId, string productId, int quantity)
    {
        if (quantity <= 0) return await RemoveAsync(userId, productId);

        var cart = await _carts.Find(x => x.UserId == userId).FirstOrDefaultAsync()
                   ?? throw new KeyNotFoundException("Cart not found.");

        var item = cart.Items.FirstOrDefault(x => x.ProductId == productId)
                   ?? throw new KeyNotFoundException("Cart item not found.");

        var product = await _products.GetAsync(productId)
                      ?? throw new KeyNotFoundException("Product not found.");

        if (quantity > product.StockQuantity)
            throw new InvalidOperationException("Requested quantity is not available.");

        item.Quantity = quantity;
        item.UnitPrice = product.Price;
        cart.UpdatedAt = DateTime.UtcNow;
        var originalVersion = cart.Version;
        cart.Version++;
        var result = await _carts.ReplaceOneAsync(
            x => x.UserId == userId && x.Version == originalVersion, cart);
        if (result.ModifiedCount == 0)
            throw new InvalidOperationException("The cart changed. Please retry.");

        return Map(cart);
    }

    public async Task<CartResponse> RemoveAsync(string userId, string productId)
    {
        var cart = await _carts.Find(x => x.UserId == userId).FirstOrDefaultAsync()
                   ?? new Cart { UserId = userId };

        cart.Items.RemoveAll(x => x.ProductId == productId);
        cart.UpdatedAt = DateTime.UtcNow;
        var originalVersion = cart.Version;
        cart.Version++;
        await _carts.ReplaceOneAsync(
            x => x.UserId == userId && x.Version == originalVersion,
            cart,
            new ReplaceOptions { IsUpsert = true });

        return Map(cart);
    }

    public async Task ClearAsync(string userId)
    {
        await _carts.DeleteOneAsync(x => x.UserId == userId);
    }

    private static CartResponse Map(Cart cart)
    {
        var items = cart.Items.Select(x => new CartItemResponse(
            x.ProductId, x.ProductName, x.ImageUrl, x.UnitPrice, x.Quantity, x.UnitPrice * x.Quantity
        )).ToList();

        return new CartResponse(
            cart.Id,
            cart.UserId,
            items,
            items.Sum(x => x.LineTotal),
            items.Sum(x => x.Quantity));
    }
}
