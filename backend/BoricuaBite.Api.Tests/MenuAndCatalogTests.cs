using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoricuaBite.Application.Catalog;
using BoricuaBite.Application.Common;
using BoricuaBite.Application.Menus;
using BoricuaBite.Application.Restaurants;
using BoricuaBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoricuaBite.Api.Tests;

public class MenuAndCatalogTests
{
    private static async Task<HttpClient> Owner(ApiFactory factory, string email = "owner@example.com")
    {
        var client = factory.CreateApiClient();
        var credentials = new { email, password = "TestOnly!Password123" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register", credentials)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false", credentials);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> Restaurant(HttpClient owner, string name = "Local Kitchen", string city = "Vega Baja")
    {
        var response = await owner.PostAsJsonAsync("/api/owner/restaurants", new RestaurantDetails(
            name, "Puerto Rican food", "787-555-0123", new PickupAddress("123 Main Street", null, city, "Puerto Rico", "00693", "US")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RestaurantResponse>())!.Id;
    }

    private static string Menu(Guid restaurantId) => $"/api/owner/restaurants/{restaurantId}/menu-items";

    private static async Task<MenuItemResponse> AddItem(HttpClient owner, Guid restaurantId, string name = "Mofongo")
    {
        var response = await owner.PostAsJsonAsync(Menu(restaurantId), new { name, price = 12.50m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MenuItemResponse>())!;
    }

    [Fact]
    public async Task Owner_CanCreateEditMarkUnavailableAndRemoveItem()
    {
        using var factory = new ApiFactory();
        using var owner = await Owner(factory);
        using var customer = factory.CreateApiClient();
        var restaurant = await Restaurant(owner);
        var item = await AddItem(owner, restaurant);
        var url = $"{Menu(restaurant)}/{item.Id}";
        Assert.Equal(item, await owner.GetFromJsonAsync<MenuItemResponse>(url));
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(url, new { name = "  Special Mofongo  ", price = 15.75m })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(url + "/availability", new { isAvailable = false })).StatusCode);
        var menu = (await customer.GetFromJsonAsync<Page<MenuItemResponse>>($"/api/restaurants/{restaurant}/menu"))!;
        Assert.Equal("Special Mofongo", Assert.Single(menu.Items).Name);
        Assert.Equal(15.75m, menu.Items[0].Price);
        Assert.False(menu.Items[0].IsAvailable);
        Assert.Equal("USD", menu.Items[0].Currency);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(url, new { name = "Restore", price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(url + "/availability", new { isAvailable = true })).StatusCode);
        menu = (await customer.GetFromJsonAsync<Page<MenuItemResponse>>($"/api/restaurants/{restaurant}/menu"))!;
        Assert.Empty(menu.Items);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BoricuaBiteDbContext>();
        Assert.True((await db.MenuItems.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task OtherOwner_CannotAccessOrModifyAnyMenuOperation()
    {
        using var factory = new ApiFactory();
        using var owner = await Owner(factory);
        using var stranger = await Owner(factory, "stranger@example.com");
        var restaurant = await Restaurant(owner);
        var item = await AddItem(owner, restaurant);
        var url = $"{Menu(restaurant)}/{item.Id}";
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(Menu(restaurant))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(Menu(restaurant), new { name = "Injected", price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync(url, new { name = "Changed", price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync(url + "/availability", new { isAvailable = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync(url)).StatusCode);
        Assert.Equal(item, await owner.GetFromJsonAsync<MenuItemResponse>(url));
    }

    [Fact]
    public async Task ItemId_CannotBeUsedUnderAnotherRestaurantEvenForSameOwner()
    {
        using var factory = new ApiFactory();
        using var owner = await Owner(factory);
        var first = await Restaurant(owner);
        var second = await Restaurant(owner, "Other Kitchen");
        var item = await AddItem(owner, first);
        var wrongUrl = $"{Menu(second)}/{item.Id}";
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(wrongUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(wrongUrl, new { name = "Changed", price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync(wrongUrl)).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":\"Mofongo\"}")]
    [InlineData("{\"name\":\"   \",\"price\":5}")]
    [InlineData("{\"name\":\"Mofongo\",\"price\":-1}")]
    [InlineData("{\"name\":\"Mofongo\",\"price\":1.001}")]
    [InlineData("{\"name\":\"Mofongo\",\"price\":10000000000}")]
    public async Task InvalidMenuData_IsRejectedOnCreateAndEdit(string json)
    {
        using var factory = new ApiFactory();
        using var owner = await Owner(factory);
        var restaurant = await Restaurant(owner);
        var item = await AddItem(owner, restaurant);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync(Menu(restaurant), new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode);
        var url = $"{Menu(restaurant)}/{item.Id}";
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(item, await owner.GetFromJsonAsync<MenuItemResponse>(url));
        Assert.Equal(1, (await owner.GetFromJsonAsync<Page<MenuItemResponse>>(Menu(restaurant)))!.TotalCount);
    }

    [Fact]
    public async Task MissingAvailability_IsRejectedAndAnonymousWritesRequireLogin()
    {
        using var factory = new ApiFactory();
        using var owner = await Owner(factory);
        using var anonymous = factory.CreateApiClient();
        var restaurant = await Restaurant(owner);
        var item = await AddItem(owner, restaurant);
        var url = $"{Menu(restaurant)}/{item.Id}";
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(url + "/availability", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Menu(restaurant))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Menu(restaurant), new { name = "Added", price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(url, new { name = "Changed", price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(url + "/availability", new { isAvailable = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("unowned")]
    public async Task UnpublishedRestaurants_AreHiddenFromAllPublicRoutes(string state)
    {
        using var factory = new ApiFactory();
        using var owner = await Owner(factory);
        using var customer = factory.CreateApiClient();
        var restaurantId = await Restaurant(owner);
        await AddItem(owner, restaurantId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BoricuaBiteDbContext>();
            var restaurant = await db.Restaurants.SingleAsync();
            if (state == "inactive") restaurant.IsActive = false;
            if (state == "deleted") restaurant.IsDeleted = true;
            if (state == "unowned") db.Entry(restaurant).Property(x => x.OwnerId).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        Assert.Empty((await customer.GetFromJsonAsync<Page<PublicRestaurant>>("/api/restaurants"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/restaurants/{restaurantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/restaurants/{restaurantId}/menu")).StatusCode);
        if (state == "deleted")
            Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(Menu(restaurantId), new { name = "New", price = 1 })).StatusCode);
    }

    [Fact]
    public async Task Catalog_SearchesFiltersAndPaginatesWithoutExposingOwnerInformation()
    {
        using var factory = new ApiFactory();
        using var owner = await Owner(factory);
        using var customer = factory.CreateApiClient();
        var first = await Restaurant(owner, "Alpha Kitchen");
        await Restaurant(owner, "Beta Kitchen", "San Juan");
        await Restaurant(owner, "Gamma Cafe");
        await owner.PutAsJsonAsync($"/api/owner/restaurants/{first}/availability", new { isOpen = true });
        var page = (await customer.GetFromJsonAsync<Page<PublicRestaurant>>("/api/restaurants?pageSize=1&page=2"))!;
        Assert.Equal(3, page.TotalCount);
        Assert.Equal("Beta Kitchen", Assert.Single(page.Items).Name);
        Assert.False(page.Items[0].IsOpen);
        var filtered = (await customer.GetFromJsonAsync<Page<PublicRestaurant>>("/api/restaurants?search=KITCHEN&city=vega%20baja&isOpen=true"))!;
        Assert.Equal(first, Assert.Single(filtered.Items).Id);
        Assert.Empty((await customer.GetFromJsonAsync<Page<PublicRestaurant>>("/api/restaurants?search=%27%20OR%201%3D1--"))!.Items);
        var raw = await customer.GetFromJsonAsync<JsonElement>($"/api/restaurants/{first}");
        Assert.False(raw.TryGetProperty("ownerId", out _));
        Assert.False(raw.TryGetProperty("email", out _));
        Assert.False(raw.TryGetProperty("isActive", out _));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=10001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task InvalidPagination_IsRejectedAcrossLists(string query)
    {
        using var factory = new ApiFactory();
        using var owner = await Owner(factory);
        var restaurant = await Restaurant(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/restaurants?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync($"/api/restaurants/{restaurant}/menu?{query}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(Menu(restaurant) + "?" + query)).StatusCode);
    }

    [Fact]
    public async Task Menu_PaginationIsStableAndMissingRestaurantIsNotAnEmptyMenu()
    {
        using var factory = new ApiFactory();
        using var owner = await Owner(factory);
        var restaurant = await Restaurant(owner);
        await AddItem(owner, restaurant, "Z Dish");
        await AddItem(owner, restaurant, "A Dish");
        var page = (await owner.GetFromJsonAsync<Page<MenuItemResponse>>($"/api/restaurants/{restaurant}/menu?pageSize=1&page=2"))!;
        Assert.Equal("Z Dish", Assert.Single(page.Items).Name);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/restaurants/{Guid.NewGuid()}/menu")).StatusCode);
    }
}
