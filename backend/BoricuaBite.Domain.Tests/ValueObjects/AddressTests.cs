using BoricuaBite.Domain.ValueObjects;

namespace BoricuaBite.Domain.Tests.ValueObjects;

public class AddressTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesAddress()
    {
        var address = new Address(
            "123 Main Street",
            "Apartment 2",
            "Vega Baja",
            "Puerto Rico",
            "00693",
            "United States");

        Assert.Equal("123 Main Street", address.AddressLine1);
        Assert.Equal("Apartment 2", address.AddressLine2);
        Assert.Equal("Vega Baja", address.City);
        Assert.Equal("Puerto Rico", address.StateOrTerritory);
        Assert.Equal("00693", address.PostalCode);
        Assert.Equal("United States", address.Country);
    }

    [Fact]
    public void Constructor_WithSurroundingWhitespace_TrimsValues()
    {
        var address = new Address(
            "  123 Main Street  ",
            "  Apartment 2  ",
            "  Vega Baja  ",
            "  Puerto Rico  ",
            "  00693  ",
            "  United States  ");

        Assert.Equal("123 Main Street", address.AddressLine1);
        Assert.Equal("Apartment 2", address.AddressLine2);
        Assert.Equal("Vega Baja", address.City);
        Assert.Equal("Puerto Rico", address.StateOrTerritory);
        Assert.Equal("00693", address.PostalCode);
        Assert.Equal("United States", address.Country);
    }

    [Fact]
    public void Constructor_WithBlankAddressLine2_SetsItToNull()
    {
        var address = new Address(
            "123 Main Street",
            "   ",
            "Vega Baja",
            "Puerto Rico",
            "00693",
            "United States");

        Assert.Null(address.AddressLine2);
    }

    [Fact]
    public void TwoAddresses_WithIdenticalValues_AreEqual()
    {
        var firstAddress = new Address(
            "123 Main Street",
            null,
            "Vega Baja",
            "Puerto Rico",
            "00693",
            "United States");

        var secondAddress = new Address(
            "123 Main Street",
            null,
            "Vega Baja",
            "Puerto Rico",
            "00693",
            "United States");

        Assert.Equal(firstAddress, secondAddress);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAddressLine1_ThrowsArgumentException(
        string addressLine1)
    {
        var action = () => new Address(
            addressLine1,
            null,
            "Vega Baja",
            "Puerto Rico",
            "00693",
            "United States");

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("addressLine1", exception.ParamName);
    }
}