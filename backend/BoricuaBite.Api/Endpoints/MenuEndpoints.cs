using System.Security.Claims;
using BoricuaBite.Application.Common;
using BoricuaBite.Application.Menus;

namespace BoricuaBite.Api.Endpoints;

public static class MenuEndpoints
{
    public static void MapMenuEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/owner/restaurants/{restaurantId:guid}/menu-items")
            .RequireAuthorization("ApiBearer").WithTags("Menu management");

        group.MapGet("/", async (Guid restaurantId, int? page, int? pageSize,
            ClaimsPrincipal user, IMenuService service, CancellationToken ct) =>
        {
            var errors = RequestValidation.PageErrors(page, pageSize);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await service.ListAsync(OwnerId(user), restaurantId, new(page ?? 1, pageSize ?? 20), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/{itemId:guid}", async (Guid restaurantId, Guid itemId,
            ClaimsPrincipal user, IMenuService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(OwnerId(user), restaurantId, itemId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async (Guid restaurantId, MenuItemDetails details,
            ClaimsPrincipal user, IMenuService service, CancellationToken ct) =>
        {
            var errors = RequestValidation.Errors(details);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await service.CreateAsync(OwnerId(user), restaurantId, details, ct);
            return result is null ? Results.NotFound() : Results.Created(
                $"/api/owner/restaurants/{restaurantId}/menu-items/{result.Id}", result);
        });

        group.MapPut("/{itemId:guid}", async (Guid restaurantId, Guid itemId, MenuItemDetails details,
            ClaimsPrincipal user, IMenuService service, CancellationToken ct) =>
        {
            var errors = RequestValidation.Errors(details);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await service.UpdateAsync(OwnerId(user), restaurantId, itemId, details, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPut("/{itemId:guid}/availability", async (Guid restaurantId, Guid itemId,
            MenuItemAvailability availability, ClaimsPrincipal user, IMenuService service, CancellationToken ct) =>
        {
            var errors = RequestValidation.Errors(availability);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await service.SetAvailabilityAsync(OwnerId(user), restaurantId, itemId,
                availability.IsAvailable!.Value, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapDelete("/{itemId:guid}", async (Guid restaurantId, Guid itemId,
            ClaimsPrincipal user, IMenuService service, CancellationToken ct) =>
            await service.DeleteAsync(OwnerId(user), restaurantId, itemId, ct)
                ? Results.NoContent() : Results.NotFound());
    }

    private static Guid OwnerId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
