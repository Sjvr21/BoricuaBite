using System.ComponentModel.DataAnnotations;
using BoricuaBite.Domain.Entities;

namespace BoricuaBite.Application.Orders;

public sealed record CreateOrderItem(
    [property: Required] Guid MenuItemId,
    [property: Range(1, 99)] int Quantity);

public sealed record CreateOrderRequest(
    [property: Required] Guid RestaurantId,
    [property: Required, MinLength(1)] IReadOnlyList<CreateOrderItem> Items,
    [property: Required] OrderPaymentMethod PaymentMethod);

public sealed record OrderItemResponse(Guid MenuItemId, string Name, decimal UnitPrice, int Quantity, decimal Subtotal);

public sealed record OrderResponse(
    Guid Id,
    Guid RestaurantId,
    string RestaurantName,
    Guid CustomerId,
    string? CustomerEmail,
    MarketplaceOrderStatus Status,
    OrderPaymentMethod PaymentMethod,
    OrderPaymentStatus PaymentStatus,
    decimal Subtotal,
    decimal TaxAmount,
    decimal ServiceFee,
    decimal CommissionAmount,
    decimal Total,
    decimal EstimatedRestaurantProceeds,
    string Currency,
    string? CheckoutUrl,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderItemResponse> Items);

public sealed record OrderStatusUpdate([property: Required] MarketplaceOrderStatus Status);

public sealed record CheckoutSessionResult(string SessionId, string CheckoutUrl);

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(Guid customerId, string? customerEmail, CreateOrderRequest request, CancellationToken ct);
    Task<IReadOnlyList<OrderResponse>> ListCustomerAsync(Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<OrderResponse>> ListOwnerAsync(Guid ownerId, Guid? restaurantId, CancellationToken ct);
    Task<OrderResponse?> UpdateOwnerStatusAsync(Guid ownerId, Guid orderId, MarketplaceOrderStatus status, CancellationToken ct);
    Task<OrderResponse?> MarkPaidByCheckoutSessionAsync(string sessionId, string? paymentIntentId, CancellationToken ct);
}

public interface ICheckoutProvider
{
    bool IsConfigured { get; }
    Task<CheckoutSessionResult> CreateOrderCheckoutAsync(MarketplaceOrder order, Restaurant restaurant, string customerEmail, CancellationToken ct);
    Task RefundAsync(string paymentIntentId, CancellationToken ct);
}
