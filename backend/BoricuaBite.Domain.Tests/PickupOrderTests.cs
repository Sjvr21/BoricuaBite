using BoricuaBite.Domain.Entities;

namespace BoricuaBite.Domain.Tests;

public class PickupOrderTests
{
    private static Restaurant OpenRestaurant()
    {
        var restaurant = new Restaurant { Name = "Local Kitchen", IsOpen = true };
        restaurant.AssignOwner(Guid.NewGuid());
        return restaurant;
    }

    private static PickupOrder PlaceOrder(Restaurant restaurant) =>
        new(restaurant, Guid.NewGuid(), [(new MenuItem(restaurant.Id, "Mofongo", 12.50m), 2)]);

    [Fact]
    public void Order_SavesLineItemsAndCalculatesSubtotal()
    {
        var restaurant = OpenRestaurant();
        var item = new MenuItem(restaurant.Id, "Mofongo", 12.50m);
        var cart = new List<(MenuItem, int)> { (item, 2) };
        var order = new PickupOrder(restaurant, Guid.NewGuid(), cart);
        cart.Clear();
        item.IsAvailable = false;

        Assert.Single(order.Items);
        Assert.Equal("Mofongo", order.Items[0].Name);
        Assert.Equal(25m, order.Subtotal);
        Assert.Equal(PickupOrderStatus.Pending, order.Status);
    }

    [Fact]
    public void Order_CompletesPickupSequence_AndCannotReopen()
    {
        var order = PlaceOrder(OpenRestaurant());
        order.TransitionTo(PickupOrderStatus.Accepted);
        order.TransitionTo(PickupOrderStatus.Preparing);
        order.TransitionTo(PickupOrderStatus.ReadyForPickup);
        order.TransitionTo(PickupOrderStatus.PickedUp);

        Assert.Equal(PickupOrderStatus.PickedUp, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(PickupOrderStatus.Accepted));
    }

    [Fact]
    public void Order_CannotSkipPreparation()
    {
        var order = PlaceOrder(OpenRestaurant());
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(PickupOrderStatus.ReadyForPickup));
    }

    [Theory]
    [InlineData(PickupOrderStatus.Rejected)]
    [InlineData(PickupOrderStatus.Cancelled)]
    public void ClosedOrder_CannotBeAccepted(PickupOrderStatus status)
    {
        var order = PlaceOrder(OpenRestaurant());
        order.TransitionTo(status);
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(PickupOrderStatus.Accepted));
    }

    [Fact]
    public void Order_RejectsItemsFromAnotherRestaurant()
    {
        var restaurant = OpenRestaurant();
        var item = new MenuItem(Guid.NewGuid(), "Mofongo", 12m);
        Assert.Throws<InvalidOperationException>(() => new PickupOrder(restaurant, Guid.NewGuid(), [(item, 1)]));
    }

    [Fact]
    public void Order_RejectsUnavailableItems()
    {
        var restaurant = OpenRestaurant();
        var item = new MenuItem(restaurant.Id, "Mofongo", 12m) { IsAvailable = false };
        Assert.Throws<InvalidOperationException>(() => new PickupOrder(restaurant, Guid.NewGuid(), [(item, 1)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Order_RejectsInvalidQuantities(int quantity)
    {
        var restaurant = OpenRestaurant();
        var item = new MenuItem(restaurant.Id, "Mofongo", 12m);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PickupOrder(restaurant, Guid.NewGuid(), [(item, quantity)]));
    }

    [Fact]
    public void Order_RejectsEmptyCart()
    {
        Assert.Throws<ArgumentException>(() => new PickupOrder(OpenRestaurant(), Guid.NewGuid(), []));
    }

    [Fact]
    public void Order_RejectsClosedRestaurant()
    {
        var restaurant = OpenRestaurant();
        restaurant.IsOpen = false;
        Assert.Throws<InvalidOperationException>(() => PlaceOrder(restaurant));
    }

    [Fact]
    public void Owner_MustHaveAnAccountIdentifier()
    {
        Assert.Throws<ArgumentException>(() => new Restaurant().AssignOwner(Guid.Empty));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.001)]
    public void MenuItem_RejectsInvalidPrices(double price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MenuItem(Guid.NewGuid(), "Mofongo", (decimal)price));
    }
}
