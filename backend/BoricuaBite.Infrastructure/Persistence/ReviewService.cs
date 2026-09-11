using BoricuaBite.Application.Common;
using BoricuaBite.Application.Reviews;
using BoricuaBite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Infrastructure.Persistence;

public sealed class ReviewService(BoricuaBiteDbContext db) : IReviewService
{
    public async Task<ReviewResponse> CreateAsync(Guid customerId, CreateReviewRequest request, CancellationToken ct)
    {
        var order = await db.MarketplaceOrders.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == request.OrderId && x.CustomerId == customerId, ct)
            ?? throw new InvalidOperationException("Completed order was not found.");

        if (order.Status != MarketplaceOrderStatus.Completed)
            throw new InvalidOperationException("You can review a restaurant only after the order is completed.");
        if (await db.RestaurantReviews.AnyAsync(x => x.OrderId == order.Id, ct))
            throw new InvalidOperationException("This order has already been reviewed.");

        var review = new RestaurantReview(order.RestaurantId, customerId, order.Id, request.Rating, request.Comment);
        db.RestaurantReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return ToResponse(review);
    }

    public async Task<Page<ReviewResponse>> ListAsync(Guid restaurantId, PageRequest page, CancellationToken ct)
    {
        var query = db.RestaurantReviews.AsNoTracking().Where(x => x.RestaurantId == restaurantId);
        var total = await query.CountAsync(ct);
        var reviews = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip(page.Offset).Take(page.Size).ToListAsync(ct);
        return new(reviews.Select(ToResponse).ToArray(), page.Number, page.Size, total);
    }

    public async Task<ReviewSummary> SummaryAsync(Guid restaurantId, CancellationToken ct)
    {
        var query = db.RestaurantReviews.AsNoTracking().Where(x => x.RestaurantId == restaurantId);
        var count = await query.CountAsync(ct);
        if (count == 0) return new(0, 0);
        var average = await query.AverageAsync(x => x.Rating, ct);
        return new(Math.Round(average, 2), count);
    }

    private static ReviewResponse ToResponse(RestaurantReview review) => new(
        review.Id, review.RestaurantId, review.OrderId, review.Rating, review.Comment, review.CreatedAtUtc);
}
