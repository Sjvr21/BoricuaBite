using BoricuaBite.Application.Common;
using BoricuaBite.Application.Menus;
using BoricuaBite.Domain.ValueObjects;

namespace BoricuaBite.Application.Catalog;

public sealed record PublicRestaurant(Guid Id, string Name, string Description,
    string PhoneNumber, Address Address, string? LogoUrl, string? CoverImageUrl,
    bool IsOpen, double AverageRating, int ReviewCount);

public interface ICatalogService
{
    Task<Page<PublicRestaurant>> BrowseAsync(string? search, string? city, bool? isOpen, PageRequest page, CancellationToken ct);
    Task<PublicRestaurant?> GetAsync(Guid restaurantId, CancellationToken ct);
    Task<Page<MenuItemResponse>?> MenuAsync(Guid restaurantId, PageRequest page, CancellationToken ct);
}
