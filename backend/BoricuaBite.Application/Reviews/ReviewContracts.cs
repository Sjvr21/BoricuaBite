using System.ComponentModel.DataAnnotations;
using BoricuaBite.Application.Common;

namespace BoricuaBite.Application.Reviews;

public sealed record CreateReviewRequest(
    [property: Required] Guid OrderId,
    [property: Range(1, 5)] int Rating,
    [property: StringLength(2000)] string? Comment);

public sealed record ReviewResponse(
    Guid Id,
    Guid RestaurantId,
    Guid OrderId,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc);

public sealed record ReviewSummary(double AverageRating, int ReviewCount);

public interface IReviewService
{
    Task<ReviewResponse> CreateAsync(Guid customerId, CreateReviewRequest request, CancellationToken ct);
    Task<Page<ReviewResponse>> ListAsync(Guid restaurantId, PageRequest page, CancellationToken ct);
    Task<ReviewSummary> SummaryAsync(Guid restaurantId, CancellationToken ct);
}
