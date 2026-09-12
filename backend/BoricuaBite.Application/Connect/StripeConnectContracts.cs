namespace BoricuaBite.Application.Connect;

public sealed record RestaurantStripeStatus(
    string? ConnectedAccountId,
    bool TransfersActive,
    bool PayoutsActive,
    bool IsReady);

public sealed record RestaurantStripeSession(
    string ConnectedAccountId,
    string ClientSecret,
    string PublishableKey,
    bool TransfersActive,
    bool PayoutsActive);

public sealed record StripeRedirectResult(string Url);

public interface IStripeConnectProvider
{
    bool IsConfigured { get; }
    string PublishableKey { get; }
    Task<string> CreateRecipientAccountAsync(string ownerEmail, string displayName, CancellationToken ct);
    Task<string> CreateAccountSessionAsync(string connectedAccountId, CancellationToken ct);
    Task<string> CreateExpressLoginLinkAsync(string connectedAccountId, CancellationToken ct);
    Task<RestaurantStripeStatus> GetStatusAsync(string connectedAccountId, CancellationToken ct);
}

public interface IRestaurantStripeService
{
    Task<RestaurantStripeStatus?> GetStatusAsync(Guid ownerId, Guid restaurantId, CancellationToken ct);
    Task<RestaurantStripeSession> CreateOnboardingSessionAsync(Guid ownerId, Guid restaurantId, string ownerEmail, CancellationToken ct);
    Task<StripeRedirectResult> CreateExpressDashboardLinkAsync(Guid ownerId, Guid restaurantId, CancellationToken ct);
}
