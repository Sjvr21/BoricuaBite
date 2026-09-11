using BoricuaBite.Domain.Common;

namespace BoricuaBite.Domain.Entities;

public sealed class MenuItem : BaseEntity
{
    public Guid RestaurantId { get; }
    public string Name { get; }
    public decimal Price { get; }
    public bool IsAvailable { get; set; } = true;

    public MenuItem(Guid restaurantId, string name, decimal price)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("A restaurant is required.", nameof(restaurantId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A menu item name is required.", nameof(name));
        if (price < 0 || decimal.Round(price, 2) != price)
            throw new ArgumentOutOfRangeException(nameof(price), "Use a nonnegative USD price with at most two decimal places.");

        RestaurantId = restaurantId;
        Name = name.Trim();
        Price = price;
    }
}
