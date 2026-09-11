using BoricuaBite.Application.Catalog;
using BoricuaBite.Application.Common;
using BoricuaBite.Application.Menus;
using BoricuaBite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Infrastructure.Persistence;

public sealed class CatalogService(BoricuaBiteDbContext db) : ICatalogService
{
    private IQueryable<Restaurant> Published => db.Restaurants.AsNoTracking()
        .Where(x => x.IsActive && x.IsPublished && x.OwnerId != null &&
            x.SubscriptionStatus == RestaurantSubscriptionStatus.Active);

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
        var ranked = await query
            .Select(r => new
            {
                Restaurant = r,
                ReviewCount = db.RestaurantReviews.Count(review => review.RestaurantId == r.Id),
                AverageRating = db.RestaurantReviews.Where(review => review.RestaurantId == r.Id)
                    .Select(review => (double?)review.Rating).Average() ?? 0
            })
            .OrderByDescending(x => x.AverageRating)
            .ThenByDescending(x => x.ReviewCount)
            .ThenBy(x => x.Restaurant.Name)
            .ThenBy(x => x.Restaurant.Id)
            .Skip(page.Offset).Take(page.Size).ToListAsync(ct);

        var items = ranked.Select(x => ToResponse(x.Restaurant, x.AverageRating, x.ReviewCount)).ToArray();
        return new(items, page.Number, page.Size, total);
    }

    public async Task<PublicRestaurant?> GetAsync(Guid restaurantId, CancellationToken ct)
    {
        var restaurant = await Published.SingleOrDefaultAsync(x => x.Id == restaurantId, ct);
        if (restaurant is null) return null;
        var ratings = db.RestaurantReviews.Where(x => x.RestaurantId == restaurantId);
        var count = await ratings.CountAsync(ct);
        var average = count == 0 ? 0 : await ratings.AverageAsync(x => (double)x.Rating, ct);
        return ToResponse(restaurant, average, count);
    }

    public async Task<Page<MenuItemResponse>?> MenuAsync(Guid restaurantId, PageRequest page, CancellationToken ct)
    {
        if (!await Published.AnyAsync(x => x.Id == restaurantId, ct)) return null;
        var query = db.MenuItems.AsNoTracking().Where(x => x.RestaurantId == restaurantId &&
            Published.Any(r => r.Id == x.RestaurantId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.Size)
            .Select(x => new MenuItemResponse(x.Id, x.Name, x.Description, x.ImageUrl, x.Price, x.IsAvailable)).ToListAsync(ct);
        return new(items, page.Number, page.Size, total);
    }

    private static PublicRestaurant ToResponse(Restaurant restaurant, double averageRating, int reviewCount) => new(
        restaurant.Id, restaurant.Name, restaurant.Description, restaurant.PhoneNumber, restaurant.Address,
        restaurant.LogoUrl, restaurant.CoverImageUrl, restaurant.IsOpen, Math.Round(averageRating, 2), reviewCount);
}
