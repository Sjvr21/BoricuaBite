using BoricuaBite.Domain.Entities;

namespace BoricuaBite.Application.Subscriptions;

public sealed record SubscriptionCheckoutResult(string SessionId, string CheckoutUrl);

public interface ISubscriptionCheckoutProvider
{
    bool IsConfigured { get; }
    Task<SubscriptionCheckoutResult> CreateAsync(Restaurant restaurant, string ownerEmail, CancellationToken ct);
}

public interface IRestaurantSubscriptionService
{
    Task<SubscriptionCheckoutResult> CreateCheckoutAsync(Guid ownerId, Guid restaurantId, string ownerEmail, CancellationToken ct);
    Task<bool> ApplyStripeStateAsync(Guid restaurantId, string? stripeSubscriptionId, RestaurantSubscriptionStatus status, CancellationToken ct);
}
