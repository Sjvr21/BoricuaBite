using BoricuaBite.Domain.Entities;

namespace BoricuaBite.Application.Subscriptions;

public sealed record SubscriptionCheckoutResult(string SessionId, string CheckoutUrl);
public sealed record SubscriptionPortalResult(string PortalUrl);

public interface ISubscriptionCheckoutProvider
{
    bool IsConfigured { get; }
    Task<SubscriptionCheckoutResult> CreateAsync(Restaurant restaurant, string ownerEmail, CancellationToken ct);
    Task<SubscriptionPortalResult> CreatePortalAsync(Restaurant restaurant, CancellationToken ct);
}

public interface IRestaurantSubscriptionService
{
    Task<SubscriptionCheckoutResult> CreateCheckoutAsync(Guid ownerId, Guid restaurantId, string ownerEmail, CancellationToken ct);
    Task<SubscriptionPortalResult> CreatePortalAsync(Guid ownerId, Guid restaurantId, CancellationToken ct);
    Task<bool> ApplyStripeStateAsync(Guid restaurantId, string? stripeSubscriptionId, RestaurantSubscriptionStatus status, CancellationToken ct);
    Task<bool> ApplyStripeStateBySubscriptionIdAsync(string stripeSubscriptionId, RestaurantSubscriptionStatus status, CancellationToken ct);
}
