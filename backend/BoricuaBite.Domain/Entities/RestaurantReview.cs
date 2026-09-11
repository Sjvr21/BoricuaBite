using BoricuaBite.Domain.Common;

namespace BoricuaBite.Domain.Entities;

public sealed class RestaurantReview : BaseEntity
{
    private RestaurantReview() { }

    public RestaurantReview(Guid restaurantId, Guid customerId, Guid orderId, int rating, string? comment)
    {
        if (restaurantId == Guid.Empty) throw new ArgumentException("Restaurant is required.", nameof(restaurantId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer is required.", nameof(customerId));
        if (orderId == Guid.Empty) throw new ArgumentException("Order is required.", nameof(orderId));
        if (rating is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be from 1 to 5.");
        if (comment is not null && comment.Trim().Length > 2000) throw new ArgumentException("Review text must be 2000 characters or fewer.", nameof(comment));

        RestaurantId = restaurantId;
        CustomerId = customerId;
        OrderId = orderId;
        Rating = rating;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    public Guid RestaurantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid OrderId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
}
