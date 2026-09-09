namespace ShoppyApp.DTOs;

public sealed record AddToCartRequest(string ProductId, int Quantity = 1);
public sealed record UpdateCartItemRequest(int Quantity);

public sealed record CartResponse(
    string Id,
    string UserId,
    IReadOnlyList<CartItemResponse> Items,
    decimal Subtotal,
    int TotalItems);

public sealed record CartItemResponse(
    string ProductId,
    string ProductName,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
