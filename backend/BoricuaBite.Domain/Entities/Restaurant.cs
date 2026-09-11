using BoricuaBite.Domain.Common;
using BoricuaBite.Domain.ValueObjects;

namespace BoricuaBite.Domain.Entities;

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

    public bool IsOpen { get; set; }

    public bool IsActive { get; set; } = true;
}
