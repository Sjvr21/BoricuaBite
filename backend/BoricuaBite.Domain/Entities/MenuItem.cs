using BoricuaBite.Domain.Common;

namespace BoricuaBite.Domain.Entities;

public sealed class MenuItem : BaseEntity
{
    public Guid RestaurantId { get; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public bool IsAvailable { get; set; } = true;

    public MenuItem(Guid restaurantId, string name, decimal price)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("A restaurant is required.", nameof(restaurantId));
        Validate(name, price);

        RestaurantId = restaurantId;
        Name = name.Trim();
        Price = price;
    }

    public void Update(string name, decimal price)
    {
        Validate(name, price);
        Name = name.Trim();
        Price = price;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void Validate(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            throw new ArgumentException("A menu item name must contain 1 to 150 characters.", nameof(name));
        if (price < 0 || price > 9999999999.99m || decimal.Round(price, 2) != price)
            throw new ArgumentOutOfRangeException(nameof(price), "Use a USD price from 0 to 9999999999.99 with at most two decimal places.");
    }
}
