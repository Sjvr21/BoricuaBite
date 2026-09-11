using BoricuaBite.Application.Restaurants;
using BoricuaBite.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace BoricuaBite.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization("PlatformAdmin")
            .WithTags("Platform admin");

        group.MapGet("/restaurants", async (IRestaurantService service, CancellationToken ct) =>
            Results.Ok(await service.ListAllForAdminAsync(ct)));

        group.MapPost("/restaurants", async (AdminCreateRestaurant request,
            UserManager<ApplicationUser> users, IRestaurantService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.OwnerEmail))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["ownerEmail"] = ["Owner email is required."] });

            var owner = await users.FindByEmailAsync(request.OwnerEmail.Trim());
            if (owner is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["ownerEmail"] = ["No account exists with that email."] });

            var errors = RestaurantEndpoints.Validate(request.Restaurant);
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var restaurant = await service.CreateAsync(owner.Id, request.Restaurant, ct);
            return Results.Created($"/api/owner/restaurants/{restaurant.Id}", restaurant);
        });

        group.MapPut("/restaurants/{id:guid}/active", async (Guid id, RestaurantActiveState state,
            IRestaurantService service, CancellationToken ct) =>
        {
            var restaurant = await service.SetActiveAsync(id, state.IsActive, ct);
            return restaurant is null ? Results.NotFound() : Results.Ok(restaurant);
        });
    }
}

public sealed record AdminCreateRestaurant(string OwnerEmail, RestaurantDetails Restaurant);
