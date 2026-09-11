using System.ComponentModel.DataAnnotations;
using BoricuaBite.Domain.Entities;
using BoricuaBite.Domain.ValueObjects;

namespace BoricuaBite.Application.Restaurants;

public sealed record RestaurantDetails(
    [property: Required, StringLength(150)] string Name,
    [property: Required, StringLength(2000)] string Description,
    [property: Required, StringLength(30)] string PhoneNumber,
    [property: Required] PickupAddress Address,
    [property: StringLength(2048)] string? LogoUrl = null,
    [property: StringLength(2048)] string? CoverImageUrl = null);

public sealed record PickupAddress(
    [property: Required, StringLength(200)] string AddressLine1,
    [property: StringLength(200)] string? AddressLine2,
    [property: Required, StringLength(100)] string City,
    [property: Required, StringLength(100)] string StateOrTerritory,
    [property: Required, StringLength(20)] string PostalCode,
    [property: Required, StringLength(100)] string Country)
{
    public Address ToDomain() => new(AddressLine1, AddressLine2, City,
        StateOrTerritory, PostalCode, Country);
}

public sealed record RestaurantResponse(Guid Id, string Name, string Description,
    string PhoneNumber, Address Address, string? LogoUrl, string? CoverImageUrl,
    bool IsOpen, bool IsPublished, bool IsActive, RestaurantSubscriptionStatus SubscriptionStatus);

public sealed record AdminRestaurantResponse(Guid Id, string Name, string Description,
    string PhoneNumber, Address Address, string? LogoUrl, string? CoverImageUrl,
    bool IsOpen, bool IsPublished, bool IsActive, RestaurantSubscriptionStatus SubscriptionStatus,
    string OwnerEmail, string? StripeConnectedAccountId, string? StripeSubscriptionId);

public sealed record RestaurantAvailability(bool IsOpen);
public sealed record RestaurantActiveState(bool IsActive);
public sealed record RestaurantSubscriptionUpdate(
    [property: Required] RestaurantSubscriptionStatus Status,
    [property: StringLength(255)] string? StripeConnectedAccountId,
    [property: StringLength(255)] string? StripeSubscriptionId);

public interface IRestaurantService
{
    Task<RestaurantResponse> CreateAsync(Guid ownerId, RestaurantDetails details, CancellationToken cancellationToken);
    Task<IReadOnlyList<RestaurantResponse>> ListOwnedAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRestaurantResponse>> ListAllForAdminAsync(CancellationToken cancellationToken);
    Task<RestaurantResponse?> GetOwnedAsync(Guid ownerId, Guid restaurantId, CancellationToken cancellationToken);
    Task<RestaurantResponse?> UpdateAsync(Guid ownerId, Guid restaurantId, RestaurantDetails details, CancellationToken cancellationToken);
    Task<RestaurantResponse?> SetAvailabilityAsync(Guid ownerId, Guid restaurantId, bool isOpen, CancellationToken cancellationToken);
    Task<AdminRestaurantResponse?> SetActiveAsync(Guid restaurantId, bool isActive, CancellationToken cancellationToken);
    Task<AdminRestaurantResponse?> SetSubscriptionAsync(Guid restaurantId, RestaurantSubscriptionUpdate update, CancellationToken cancellationToken);
}
