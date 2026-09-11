using System.Security.Claims;
using System.Threading.RateLimiting;
using BoricuaBite.Api.Endpoints;
using BoricuaBite.Application.Restaurants;
using BoricuaBite.Application.Catalog;
using BoricuaBite.Application.Menus;
using BoricuaBite.Infrastructure.Identity;
using BoricuaBite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<BoricuaBiteDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("BoricuaBite")
        ?? throw new InvalidOperationException("Set ConnectionStrings:BoricuaBite with user-secrets or an environment variable.")));
builder.Services.AddScoped<IRestaurantService, RestaurantService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<BoricuaBiteDbContext>();
builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
{
    options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
    options.RefreshTokenExpiration = TimeSpan.FromDays(7);
});
builder.Services.AddAuthorizationBuilder().AddPolicy("ApiBearer", policy =>
{
    policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme);
    policy.RequireAuthenticatedUser();
    policy.RequireAssertion(context => Guid.TryParse(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty);
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGroup("/api/auth").RequireRateLimiting("authentication").MapIdentityApi<ApplicationUser>();
app.MapGet("/api/account", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
{
    var user = await users.GetUserAsync(principal);
    return user is null ? Results.Unauthorized() : Results.Ok(new { user.Id, user.Email });
}).RequireAuthorization("ApiBearer");
app.MapRestaurantEndpoints();
app.MapMenuEndpoints();
app.MapCatalogEndpoints();

app.Run();

public partial class Program { }
