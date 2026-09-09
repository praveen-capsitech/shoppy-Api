using ShoppyApp.Models;

namespace ShoppyApp.DTOs;

public sealed record ShippingAddressRequest(
    string FullName,
    string Phone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode);

public sealed record CreateCodOrderRequest(ShippingAddressRequest ShippingAddress);

public sealed record UpdateOrderStatusRequest(OrderStatus Status);

public sealed record OrderResponse(
    string Id,
    string UserId,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Subtotal,
    decimal DeliveryCharge,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    OrderStatus Status,
    ShippingAddress ShippingAddress,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record OrderItemResponse(
    string ProductId,
    string ProductName,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
