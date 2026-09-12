using BoricuaBite.Application.Subscriptions;
using BoricuaBite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Infrastructure.Persistence;

public sealed class RestaurantSubscriptionService(
    BoricuaBiteDbContext db,
    ISubscriptionCheckoutProvider checkoutProvider) : IRestaurantSubscriptionService
{
    public async Task<SubscriptionCheckoutResult> CreateCheckoutAsync(Guid ownerId, Guid restaurantId, string ownerEmail, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.SingleOrDefaultAsync(
            x => x.Id == restaurantId && x.OwnerId == ownerId, ct)
            ?? throw new InvalidOperationException("Restaurant was not found.");

        if (restaurant.SubscriptionStatus == RestaurantSubscriptionStatus.Active)
            throw new InvalidOperationException("This restaurant already has an active subscription.");
        if (!string.IsNullOrWhiteSpace(restaurant.StripeSubscriptionId) &&
            restaurant.SubscriptionStatus != RestaurantSubscriptionStatus.Cancelled)
            throw new InvalidOperationException("This restaurant already has a Stripe subscription. Use the billing portal to manage it.");
        if (!checkoutProvider.IsConfigured)
            throw new InvalidOperationException("Restaurant subscription checkout is not configured yet.");

        return await checkoutProvider.CreateAsync(restaurant, ownerEmail, ct);
    }

    public async Task<SubscriptionPortalResult> CreatePortalAsync(Guid ownerId, Guid restaurantId, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.SingleOrDefaultAsync(
            x => x.Id == restaurantId && x.OwnerId == ownerId, ct)
            ?? throw new InvalidOperationException("Restaurant was not found.");
        return await checkoutProvider.CreatePortalAsync(restaurant, ct);
    }

    public async Task<bool> ApplyStripeStateAsync(Guid restaurantId, string? stripeSubscriptionId,
        RestaurantSubscriptionStatus status, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.SingleOrDefaultAsync(x => x.Id == restaurantId, ct);
        if (restaurant is null) return false;
        restaurant.StripeSubscriptionId = string.IsNullOrWhiteSpace(stripeSubscriptionId)
            ? restaurant.StripeSubscriptionId
            : stripeSubscriptionId.Trim();
        restaurant.SetSubscriptionStatus(status);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ApplyStripeStateBySubscriptionIdAsync(
        string stripeSubscriptionId, RestaurantSubscriptionStatus status, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stripeSubscriptionId)) return false;
        var restaurant = await db.Restaurants.SingleOrDefaultAsync(
            x => x.StripeSubscriptionId == stripeSubscriptionId, ct);
        if (restaurant is null) return false;
        restaurant.SetSubscriptionStatus(status);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
