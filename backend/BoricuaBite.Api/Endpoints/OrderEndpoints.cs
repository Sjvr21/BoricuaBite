using System.Security.Claims;
using BoricuaBite.Application.Orders;
using BoricuaBite.Domain.Entities;

namespace BoricuaBite.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var customer = app.MapGroup("/api/orders").RequireAuthorization("ApiBearer").WithTags("Orders");

        customer.MapPost("/", async (CreateOrderRequest request, ClaimsPrincipal principal, IOrderService orders, CancellationToken ct) =>
        {
            try
            {
                var result = await orders.CreateAsync(UserId(principal), principal.FindFirstValue(ClaimTypes.Email), request, ct);
                return Results.Created($"/api/orders/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        customer.MapGet("/my", async (ClaimsPrincipal principal, IOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.ListCustomerAsync(UserId(principal), ct)));

        var owner = app.MapGroup("/api/owner/orders").RequireAuthorization("ApiBearer").WithTags("Owner orders");

        owner.MapGet("/", async (Guid? restaurantId, ClaimsPrincipal principal, IOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.ListOwnerAsync(UserId(principal), restaurantId, ct)));

        owner.MapPut("/{id:guid}/status", async (Guid id, OrderStatusUpdate request, ClaimsPrincipal principal, IOrderService orders, CancellationToken ct) =>
        {
            try
            {
                var result = await orders.UpdateOwnerStatusAsync(UserId(principal), id, request.Status, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
