using System.Net.Http.Headers;
using System.Text.Json;
using BoricuaBite.Application.Subscriptions;
using BoricuaBite.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace BoricuaBite.Infrastructure.Payments;

public sealed class StripeSubscriptionCheckoutProvider(HttpClient http, IConfiguration configuration)
    : ISubscriptionCheckoutProvider
{
    private readonly string? secretKey = configuration["Stripe:SecretKey"];
    private readonly string? priceId = configuration["Stripe:RestaurantSubscriptionPriceId"];
    private readonly string frontendBaseUrl = (configuration["Stripe:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
    private readonly string previewApiVersion = configuration["Stripe:PreviewApiVersion"] ?? "2026-08-26.preview";
    private readonly int trialDays = int.TryParse(configuration["Stripe:RestaurantTrialDays"], out var days) ? days : 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(secretKey) && !string.IsNullOrWhiteSpace(priceId);

    public async Task<SubscriptionCheckoutResult> CreateAsync(
        Restaurant restaurant, string ownerEmail, CancellationToken ct)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(restaurant.StripeConnectedAccountId))
            throw new InvalidOperationException("Complete Stripe Connect onboarding before starting the subscription.");

        var fields = new List<KeyValuePair<string, string>>
        {
            new("mode", "subscription"),
            new("success_url", $"{frontendBaseUrl}/?subscription=success&restaurant={restaurant.Id}"),
            new("cancel_url", $"{frontendBaseUrl}/?subscription=cancelled&restaurant={restaurant.Id}"),
            new("customer_account", restaurant.StripeConnectedAccountId),
            new("line_items[0][price]", priceId!),
            new("line_items[0][quantity]", "1"),
            new("subscription_data[trial_period_days]", trialDays.ToString()),
            new("subscription_data[metadata][restaurant_id]", restaurant.Id.ToString()),
            new("metadata[restaurant_id]", restaurant.Id.ToString()),
            new("payment_method_collection", "always")
        };

        var root = await SendFormAsync("https://api.stripe.com/v1/checkout/sessions", fields, preview: true, ct);
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        var url = root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Stripe did not return a subscription Checkout URL.");

        return new SubscriptionCheckoutResult(id, url);
    }

    public async Task<SubscriptionPortalResult> CreatePortalAsync(Restaurant restaurant, CancellationToken ct)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(restaurant.StripeConnectedAccountId))
            throw new InvalidOperationException("This restaurant does not have a Stripe connected account.");
        if (string.IsNullOrWhiteSpace(restaurant.StripeSubscriptionId))
            throw new InvalidOperationException("Start the restaurant subscription before opening the billing portal.");

        var root = await SendFormAsync("https://api.stripe.com/v1/billing_portal/sessions",
        [
            new("customer_account", restaurant.StripeConnectedAccountId),
            new("return_url", $"{frontendBaseUrl}/?view=owner&restaurant={restaurant.Id}")
        ], preview: true, ct);

        var url = root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Stripe did not return a Customer Portal URL.");
        return new SubscriptionPortalResult(url);
    }

    private async Task<JsonElement> SendFormAsync(
        string url, IEnumerable<KeyValuePair<string, string>> fields, bool preview, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        if (preview) request.Headers.TryAddWithoutValidation("Stripe-Version", previewApiVersion);

        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Stripe request failed ({(int)response.StatusCode}): {json}");

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Stripe:SecretKey and Stripe:RestaurantSubscriptionPriceId must be configured.");
    }
}
