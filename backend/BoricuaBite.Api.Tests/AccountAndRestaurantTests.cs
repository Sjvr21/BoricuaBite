using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BoricuaBite.Application.Restaurants;
using BoricuaBite.Domain.Entities;
using BoricuaBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoricuaBite.Api.Tests;

public class AccountAndRestaurantTests
{
    private const string Password = "LocalKitchen!12345";
    private const string Restaurants = "/api/owner/restaurants";

    private static RestaurantDetails Details(string name = "Local Kitchen") => new(
        name, "Puerto Rican food for pickup", "787-555-0123",
        new PickupAddress("123 Main Street", null, "Vega Baja", "Puerto Rico", "00693", "US"));

    private static async Task<JsonElement> RegisterAndLogin(HttpClient client, string email)
    {
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        return tokens;
    }

    [Fact]
    public async Task Account_CanRegisterLoginRefreshAndReadProfile()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var tokens = await RegisterAndLogin(client, "owner@example.com");
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/account");
        Assert.Equal("owner@example.com", profile.GetProperty("email").GetString());
        Assert.True(Guid.TryParse(profile.GetProperty("id").GetString(), out _));
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<BoricuaBiteDbContext>().Users.SingleAsync();
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual(Password, user.PasswordHash);
    }

    [Fact]
    public async Task Registration_RejectsWeakPasswordsAndDuplicateEmails()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register",
            new { email = "owner@example.com", password = "weak" })).StatusCode);
        await RegisterAndLogin(client, "owner@example.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register",
            new { email = "OWNER@example.com", password = Password })).StatusCode);
    }

    [Fact]
    public async Task Login_LocksAccountAfterRepeatedWrongPasswords()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await RegisterAndLogin(client, "owner@example.com");
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new { email = "owner@example.com", password = "WrongPassword!123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new { email = "owner@example.com", password = Password })).StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_RejectAnonymousAndForgedTokens()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Restaurants)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Restaurants, Details())).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "forged-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Restaurants)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "forged" })).StatusCode);
    }

    [Fact]
    public async Task Owner_CanCreateReloadEditAndOpenRestaurant()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await RegisterAndLogin(client, "owner@example.com");
        var created = await client.PostAsJsonAsync(Restaurants, Details("  Local Kitchen  "));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var restaurant = (await created.Content.ReadFromJsonAsync<RestaurantResponse>())!;
        Assert.False(restaurant.IsOpen);
        var reloaded = await client.GetFromJsonAsync<RestaurantResponse>(created.Headers.Location);
        Assert.Equal("Local Kitchen", reloaded!.Name);
        Assert.Equal("Vega Baja", reloaded.Address.City);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{Restaurants}/{restaurant.Id}", Details("Updated Kitchen"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{Restaurants}/{restaurant.Id}/availability", new { isOpen = true })).StatusCode);
        reloaded = await client.GetFromJsonAsync<RestaurantResponse>(created.Headers.Location);
        Assert.Equal("Updated Kitchen", reloaded!.Name);
        Assert.True(reloaded.IsOpen);
    }

    [Fact]
    public async Task DifferentAccount_CannotSeeEditOrOpenAnotherOwnersRestaurant()
    {
        using var factory = new ApiFactory();
        using var owner = factory.CreateApiClient();
        await RegisterAndLogin(owner, "owner@example.com");
        var created = await owner.PostAsJsonAsync(Restaurants, Details());
        var restaurant = (await created.Content.ReadFromJsonAsync<RestaurantResponse>())!;
        using var stranger = factory.CreateApiClient();
        await RegisterAndLogin(stranger, "stranger@example.com");
        Assert.Empty((await stranger.GetFromJsonAsync<RestaurantResponse[]>(Restaurants))!);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync($"{Restaurants}/{restaurant.Id}", Details("Stolen"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync($"{Restaurants}/{restaurant.Id}/availability", new { isOpen = true })).StatusCode);
        var unchanged = await owner.GetFromJsonAsync<RestaurantResponse>(created.Headers.Location);
        Assert.Equal("Local Kitchen", unchanged!.Name);
        Assert.False(unchanged.IsOpen);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("address")]
    [InlineData("city")]
    [InlineData("tooLong")]
    public async Task Restaurant_RejectsInvalidInputWithoutSaving(string invalidField)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await RegisterAndLogin(client, "owner@example.com");
        var details = Details();
        details = invalidField switch
        {
            "name" => details with { Name = "   " },
            "address" => details with { Address = null! },
            "city" => details with { Address = details.Address with { City = "" } },
            _ => details with { Name = new string('x', 151) }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Restaurants, details)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<RestaurantResponse[]>(Restaurants))!);
    }

    [Fact]
    public async Task Restaurant_OwnershipComesFromLoginAndCannotBeOverriddenByRequest()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await RegisterAndLogin(client, "owner@example.com");
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/account");
        var request = JsonSerializer.SerializeToNode(Details(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        request["ownerId"] = Guid.NewGuid().ToString();
        request["isActive"] = false;
        request["isOpen"] = true;
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Restaurants, request)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var restaurant = await scope.ServiceProvider.GetRequiredService<BoricuaBiteDbContext>().Restaurants.SingleAsync();
        Assert.Equal(profile.GetProperty("id").GetGuid(), restaurant.OwnerId);
        Assert.True(restaurant.IsActive);
        Assert.False(restaurant.IsOpen);
    }

    [Fact]
    public async Task DeletedRestaurant_IsHiddenAndCannotBeUpdated()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await RegisterAndLogin(client, "owner@example.com");
        var created = await client.PostAsJsonAsync(Restaurants, Details());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BoricuaBiteDbContext>();
            (await db.Restaurants.SingleAsync()).IsDeleted = true;
            await db.SaveChangesAsync();
        }
        Assert.Empty((await client.GetFromJsonAsync<RestaurantResponse[]>(Restaurants))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(created.Headers.Location, Details("Restored"))).StatusCode);
    }

    [Fact]
    public async Task Authentication_ThrottlesExcessiveRequests()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        for (var attempt = 0; attempt < 20; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "invalid" })).StatusCode);
    }

    [Fact]
    public async Task MenuItem_PersistsWithItsRestaurantAndPrice()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        await RegisterAndLogin(client, "owner@example.com");
        var created = await client.PostAsJsonAsync(Restaurants, Details());
        var restaurant = (await created.Content.ReadFromJsonAsync<RestaurantResponse>())!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BoricuaBiteDbContext>();
            db.MenuItems.Add(new MenuItem(restaurant.Id, "Mofongo", 12.50m));
            await db.SaveChangesAsync();
        }
        using var readScope = factory.Services.CreateScope();
        var item = await readScope.ServiceProvider.GetRequiredService<BoricuaBiteDbContext>().MenuItems.SingleAsync();
        Assert.Equal(restaurant.Id, item.RestaurantId);
        Assert.Equal("Mofongo", item.Name);
        Assert.Equal(12.50m, item.Price);
    }
}
