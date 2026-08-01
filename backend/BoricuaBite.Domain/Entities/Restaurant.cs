using BoricuaBite.Domain.Common;

namespace BoricuaBite.Domain.Entities;

public class Restaurant : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string StateOrTerritory { get; set; } = "Puerto Rico";

    public string PostalCode { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public bool IsOpen { get; set; }

    public bool IsActive { get; set; } = true;
}