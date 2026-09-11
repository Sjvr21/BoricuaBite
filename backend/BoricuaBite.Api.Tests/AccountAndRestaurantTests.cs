using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BoricuaBite.Application.Restaurants;
using BoricuaBite.Domain.Entities;

namespace BoricuaBite.Api.Tests;

public class AccountAndRestaurantTests
{
    private const string Password = "LocalKitchen!12345";

    private static RestaurantDetails Details(string name = "Local Kitchen") => new(
        name, "Puerto Rican food for pickup", "787-555-0123",
        new PickupAddress("123 Main Street", null, "Vega Baja", "Puerto Rico", "00693", "US"),
        null, null);

    private static async Task RegisterAsync(HttpClient client, string email) =>
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).StatusCode);

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
    }

    private static async Task<RestaurantResponse> AdminCreateAsync(ApiFactory factory, string ownerEmail)
    {
        using var admin = factory.CreateApiClient();
        await RegisterAsync(admin, "admin@example.com");
        await LoginAsync(admin, "admin@example.com");
        var response = await admin.PostAsJsonAsync("/api/admin/restaurants", new { ownerEmail, restaurant = Details() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RestaurantResponse>())!;
    }

    [Fact]
    public async Task CustomerAccount_CanRegisterLoginAndReadProfile()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await RegisterAsync(client, "customer@example.com");
        await LoginAsync(client, "customer@example.com");
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/account");
        Assert.Equal("customer@example.com", profile.GetProperty("email").GetString());
        Assert.False(profile.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task RestaurantOwners_CannotSelfCreateListings()
    {
        using var factory = new ApiFactory();
        using var owner = factory.CreateApiClient();
        await RegisterAsync(owner, "owner@example.com");
        await LoginAsync(owner, "owner@example.com");
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await owner.PostAsJsonAsync("/api/owner/restaurants", Details())).StatusCode);
    }

    [Fact]
    public async Task AdminCreatesPendingRestaurant_ThenActivatesSubscription()
    {
        using var factory = new ApiFactory();
        using var owner = factory.CreateApiClient();
        await RegisterAsync(owner, "owner@example.com");
        var restaurant = await AdminCreateAsync(factory, "owner@example.com");
        Assert.Equal(RestaurantSubscriptionStatus.Pending, restaurant.SubscriptionStatus);

        await LoginAsync(owner, "owner@example.com");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PutAsJsonAsync($"/api/owner/restaurants/{restaurant.Id}/availability", new { isOpen = true })).StatusCode);

        using var admin = factory.CreateApiClient();
        await LoginAsync(admin, "admin@example.com");
        var activated = await admin.PutAsJsonAsync($"/api/admin/restaurants/{restaurant.Id}/subscription", new
        {
            status = "Active",
            stripeConnectedAccountId = "acct_test_restaurant",
            stripeSubscriptionId = "sub_test_restaurant"
        });
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await owner.PutAsJsonAsync($"/api/owner/restaurants/{restaurant.Id}/availability", new { isOpen = true })).StatusCode);
        var owned = (await owner.GetFromJsonAsync<RestaurantResponse[]>($"/api/owner/restaurants"))!;
        Assert.True(Assert.Single(owned).IsOpen);
    }

    [Fact]
    public async Task AdminCanSuspendRestaurantAndOwnerCannotReopenIt()
    {
        using var factory = new ApiFactory();
        using var owner = factory.CreateApiClient();
        await RegisterAsync(owner, "owner@example.com");
        var restaurant = await AdminCreateAsync(factory, "owner@example.com");

        using var admin = factory.CreateApiClient();
        await LoginAsync(admin, "admin@example.com");
        await admin.PutAsJsonAsync($"/api/admin/restaurants/{restaurant.Id}/subscription", new { status = "Active" });
        await admin.PutAsJsonAsync($"/api/admin/restaurants/{restaurant.Id}/active", new { isActive = false });

        await LoginAsync(owner, "owner@example.com");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PutAsJsonAsync($"/api/owner/restaurants/{restaurant.Id}/availability", new { isOpen = true })).StatusCode);
    }
}
