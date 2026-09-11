using System.ComponentModel.DataAnnotations;
using BoricuaBite.Domain.ValueObjects;

namespace BoricuaBite.Application.Restaurants;

public sealed record RestaurantDetails(
    [property: Required, StringLength(150)] string Name,
    [property: Required, StringLength(2000)] string Description,
    [property: Required, StringLength(30)] string PhoneNumber,
    [property: Required] PickupAddress Address);

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
    string PhoneNumber, Address Address, bool IsOpen, bool IsActive);

public sealed record AdminRestaurantResponse(Guid Id, string Name, string Description,
    string PhoneNumber, Address Address, bool IsOpen, bool IsActive, string OwnerEmail);

public sealed record RestaurantAvailability(bool IsOpen);
public sealed record RestaurantActiveState(bool IsActive);

public interface IRestaurantService
{
    Task<RestaurantResponse> CreateAsync(Guid ownerId, RestaurantDetails details, CancellationToken cancellationToken);
    Task<IReadOnlyList<RestaurantResponse>> ListOwnedAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRestaurantResponse>> ListAllForAdminAsync(CancellationToken cancellationToken);
    Task<RestaurantResponse?> GetOwnedAsync(Guid ownerId, Guid restaurantId, CancellationToken cancellationToken);
    Task<RestaurantResponse?> UpdateAsync(Guid ownerId, Guid restaurantId, RestaurantDetails details, CancellationToken cancellationToken);
    Task<RestaurantResponse?> SetAvailabilityAsync(Guid ownerId, Guid restaurantId, bool isOpen, CancellationToken cancellationToken);
    Task<AdminRestaurantResponse?> SetActiveAsync(Guid restaurantId, bool isActive, CancellationToken cancellationToken);
}
