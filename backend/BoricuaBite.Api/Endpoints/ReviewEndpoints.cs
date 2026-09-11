using System.Security.Claims;
using BoricuaBite.Application.Common;
using BoricuaBite.Application.Reviews;

namespace BoricuaBite.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/restaurants/{restaurantId:guid}/reviews", async (
            Guid restaurantId, int page, int pageSize, IReviewService reviews, CancellationToken ct) =>
        {
            var pageRequest = PageRequest.Create(page, pageSize);
            if (pageRequest is null) return Results.BadRequest(new { error = "Invalid pagination." });
            return Results.Ok(await reviews.ListAsync(restaurantId, pageRequest, ct));
        }).WithTags("Reviews");

        app.MapGet("/api/restaurants/{restaurantId:guid}/reviews/summary", async (
            Guid restaurantId, IReviewService reviews, CancellationToken ct) =>
            Results.Ok(await reviews.SummaryAsync(restaurantId, ct))).WithTags("Reviews");

        app.MapPost("/api/reviews", async (CreateReviewRequest request, ClaimsPrincipal principal,
            IReviewService reviews, CancellationToken ct) =>
        {
            try
            {
                var result = await reviews.CreateAsync(UserId(principal), request, ct);
                return Results.Created($"/api/restaurants/{result.RestaurantId}/reviews", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization("ApiBearer").WithTags("Reviews");
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
