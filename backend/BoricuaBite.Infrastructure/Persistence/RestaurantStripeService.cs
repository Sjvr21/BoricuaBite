using BoricuaBite.Application.Connect;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Infrastructure.Persistence;

public sealed class RestaurantStripeService(
    BoricuaBiteDbContext db,
    IStripeConnectProvider stripe) : IRestaurantStripeService
{
    public async Task<RestaurantStripeStatus?> GetStatusAsync(Guid ownerId, Guid restaurantId, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == restaurantId && x.OwnerId == ownerId, ct);
        if (restaurant is null) return null;
        if (string.IsNullOrWhiteSpace(restaurant.StripeConnectedAccountId))
            return new RestaurantStripeStatus(null, false, false, false);
        return await stripe.GetStatusAsync(restaurant.StripeConnectedAccountId, ct);
    }

    public async Task<RestaurantStripeSession> CreateOnboardingSessionAsync(
        Guid ownerId, Guid restaurantId, string ownerEmail, CancellationToken ct)
    {
        if (!stripe.IsConfigured)
            throw new InvalidOperationException("Stripe Connect is not configured.");

        var restaurant = await db.Restaurants
            .SingleOrDefaultAsync(x => x.Id == restaurantId && x.OwnerId == ownerId, ct)
            ?? throw new InvalidOperationException("Restaurant was not found.");

        if (string.IsNullOrWhiteSpace(restaurant.StripeConnectedAccountId))
        {
            restaurant.StripeConnectedAccountId = await stripe.CreateRecipientAccountAsync(ownerEmail, restaurant.Name, ct);
            restaurant.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var status = await stripe.GetStatusAsync(restaurant.StripeConnectedAccountId, ct);
        var clientSecret = await stripe.CreateAccountSessionAsync(restaurant.StripeConnectedAccountId, ct);
        return new RestaurantStripeSession(
            restaurant.StripeConnectedAccountId,
            clientSecret,
            stripe.PublishableKey,
            status.TransfersActive,
            status.PayoutsActive);
    }

    public async Task<StripeRedirectResult> CreateExpressDashboardLinkAsync(
        Guid ownerId, Guid restaurantId, CancellationToken ct)
    {
        var connectedAccountId = await db.Restaurants.AsNoTracking()
            .Where(x => x.Id == restaurantId && x.OwnerId == ownerId)
            .Select(x => x.StripeConnectedAccountId)
            .SingleOrDefaultAsync(ct);

        if (connectedAccountId is null)
            throw new InvalidOperationException("Complete Stripe onboarding first.");

        var url = await stripe.CreateExpressLoginLinkAsync(connectedAccountId, ct);
        return new StripeRedirectResult(url);
    }
}
