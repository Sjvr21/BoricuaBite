using BoricuaBite.Application.Restaurants;
using BoricuaBite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Infrastructure.Persistence;

public sealed class RestaurantService(BoricuaBiteDbContext db) : IRestaurantService
{
    public async Task<RestaurantResponse> CreateAsync(Guid ownerId, RestaurantDetails details, CancellationToken cancellationToken)
    {
        var restaurant = new Restaurant();
        restaurant.AssignOwner(ownerId);
        ApplyDetails(restaurant, details);
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(restaurant);
    }

    public async Task<IReadOnlyList<RestaurantResponse>> ListOwnedAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var restaurants = await db.Restaurants.AsNoTracking().Where(x => x.OwnerId == ownerId)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return restaurants.Select(ToResponse).ToArray();
    }

    public async Task<RestaurantResponse?> GetOwnedAsync(Guid ownerId, Guid restaurantId, CancellationToken cancellationToken)
    {
        var restaurant = await db.Restaurants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == restaurantId && x.OwnerId == ownerId, cancellationToken);
        return restaurant is null ? null : ToResponse(restaurant);
    }

    public async Task<RestaurantResponse?> UpdateAsync(Guid ownerId, Guid restaurantId, RestaurantDetails details, CancellationToken cancellationToken)
    {
        var restaurant = await db.Restaurants
            .SingleOrDefaultAsync(x => x.Id == restaurantId && x.OwnerId == ownerId, cancellationToken);
        if (restaurant is null) return null;
        ApplyDetails(restaurant, details);
        restaurant.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(restaurant);
    }

    public async Task<RestaurantResponse?> SetAvailabilityAsync(Guid ownerId, Guid restaurantId, bool isOpen, CancellationToken cancellationToken)
    {
        var restaurant = await db.Restaurants
            .SingleOrDefaultAsync(x => x.Id == restaurantId && x.OwnerId == ownerId, cancellationToken);
        if (restaurant is null) return null;
        restaurant.IsOpen = isOpen;
        restaurant.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(restaurant);
    }

    private static void ApplyDetails(Restaurant restaurant, RestaurantDetails details)
    {
        restaurant.Name = details.Name.Trim();
        restaurant.Description = details.Description.Trim();
        restaurant.PhoneNumber = details.PhoneNumber.Trim();
        restaurant.Address = details.Address.ToDomain();
    }

    private static RestaurantResponse ToResponse(Restaurant restaurant) => new(
        restaurant.Id, restaurant.Name, restaurant.Description, restaurant.PhoneNumber,
        restaurant.Address, restaurant.IsOpen, restaurant.IsActive);
}
