using MongoDB.Driver;
using ShoppyApp.DTOs;
using ShoppyApp.Models;

namespace ShoppyApp.Services;

public sealed class OrderService
{
    private const decimal DeliveryCharge = 0m;

    private readonly IMongoCollection<Order> _orders;
    private readonly IMongoCollection<Cart> _carts;
    private readonly ProductSnapshotService _products;
    private readonly IMongoClient _client;

    public OrderService(IMongoDatabase database, IMongoClient client, ProductSnapshotService products)
    {
        _orders = database.GetCollection<Order>("orders");
        _carts = database.GetCollection<Cart>("carts");
        _client = client;
        _products = products;

        _orders.Indexes.CreateMany([
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(x => x.UserId)),
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Descending(x => x.CreatedAt)),
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(x => x.Status))
        ]);
    }

    public async Task<OrderResponse> CreateCodAsync(string userId, CreateCodOrderRequest request)
    {
        ValidateAddress(request.ShippingAddress);

        using var session = await _client.StartSessionAsync();
        session.StartTransaction();

        try
        {
            var cart = await _carts.Find(session, x => x.UserId == userId).FirstOrDefaultAsync();
            if (cart is null || cart.Items.Count == 0)
                throw new InvalidOperationException("Your cart is empty.");

            var items = new List<OrderItem>();

            foreach (var cartItem in cart.Items)
            {
                var product = await _products.GetAsync(cartItem.ProductId, session)
                              ?? throw new InvalidOperationException(
                                  $"Product '{cartItem.ProductName}' is no longer available.");

                if (!await _products.TryReserveAsync(product.Id, cartItem.Quantity, session))
                    throw new InvalidOperationException(
                        $"Only the available stock of '{product.Name}' can be ordered.");

                items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ImageUrl = product.ImageUrl,
                    UnitPrice = product.Price,
                    Quantity = cartItem.Quantity
                });
            }

            var subtotal = items.Sum(x => x.LineTotal);

            var order = new Order
            {
                UserId = userId,
                Items = items,
                Subtotal = subtotal,
                DeliveryCharge = DeliveryCharge,
                TotalAmount = subtotal + DeliveryCharge,
                PaymentMethod = PaymentMethod.CashOnDelivery,
                Status = OrderStatus.Pending,
                StockReserved = true,
                ShippingAddress = new ShippingAddress
                {
                    FullName = request.ShippingAddress.FullName.Trim(),
                    Phone = request.ShippingAddress.Phone.Trim(),
                    AddressLine1 = request.ShippingAddress.AddressLine1.Trim(),
                    AddressLine2 = request.ShippingAddress.AddressLine2?.Trim(),
                    City = request.ShippingAddress.City.Trim(),
                    State = request.ShippingAddress.State.Trim(),
                    PostalCode = request.ShippingAddress.PostalCode.Trim()
                },
                CreatedAt = DateTime.UtcNow
            };

            await _orders.InsertOneAsync(session, order);
            await _carts.DeleteOneAsync(session, x => x.UserId == userId);
            await session.CommitTransactionAsync();

            return Map(order);
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<OrderResponse>> GetMineAsync(string userId)
    {
        var orders = await _orders.Find(x => x.UserId == userId)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return orders.Select(Map).ToList();
    }

    public async Task<OrderResponse?> GetMineByIdAsync(string userId, string id)
    {
        var order = await _orders.Find(x => x.Id == id && x.UserId == userId)
            .FirstOrDefaultAsync();

        return order is null ? null : Map(order);
    }

    public async Task<IReadOnlyList<OrderResponse>> GetAllAsync()
    {
        var orders = await _orders.Find(FilterDefinition<Order>.Empty)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return orders.Select(Map).ToList();
    }

    public async Task<OrderResponse?> UpdateStatusAsync(string id, OrderStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentException("Invalid order status.");

        var order = await _orders.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (order is null) return null;

        if (!IsValidTransition(order.Status, status))
            throw new InvalidOperationException(
                $"Cannot change order from {order.Status} to {status}.");

        var originalStatus = order.Status;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        var result = await _orders.ReplaceOneAsync(
            x => x.Id == id && x.Status == originalStatus, order);
        if (result.ModifiedCount == 0)
            throw new InvalidOperationException("The order changed. Please retry.");

        return Map(order);
    }

    public async Task<OrderResponse?> CancelMineAsync(string userId, string id)
    {
        var order = await _orders.Find(x => x.Id == id && x.UserId == userId)
            .FirstOrDefaultAsync();

        if (order is null) return null;

        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
            throw new InvalidOperationException(
                "Only pending or confirmed orders can be cancelled.");

        using var session = await _client.StartSessionAsync();
        session.StartTransaction();

        try
        {
            var result = await _orders.ReplaceOneAsync(
                session,
                x => x.Id == id && x.UserId == userId && x.Status == order.Status,
                new Order
                {
                    Id = order.Id,
                    UserId = order.UserId,
                    Items = order.Items,
                    Subtotal = order.Subtotal,
                    DeliveryCharge = order.DeliveryCharge,
                    TotalAmount = order.TotalAmount,
                    PaymentMethod = order.PaymentMethod,
                    Status = OrderStatus.Cancelled,
                    StockReserved = order.StockReserved,
                    ShippingAddress = order.ShippingAddress,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                });

            if (result.ModifiedCount == 0)
                throw new InvalidOperationException("The order changed. Please retry.");

            if (order.StockReserved)
            {
                foreach (var item in order.Items)
                    await _products.ReleaseAsync(item.ProductId, item.Quantity, session);
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await session.CommitTransactionAsync();
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }

        return Map(order);
    }

    private static bool IsValidTransition(OrderStatus current, OrderStatus next)
    {
        return Enum.IsDefined(next);
    }

    private static void ValidateAddress(ShippingAddressRequest a)
    {
        if (string.IsNullOrWhiteSpace(a.FullName) ||
            string.IsNullOrWhiteSpace(a.Phone) ||
            string.IsNullOrWhiteSpace(a.AddressLine1) ||
            string.IsNullOrWhiteSpace(a.City) ||
            string.IsNullOrWhiteSpace(a.State) ||
            string.IsNullOrWhiteSpace(a.PostalCode))
            throw new ArgumentException("All required shipping address fields must be provided.");
    }

    private static OrderResponse Map(Order order) =>
        new(
            order.Id,
            order.UserId,
            order.Items.Select(x => new OrderItemResponse(
                x.ProductId, x.ProductName, x.ImageUrl, x.UnitPrice, x.Quantity, x.LineTotal)).ToList(),
            order.Subtotal,
            order.DeliveryCharge,
            order.TotalAmount,
            order.PaymentMethod,
            order.Status,
            order.ShippingAddress,
            order.CreatedAt,
            order.UpdatedAt);
}
