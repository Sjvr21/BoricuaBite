using BoricuaBite.Domain.Entities;

namespace BoricuaBite.Domain.Tests;

public class MenuItemTests
{
    [Fact]
    public void EditingMenuItem_DoesNotChangeExistingOrderPrices()
    {
        var restaurant = new Restaurant { IsOpen = true };
        restaurant.AssignOwner(Guid.NewGuid());
        var item = new MenuItem(restaurant.Id, "Mofongo", 12.50m);
        var order = new PickupOrder(restaurant, Guid.NewGuid(), [(item, 2)]);
        item.Update("Special Mofongo", 15m);
        Assert.Equal(25m, order.Subtotal);
        Assert.Equal("Mofongo", order.Items[0].Name);
        Assert.Equal(12.50m, order.Items[0].UnitPrice);
    }

    [Fact]
    public void InvalidUpdate_PreservesOriginalItem()
    {
        var item = new MenuItem(Guid.NewGuid(), "Mofongo", 12.50m);
        Assert.Throws<ArgumentOutOfRangeException>(() => item.Update("Changed", 10000000000m));
        Assert.Equal("Mofongo", item.Name);
        Assert.Equal(12.50m, item.Price);
    }
}
