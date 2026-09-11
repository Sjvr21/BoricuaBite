using System.ComponentModel.DataAnnotations;
using BoricuaBite.Application.Common;

namespace BoricuaBite.Application.Menus;

public sealed record MenuItemDetails(
    [property: Required, StringLength(150)] string Name,
    [property: Required] decimal? Price,
    [property: StringLength(1000)] string? Description = null,
    [property: StringLength(2048)] string? ImageUrl = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Price is decimal price && (price < 0 || price > 9999999999.99m || decimal.Round(price, 2) != price))
            yield return new ValidationResult("Use a USD price from 0 to 9999999999.99 with at most two decimal places.", [nameof(Price)]);
    }
}

public sealed record MenuItemAvailability([property: Required] bool? IsAvailable);
public sealed record MenuItemResponse(Guid Id, string Name, string Description, string? ImageUrl, decimal Price, bool IsAvailable)
{
    public string Currency => "USD";
}

public interface IMenuService
{
    Task<Page<MenuItemResponse>?> ListAsync(Guid ownerId, Guid restaurantId, PageRequest page, CancellationToken ct);
    Task<MenuItemResponse?> GetAsync(Guid ownerId, Guid restaurantId, Guid itemId, CancellationToken ct);
    Task<MenuItemResponse?> CreateAsync(Guid ownerId, Guid restaurantId, MenuItemDetails details, CancellationToken ct);
    Task<MenuItemResponse?> UpdateAsync(Guid ownerId, Guid restaurantId, Guid itemId, MenuItemDetails details, CancellationToken ct);
    Task<MenuItemResponse?> SetAvailabilityAsync(Guid ownerId, Guid restaurantId, Guid itemId, bool available, CancellationToken ct);
    Task<bool> DeleteAsync(Guid ownerId, Guid restaurantId, Guid itemId, CancellationToken ct);
}
