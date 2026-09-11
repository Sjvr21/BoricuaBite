using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BoricuaBite.Api.Endpoints;
using BoricuaBite.Application.Authentication;
using BoricuaBite.Application.Restaurants;
using BoricuaBite.Application.Catalog;
using BoricuaBite.Application.Menus;
using BoricuaBite.Application.Notifications;
using BoricuaBite.Application.Orders;
using BoricuaBite.Application.Reviews;
using BoricuaBite.Infrastructure.Identity;
using BoricuaBite.Infrastructure.Notifications;
using BoricuaBite.Infrastructure.Payments;
using BoricuaBite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<BoricuaBiteDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("BoricuaBite")
        ?? throw new InvalidOperationException("Set ConnectionStrings:BoricuaBite with user-secrets or an environment variable.")));
builder.Services.Configure<MarketplacePricingOptions>(builder.Configuration.GetSection(MarketplacePricingOptions.SectionName));
builder.Services.Configure<EmailDeliveryOptions>(builder.Configuration.GetSection(EmailDeliveryOptions.SectionName));
builder.Services.Configure<GoogleIdentityOptions>(builder.Configuration.GetSection(GoogleIdentityOptions.SectionName));
builder.Services.AddScoped<IRestaurantService, RestaurantService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IGoogleIdentityValidator, GoogleIdentityValidator>();
builder.Services.AddHttpClient<ITransactionalEmailSender, SendGridEmailSender>(client =>
    client.BaseAddress = new Uri("https://api.sendgrid.com/"));
builder.Services.AddHttpClient<ICheckoutProvider, StripeCheckoutProvider>();
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

var authorization = builder.Services.AddAuthorizationBuilder();
authorization.AddPolicy("ApiBearer", policy =>
{
    policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme);
    policy.RequireAuthenticatedUser();
    policy.RequireAssertion(context => Guid.TryParse(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty);
});
authorization.AddPolicy("PlatformAdmin", policy =>
{
    policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme);
    policy.RequireAuthenticatedUser();
    policy.RequireAssertion(context =>
    {
        var configuredAdmin = builder.Configuration["Admin:Email"];
        var currentEmail = context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.FindFirstValue(ClaimTypes.Name);
        return !string.IsNullOrWhiteSpace(configuredAdmin)
            && string.Equals(configuredAdmin.Trim(), currentEmail, StringComparison.OrdinalIgnoreCase);
    });
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

// Password-only login would bypass BoricuaBite's email 2FA flow. Keep Identity's
// registration/refresh endpoints, but require password sign-in through /api/security.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path.Equals("/api/auth/login"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGroup("/api/auth").RequireRateLimiting("authentication").MapIdentityApi<ApplicationUser>();
app.MapAuthSecurityEndpoints();
app.MapGet("/api/account", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, IConfiguration configuration) =>
{
    var user = await users.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var configuredAdmin = configuration["Admin:Email"];
    var isAdmin = !string.IsNullOrWhiteSpace(configuredAdmin)
        && string.Equals(configuredAdmin.Trim(), user.Email, StringComparison.OrdinalIgnoreCase);
    return Results.Ok(new { user.Id, user.Email, IsAdmin = isAdmin });
}).RequireAuthorization("ApiBearer");
app.MapRestaurantEndpoints();
app.MapAdminEndpoints();
app.MapMenuEndpoints();
app.MapCatalogEndpoints();
app.MapOrderEndpoints();
app.MapReviewEndpoints();
app.MapMediaEndpoints();
app.MapStripeWebhookEndpoints();

app.Run();

public partial class Program { }
