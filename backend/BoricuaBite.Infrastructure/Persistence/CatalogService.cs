using BoricuaBite.Application.Catalog;
using BoricuaBite.Application.Common;
using BoricuaBite.Application.Menus;
using BoricuaBite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Infrastructure.Persistence;

public sealed class CatalogService(BoricuaBiteDbContext db) : ICatalogService
{
    private IQueryable<Restaurant> Published => db.Restaurants.AsNoTracking()
        .Where(x => x.IsActive && x.OwnerId != null);

    public async Task<Page<PublicRestaurant>> BrowseAsync(string? search, string? city, bool? isOpen, PageRequest page, CancellationToken ct)
    {
        var query = Published;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityName = city.Trim().ToLowerInvariant();
            query = query.Where(x => x.Address.City.ToLower() == cityName);
        }
        if (isOpen.HasValue) query = query.Where(x => x.IsOpen == isOpen.Value);
        var total = await query.CountAsync(ct);
        var restaurants = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.Size).ToListAsync(ct);
        return new(restaurants.Select(ToResponse).ToArray(), page.Number, page.Size, total);
    }

    public async Task<PublicRestaurant?> GetAsync(Guid restaurantId, CancellationToken ct)
    {
        var restaurant = await Published.SingleOrDefaultAsync(x => x.Id == restaurantId, ct);
        return restaurant is null ? null : ToResponse(restaurant);
    }

    public async Task<Page<MenuItemResponse>?> MenuAsync(Guid restaurantId, PageRequest page, CancellationToken ct)
    {
        if (!await Published.AnyAsync(x => x.Id == restaurantId, ct)) return null;
        var query = db.MenuItems.AsNoTracking().Where(x => x.RestaurantId == restaurantId &&
            Published.Any(r => r.Id == x.RestaurantId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.Size)
            .Select(x => new MenuItemResponse(x.Id, x.Name, x.Price, x.IsAvailable)).ToListAsync(ct);
        return new(items, page.Number, page.Size, total);
    }

    private static PublicRestaurant ToResponse(Restaurant restaurant) => new(restaurant.Id,
        restaurant.Name, restaurant.Description, restaurant.PhoneNumber, restaurant.Address,
        restaurant.LogoUrl, restaurant.IsOpen);
}
