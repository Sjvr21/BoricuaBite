using System.Security.Claims;
using BoricuaBite.Application.Connect;
using BoricuaBite.Application.Subscriptions;

namespace BoricuaBite.Api.Endpoints;

public static class StripeOwnerEndpoints
{
    public static void MapStripeOwnerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/owner/restaurants/{restaurantId:guid}/stripe")
            .RequireAuthorization("ApiBearer")
            .WithTags("Stripe restaurant onboarding");

        group.MapGet("/status", async (Guid restaurantId, ClaimsPrincipal user,
            IRestaurantStripeService stripe, CancellationToken ct) =>
        {
            var result = await stripe.GetStatusAsync(OwnerId(user), restaurantId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/connect-session", async (Guid restaurantId, ClaimsPrincipal user,
            IRestaurantStripeService stripe, CancellationToken ct) =>
        {
            try
            {
                var email = Email(user);
                if (string.IsNullOrWhiteSpace(email))
                    return Results.BadRequest(new { error = "Your account needs an email address before Stripe onboarding." });
                return Results.Ok(await stripe.CreateOnboardingSessionAsync(OwnerId(user), restaurantId, email, ct));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/express-dashboard", async (Guid restaurantId, ClaimsPrincipal user,
            IRestaurantStripeService stripe, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await stripe.CreateExpressDashboardLinkAsync(OwnerId(user), restaurantId, ct));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/subscription-checkout", async (Guid restaurantId, ClaimsPrincipal user,
            IRestaurantSubscriptionService subscriptions, CancellationToken ct) =>
        {
            try
            {
                var email = Email(user);
                if (string.IsNullOrWhiteSpace(email))
                    return Results.BadRequest(new { error = "Your account needs an email address before starting billing." });
                return Results.Ok(await subscriptions.CreateCheckoutAsync(OwnerId(user), restaurantId, email, ct));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/subscription-portal", async (Guid restaurantId, ClaimsPrincipal user,
            IRestaurantSubscriptionService subscriptions, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await subscriptions.CreatePortalAsync(OwnerId(user), restaurantId, ct));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    private static Guid OwnerId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string? Email(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(ClaimTypes.Name);
}
