using BoricuaBite.Application.Catalog;
using BoricuaBite.Application.Common;

namespace BoricuaBite.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/restaurants").AllowAnonymous().WithTags("Restaurant browsing");

        group.MapGet("/", async (string? search, string? city, bool? isOpen, int? page, int? pageSize,
            ICatalogService service, CancellationToken ct) =>
        {
            var errors = RequestValidation.PageErrors(page, pageSize);
            if (search?.Length > 150) errors["search"] = ["Search must be at most 150 characters."];
            if (city?.Length > 100) errors["city"] = ["City must be at most 100 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await service.BrowseAsync(search, city, isOpen,
                new(page ?? 1, pageSize ?? 20), ct));
        });

        group.MapGet("/{restaurantId:guid}", async (Guid restaurantId, ICatalogService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(restaurantId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/{restaurantId:guid}/menu", async (Guid restaurantId, int? page, int? pageSize,
            ICatalogService service, CancellationToken ct) =>
        {
            var errors = RequestValidation.PageErrors(page, pageSize);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await service.MenuAsync(restaurantId, new(page ?? 1, pageSize ?? 20), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
    }
}
