using BoricuaBite.Domain.Common;

namespace BoricuaBite.Domain.Entities;

public sealed class MenuItem : BaseEntity
{
    public Guid RestaurantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public decimal Price { get; private set; }
    public bool IsAvailable { get; set; } = true;

    private MenuItem() { }

    public MenuItem(Guid restaurantId, string name, decimal price, string? description = null, string? imageUrl = null)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("A restaurant is required.", nameof(restaurantId));
        RestaurantId = restaurantId;
        Update(name, price, description, imageUrl);
    }

    public void Update(string name, decimal price, string? description = null, string? imageUrl = null)
    {
        Validate(name, price, description, imageUrl);
        Name = name.Trim();
        Price = price;
        Description = description?.Trim() ?? string.Empty;
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void Validate(string name, decimal price, string? description, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            throw new ArgumentException("A menu item name must contain 1 to 150 characters.", nameof(name));
        if (price < 0 || price > 9999999999.99m || decimal.Round(price, 2) != price)
            throw new ArgumentOutOfRangeException(nameof(price), "Use a USD price from 0 to 9999999999.99 with at most two decimal places.");
        if (description is not null && description.Trim().Length > 1000)
            throw new ArgumentException("Description must be 1000 characters or fewer.", nameof(description));
        if (imageUrl is not null && imageUrl.Trim().Length > 2048)
            throw new ArgumentException("Image URL must be 2048 characters or fewer.", nameof(imageUrl));
    }
}
