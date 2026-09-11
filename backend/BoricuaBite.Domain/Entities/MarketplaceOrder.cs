using BoricuaBite.Domain.Common;

namespace BoricuaBite.Domain.Entities;

public enum MarketplaceOrderStatus
{
    Pending,
    Accepted,
    Preparing,
    ReadyForPickup,
    Completed,
    Rejected,
    Cancelled
}

public enum OrderPaymentMethod
{
    Online,
    PayAtStore
}

public enum OrderPaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded
}

public sealed class MarketplaceOrder : BaseEntity
{
    private readonly List<MarketplaceOrderItem> items = [];

    private MarketplaceOrder() { }

    public MarketplaceOrder(Guid restaurantId, Guid customerId, OrderPaymentMethod paymentMethod,
        decimal subtotal, decimal taxAmount, decimal platformFee, IEnumerable<MarketplaceOrderItem> orderItems)
    {
        if (restaurantId == Guid.Empty) throw new ArgumentException("Restaurant is required.", nameof(restaurantId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer is required.", nameof(customerId));
        if (subtotal < 0 || taxAmount < 0 || platformFee < 0) throw new ArgumentOutOfRangeException(nameof(subtotal));

        var snapshots = orderItems?.ToList() ?? throw new ArgumentNullException(nameof(orderItems));
        if (snapshots.Count == 0) throw new ArgumentException("An order must contain at least one item.", nameof(orderItems));

        RestaurantId = restaurantId;
        CustomerId = customerId;
        PaymentMethod = paymentMethod;
        Subtotal = Money(subtotal);
        TaxAmount = Money(taxAmount);
        PlatformFee = Money(platformFee);
        Total = Money(Subtotal + TaxAmount + PlatformFee);
        items.AddRange(snapshots);
    }

    public Guid RestaurantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public MarketplaceOrderStatus Status { get; private set; } = MarketplaceOrderStatus.Pending;
    public OrderPaymentMethod PaymentMethod { get; private set; }
    public OrderPaymentStatus PaymentStatus { get; private set; } = OrderPaymentStatus.Pending;
    public decimal Subtotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal PlatformFee { get; private set; }
    public decimal Total { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? StripeCheckoutSessionId { get; private set; }
    public IReadOnlyCollection<MarketplaceOrderItem> Items => items.AsReadOnly();

    public void AttachCheckoutSession(string sessionId)
    {
        if (PaymentMethod != OrderPaymentMethod.Online)
            throw new InvalidOperationException("Only online orders use checkout sessions.");
        StripeCheckoutSessionId = string.IsNullOrWhiteSpace(sessionId) ? throw new ArgumentException("Session id is required.") : sessionId.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkPaid()
    {
        PaymentStatus = OrderPaymentStatus.Paid;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkPaymentFailed()
    {
        PaymentStatus = OrderPaymentStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void TransitionTo(MarketplaceOrderStatus next)
    {
        var allowed = (Status, next) switch
        {
            (MarketplaceOrderStatus.Pending, MarketplaceOrderStatus.Accepted or MarketplaceOrderStatus.Rejected or MarketplaceOrderStatus.Cancelled) => true,
            (MarketplaceOrderStatus.Accepted, MarketplaceOrderStatus.Preparing or MarketplaceOrderStatus.Cancelled) => true,
            (MarketplaceOrderStatus.Preparing, MarketplaceOrderStatus.ReadyForPickup) => true,
            (MarketplaceOrderStatus.ReadyForPickup, MarketplaceOrderStatus.Completed) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"Cannot change order from {Status} to {next}.");

        Status = next;
        if (next == MarketplaceOrderStatus.Completed && PaymentMethod == OrderPaymentMethod.PayAtStore)
            PaymentStatus = OrderPaymentStatus.Paid;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class MarketplaceOrderItem
{
    private MarketplaceOrderItem() { }

    public MarketplaceOrderItem(Guid menuItemId, string name, decimal unitPrice, int quantity)
    {
        if (menuItemId == Guid.Empty) throw new ArgumentException("Menu item is required.", nameof(menuItemId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required.", nameof(name));
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));
        if (quantity <= 0 || quantity > 99) throw new ArgumentOutOfRangeException(nameof(quantity));
        Id = Guid.NewGuid();
        MenuItemId = menuItemId;
        Name = name.Trim();
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        Quantity = quantity;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid MenuItemId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal Subtotal => UnitPrice * Quantity;
}
