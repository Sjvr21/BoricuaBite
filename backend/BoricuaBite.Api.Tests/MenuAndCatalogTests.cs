using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BoricuaBite.Application.Catalog;
using BoricuaBite.Application.Common;
using BoricuaBite.Application.Menus;
using BoricuaBite.Application.Orders;
using BoricuaBite.Application.Restaurants;
using BoricuaBite.Application.Reviews;
using BoricuaBite.Domain.Entities;

namespace BoricuaBite.Api.Tests;

public class MenuAndCatalogTests
{
    private const string Password = "TestOnly!Password123";

    private static async Task RegisterAndLoginAsync(HttpClient client, string email)
    {
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static RestaurantDetails Details(string name = "Local Kitchen", string city = "Vega Baja") => new(
        name, "Puerto Rican food", "787-555-0123",
        new PickupAddress("123 Main Street", null, city, "Puerto Rico", "00693", "US"));

    private static async Task<RestaurantResponse> CreateActiveRestaurantAsync(ApiFactory factory, HttpClient owner,
        string ownerEmail, string name = "Local Kitchen", string city = "Vega Baja")
    {
        using var admin = factory.CreateApiClient();
        await RegisterAndLoginAsync(admin, "admin@example.com");
        var created = await admin.PostAsJsonAsync("/api/admin/restaurants", new
        {
            ownerEmail,
            restaurant = Details(name, city)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var restaurant = (await created.Content.ReadFromJsonAsync<RestaurantResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/restaurants/{restaurant.Id}/subscription", new
        {
            status = "Active",
            stripeConnectedAccountId = "acct_test",
            stripeSubscriptionId = "sub_test"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PutAsJsonAsync($"/api/owner/restaurants/{restaurant.Id}/availability", new { isOpen = true })).StatusCode);
        return restaurant;
    }

    private static async Task<MenuItemResponse> AddItemAsync(HttpClient owner, Guid restaurantId,
        string name = "Mofongo", decimal price = 10m)
    {
        var response = await owner.PostAsJsonAsync($"/api/owner/restaurants/{restaurantId}/menu-items", new
        {
            name,
            price,
            description = "Fresh local favorite",
            imageUrl = "/uploads/test.jpg"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MenuItemResponse>())!;
    }

    [Fact]
    public async Task Menu_PersistsDescriptionAndImage_AndIsPublicForSubscribedRestaurant()
    {
        using var factory = new ApiFactory();
        using var owner = factory.CreateApiClient();
        await RegisterAndLoginAsync(owner, "owner@example.com");
        var restaurant = await CreateActiveRestaurantAsync(factory, owner, "owner@example.com");
        var item = await AddItemAsync(owner, restaurant.Id);

        using var customer = factory.CreateApiClient();
        var menu = (await customer.GetFromJsonAsync<Page<MenuItemResponse>>($"/api/restaurants/{restaurant.Id}/menu"))!;
        var publicItem = Assert.Single(menu.Items);
        Assert.Equal(item.Id, publicItem.Id);
        Assert.Equal("Fresh local favorite", publicItem.Description);
        Assert.Equal("/uploads/test.jpg", publicItem.ImageUrl);
    }

    [Fact]
    public async Task PayAtStoreOrder_UsesServerPricingAndCompletesThroughOwnerWorkflow()
    {
        using var factory = new ApiFactory();
        using var owner = factory.CreateApiClient();
        await RegisterAndLoginAsync(owner, "owner@example.com");
        var restaurant = await CreateActiveRestaurantAsync(factory, owner, "owner@example.com");
        var item = await AddItemAsync(owner, restaurant.Id, price: 10m);

        using var customer = factory.CreateApiClient();
        await RegisterAndLoginAsync(customer, "customer@example.com");
        var created = await customer.PostAsJsonAsync("/api/orders", new
        {
            restaurantId = restaurant.Id,
            items = new[] { new { menuItemId = item.Id, quantity = 2 } },
            paymentMethod = "PayAtStore"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = (await created.Content.ReadFromJsonAsync<OrderResponse>())!;
        Assert.Equal(20m, order.Subtotal);
        Assert.Equal(2m, order.TaxAmount);
        Assert.Equal(2m, order.ServiceFee);
        Assert.Equal(2m, order.CommissionAmount);
        Assert.Equal(24m, order.Total);
        Assert.Equal(20m, order.EstimatedRestaurantProceeds);
        Assert.Equal(OrderPaymentStatus.Pending, order.PaymentStatus);

        foreach (var status in new[] { "Accepted", "Preparing", "ReadyForPickup", "Completed" })
        {
            var response = await owner.PutAsJsonAsync($"/api/owner/orders/{order.Id}/status", new { status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var history = (await customer.GetFromJsonAsync<OrderResponse[]>("/api/orders/my"))!;
        var completed = Assert.Single(history);
        Assert.Equal(MarketplaceOrderStatus.Completed, completed.Status);
        Assert.Equal(OrderPaymentStatus.Paid, completed.PaymentStatus);
    }

    [Fact]
    public async Task ReviewRequiresCompletedOwnedOrder_AndOnlyOneReviewPerOrder()
    {
        using var factory = new ApiFactory();
        using var owner = factory.CreateApiClient();
        await RegisterAndLoginAsync(owner, "owner@example.com");
        var restaurant = await CreateActiveRestaurantAsync(factory, owner, "owner@example.com");
        var item = await AddItemAsync(owner, restaurant.Id);

        using var customer = factory.CreateApiClient();
        await RegisterAndLoginAsync(customer, "customer@example.com");
        var created = await customer.PostAsJsonAsync("/api/orders", new
        {
            restaurantId = restaurant.Id,
            items = new[] { new { menuItemId = item.Id, quantity = 1 } },
            paymentMethod = "PayAtStore"
        });
        var order = (await created.Content.ReadFromJsonAsync<OrderResponse>())!;

        Assert.Equal(HttpStatusCode.BadRequest,
            (await customer.PostAsJsonAsync("/api/reviews", new { orderId = order.Id, rating = 5, comment = "Great" })).StatusCode);

        foreach (var status in new[] { "Accepted", "Preparing", "ReadyForPickup", "Completed" })
            (await owner.PutAsJsonAsync($"/api/owner/orders/{order.Id}/status", new { status })).EnsureSuccessStatusCode();

        var reviewResponse = await customer.PostAsJsonAsync("/api/reviews", new { orderId = order.Id, rating = 5, comment = "Excellent food" });
        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await customer.PostAsJsonAsync("/api/reviews", new { orderId = order.Id, rating = 4, comment = "Again" })).StatusCode);

        var summary = await customer.GetFromJsonAsync<ReviewSummary>($"/api/restaurants/{restaurant.Id}/reviews/summary");
        Assert.Equal(5, summary!.AverageRating);
        Assert.Equal(1, summary.ReviewCount);
    }

    [Fact]
    public async Task Catalog_HidesPendingSubscriptions_AndRanksHigherRatedRestaurantsFirst()
    {
        using var factory = new ApiFactory();
        using var firstOwner = factory.CreateApiClient();
        await RegisterAndLoginAsync(firstOwner, "first@example.com");
        var first = await CreateActiveRestaurantAsync(factory, firstOwner, "first@example.com", "Five Star Kitchen");
        var firstItem = await AddItemAsync(firstOwner, first.Id);

        using var secondOwner = factory.CreateApiClient();
        await RegisterAndLoginAsync(secondOwner, "second@example.com");
        using var admin = factory.CreateApiClient();
        await LoginAsync(admin, "admin@example.com");
        var pendingResponse = await admin.PostAsJsonAsync("/api/admin/restaurants", new
        {
            ownerEmail = "second@example.com",
            restaurant = Details("Pending Kitchen", "San Juan")
        });
        Assert.Equal(HttpStatusCode.Created, pendingResponse.StatusCode);

        using var customer = factory.CreateApiClient();
        await RegisterAndLoginAsync(customer, "customer@example.com");
        var orderResponse = await customer.PostAsJsonAsync("/api/orders", new
        {
            restaurantId = first.Id,
            items = new[] { new { menuItemId = firstItem.Id, quantity = 1 } },
            paymentMethod = "PayAtStore"
        });
        var order = (await orderResponse.Content.ReadFromJsonAsync<OrderResponse>())!;
        foreach (var status in new[] { "Accepted", "Preparing", "ReadyForPickup", "Completed" })
            (await firstOwner.PutAsJsonAsync($"/api/owner/orders/{order.Id}/status", new { status })).EnsureSuccessStatusCode();
        (await customer.PostAsJsonAsync("/api/reviews", new { orderId = order.Id, rating = 5, comment = "Best" })).EnsureSuccessStatusCode();

        var page = (await customer.GetFromJsonAsync<Page<PublicRestaurant>>("/api/restaurants"))!;
        var visible = Assert.Single(page.Items);
        Assert.Equal("Five Star Kitchen", visible.Name);
        Assert.Equal(5, visible.AverageRating);
        Assert.Equal(1, visible.ReviewCount);
    }
}
