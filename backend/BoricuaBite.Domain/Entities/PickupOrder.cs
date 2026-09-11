using BoricuaBite.Domain.Common;

namespace BoricuaBite.Domain.Entities;

public enum PickupOrderStatus
{
    Pending,
    Accepted,
    Preparing,
    ReadyForPickup,
    PickedUp,
    Rejected,
    Cancelled
}

public sealed record OrderItem(Guid MenuItemId, string Name, decimal UnitPrice, int Quantity)
{
    public decimal Subtotal => UnitPrice * Quantity;
}

public sealed class PickupOrder : BaseEntity
{
    public Guid RestaurantId { get; }
    public Guid CustomerId { get; }
    public IReadOnlyList<OrderItem> Items { get; }
    public decimal Subtotal { get; }
    public string Currency => "USD";
    public PickupOrderStatus Status { get; private set; } = PickupOrderStatus.Pending;

    public PickupOrder(Restaurant restaurant, Guid customerId,
        IEnumerable<(MenuItem Item, int Quantity)> items)
    {
        ArgumentNullException.ThrowIfNull(restaurant);
        ArgumentNullException.ThrowIfNull(items);
        if (customerId == Guid.Empty)
            throw new ArgumentException("A customer account is required.", nameof(customerId));
        if (restaurant.OwnerId is null || !restaurant.IsActive || !restaurant.IsOpen || restaurant.IsDeleted)
            throw new InvalidOperationException("This restaurant is not accepting pickup orders.");

        var snapshots = new List<OrderItem>();
        foreach (var (item, quantity) in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(items), "Quantities must be positive.");
            if (item.RestaurantId != restaurant.Id)
                throw new InvalidOperationException("An order can only contain items from one restaurant.");
            if (!item.IsAvailable || item.IsDeleted)
                throw new InvalidOperationException("An ordered item is unavailable.");
            snapshots.Add(new OrderItem(item.Id, item.Name, item.Price, quantity));
        }
        if (snapshots.Count == 0)
            throw new ArgumentException("An order must contain at least one item.", nameof(items));

        RestaurantId = restaurant.Id;
        CustomerId = customerId;
        Items = snapshots.AsReadOnly();
        Subtotal = snapshots.Sum(item => item.Subtotal);
    }

    public void TransitionTo(PickupOrderStatus next)
    {
        var allowed = (Status, next) switch
        {
            (PickupOrderStatus.Pending, PickupOrderStatus.Accepted or PickupOrderStatus.Rejected or PickupOrderStatus.Cancelled) => true,
            (PickupOrderStatus.Accepted, PickupOrderStatus.Preparing or PickupOrderStatus.Cancelled) => true,
            (PickupOrderStatus.Preparing, PickupOrderStatus.ReadyForPickup) => true,
            (PickupOrderStatus.ReadyForPickup, PickupOrderStatus.PickedUp) => true,
            _ => false
        };
        if (!allowed)
            throw new InvalidOperationException($"Cannot change a pickup order from {Status} to {next}.");

        Status = next;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
