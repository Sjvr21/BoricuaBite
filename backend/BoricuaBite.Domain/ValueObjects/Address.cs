namespace BoricuaBite.Domain.ValueObjects;

public sealed record Address
{
    public string AddressLine1 { get; }
    public string? AddressLine2 { get; }
    public string City { get; }
    public string StateOrTerritory { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(
        string addressLine1,
        string? addressLine2,
        string city,
        string stateOrTerritory,
        string postalCode,
        string country)
    {
        AddressLine1 = RequireValue(addressLine1, nameof(addressLine1));
        AddressLine2 = string.IsNullOrWhiteSpace(addressLine2)
            ? null
            : addressLine2.Trim();
        City = RequireValue(city, nameof(city));
        StateOrTerritory = RequireValue(stateOrTerritory, nameof(stateOrTerritory));
        PostalCode = RequireValue(postalCode, nameof(postalCode));
        Country = RequireValue(country, nameof(country));
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Address values cannot be empty.",
                parameterName);
        }

        return value.Trim();
    }
}