using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BoricuaBite.Application.Restaurants;

namespace BoricuaBite.Api.Endpoints;

public static class RestaurantEndpoints
{
    public static void MapRestaurantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/owner/restaurants").RequireAuthorization("ApiBearer")
            .WithTags("Restaurant management");

        group.MapGet("/", async (ClaimsPrincipal user, IRestaurantService service, CancellationToken ct) =>
            Results.Ok(await service.ListOwnedAsync(OwnerId(user), ct)));

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IRestaurantService service, CancellationToken ct) =>
        {
            var restaurant = await service.GetOwnedAsync(OwnerId(user), id, ct);
            return restaurant is null ? Results.NotFound() : Results.Ok(restaurant);
        });

        group.MapPut("/{id:guid}", async (Guid id, RestaurantDetails details, ClaimsPrincipal user, IRestaurantService service, CancellationToken ct) =>
        {
            var errors = Validate(details);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var restaurant = await service.UpdateAsync(OwnerId(user), id, details, ct);
            return restaurant is null ? Results.NotFound() : Results.Ok(restaurant);
        });

        group.MapPut("/{id:guid}/availability", async (Guid id, RestaurantAvailability availability,
            ClaimsPrincipal user, IRestaurantService service, CancellationToken ct) =>
        {
            try
            {
                var restaurant = await service.SetAvailabilityAsync(OwnerId(user), id, availability.IsOpen, ct);
                return restaurant is null ? Results.NotFound() : Results.Ok(restaurant);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    private static Guid OwnerId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    internal static Dictionary<string, string[]> Validate(RestaurantDetails details)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateObject(details, "", errors);
        if (details.Address is not null) ValidateObject(details.Address, "address.", errors);
        return errors;
    }

    private static void ValidateObject(object value, string prefix, Dictionary<string, string[]> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        foreach (var result in results)
            foreach (var member in result.MemberNames)
                errors[prefix + member] = [result.ErrorMessage!];
    }
}
