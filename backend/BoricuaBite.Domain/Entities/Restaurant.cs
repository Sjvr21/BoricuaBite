using BoricuaBite.Domain.Common;
using BoricuaBite.Domain.ValueObjects;

namespace BoricuaBite.Domain.Entities;

public enum RestaurantSubscriptionStatus
{
    Pending,
    Active,
    PastDue,
    Cancelled
}

public class Restaurant : BaseEntity
{
    public Guid? OwnerId { get; private set; }

    public void AssignOwner(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("An owner account is required.", nameof(ownerId));

        OwnerId = ownerId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Address Address { get; set; } = null!;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? StripeConnectedAccountId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public RestaurantSubscriptionStatus SubscriptionStatus { get; set; } = RestaurantSubscriptionStatus.Pending;
    public bool IsOpen { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public void SetSubscriptionStatus(RestaurantSubscriptionStatus status)
    {
        SubscriptionStatus = status;
        if (status == RestaurantSubscriptionStatus.Cancelled)
        {
            IsOpen = false;
            IsPublished = false;
        }
        else if (status is RestaurantSubscriptionStatus.Active or RestaurantSubscriptionStatus.PastDue)
        {
            // PastDue is a grace state while Stripe Smart Retries are still running.
            // A terminal Stripe state maps to Cancelled and suspends marketplace access.
            IsPublished = true;
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
