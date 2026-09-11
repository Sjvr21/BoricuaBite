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

    public async Task<IReadOnlyList<AdminRestaurantResponse>> ListAllForAdminAsync(CancellationToken cancellationToken)
    {
        var restaurants = await db.Restaurants.AsNoTracking()
            .Join(db.Users.AsNoTracking(), r => r.OwnerId, u => (Guid?)u.Id, (r, u) => new { Restaurant = r, OwnerEmail = u.Email })
            .OrderBy(x => x.Restaurant.Name)
            .ToListAsync(cancellationToken);

        return restaurants.Select(x => ToAdminResponse(x.Restaurant, x.OwnerEmail ?? string.Empty)).ToArray();
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
        if (isOpen && (!restaurant.IsActive || !restaurant.IsPublished || restaurant.SubscriptionStatus != RestaurantSubscriptionStatus.Active))
            throw new InvalidOperationException("The restaurant must be active, published, and subscribed before it can accept orders.");
        restaurant.IsOpen = isOpen;
        restaurant.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(restaurant);
    }

    public async Task<AdminRestaurantResponse?> SetActiveAsync(Guid restaurantId, bool isActive, CancellationToken cancellationToken)
    {
        var restaurant = await db.Restaurants.SingleOrDefaultAsync(x => x.Id == restaurantId, cancellationToken);
        if (restaurant is null || restaurant.OwnerId is null) return null;

        restaurant.IsActive = isActive;
        if (!isActive) restaurant.IsOpen = false;
        restaurant.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await AdminResponseAsync(restaurant, cancellationToken);
    }

    public async Task<AdminRestaurantResponse?> SetSubscriptionAsync(Guid restaurantId, RestaurantSubscriptionUpdate update, CancellationToken cancellationToken)
    {
        var restaurant = await db.Restaurants.SingleOrDefaultAsync(x => x.Id == restaurantId, cancellationToken);
        if (restaurant is null || restaurant.OwnerId is null) return null;

        restaurant.StripeConnectedAccountId = string.IsNullOrWhiteSpace(update.StripeConnectedAccountId) ? null : update.StripeConnectedAccountId.Trim();
        restaurant.StripeSubscriptionId = string.IsNullOrWhiteSpace(update.StripeSubscriptionId) ? null : update.StripeSubscriptionId.Trim();
        restaurant.SetSubscriptionStatus(update.Status);
        await db.SaveChangesAsync(cancellationToken);
        return await AdminResponseAsync(restaurant, cancellationToken);
    }

    private async Task<AdminRestaurantResponse> AdminResponseAsync(Restaurant restaurant, CancellationToken cancellationToken)
    {
        var ownerEmail = await db.Users.AsNoTracking()
            .Where(x => x.Id == restaurant.OwnerId!.Value)
            .Select(x => x.Email)
            .SingleAsync(cancellationToken);
        return ToAdminResponse(restaurant, ownerEmail ?? string.Empty);
    }

    private static void ApplyDetails(Restaurant restaurant, RestaurantDetails details)
    {
        restaurant.Name = details.Name.Trim();
        restaurant.Description = details.Description.Trim();
        restaurant.PhoneNumber = details.PhoneNumber.Trim();
        restaurant.Address = details.Address.ToDomain();
        restaurant.LogoUrl = string.IsNullOrWhiteSpace(details.LogoUrl) ? null : details.LogoUrl.Trim();
        restaurant.CoverImageUrl = string.IsNullOrWhiteSpace(details.CoverImageUrl) ? null : details.CoverImageUrl.Trim();
    }

    private static RestaurantResponse ToResponse(Restaurant restaurant) => new(
        restaurant.Id, restaurant.Name, restaurant.Description, restaurant.PhoneNumber,
        restaurant.Address, restaurant.LogoUrl, restaurant.CoverImageUrl, restaurant.IsOpen,
        restaurant.IsPublished, restaurant.IsActive, restaurant.SubscriptionStatus);

    private static AdminRestaurantResponse ToAdminResponse(Restaurant restaurant, string ownerEmail) => new(
        restaurant.Id, restaurant.Name, restaurant.Description, restaurant.PhoneNumber,
        restaurant.Address, restaurant.LogoUrl, restaurant.CoverImageUrl, restaurant.IsOpen,
        restaurant.IsPublished, restaurant.IsActive, restaurant.SubscriptionStatus, ownerEmail,
        restaurant.StripeConnectedAccountId, restaurant.StripeSubscriptionId);
}
