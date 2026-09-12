using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BoricuaBite.Application.Connect;
using Microsoft.Extensions.Configuration;

namespace BoricuaBite.Infrastructure.Payments;

public sealed class StripeConnectProvider(HttpClient http, IConfiguration configuration) : IStripeConnectProvider
{
    private readonly string? secretKey = configuration["Stripe:SecretKey"];
    private readonly string publishableKey = configuration["Stripe:PublishableKey"] ?? string.Empty;
    private readonly string connectedAccountCountry = (configuration["Stripe:ConnectedAccountCountry"] ?? "us").Trim().ToLowerInvariant();
    private readonly string previewApiVersion = configuration["Stripe:PreviewApiVersion"] ?? "2026-08-26.preview";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(secretKey) && !string.IsNullOrWhiteSpace(publishableKey);
    public string PublishableKey => publishableKey;

    public async Task<string> CreateRecipientAccountAsync(string ownerEmail, string displayName, CancellationToken ct)
    {
        EnsureConfigured();

        var body = new
        {
            contact_email = ownerEmail.Trim(),
            display_name = displayName.Trim(),
            dashboard = "express",
            identity = new { country = connectedAccountCountry },
            defaults = new
            {
                currency = "usd",
                responsibilities = new
                {
                    fees_collector = "application",
                    losses_collector = "application"
                }
            },
            configuration = new
            {
                customer = new { },
                recipient = new
                {
                    capabilities = new
                    {
                        stripe_balance = new
                        {
                            stripe_transfers = new { requested = true }
                        }
                    }
                }
            },
            include = new[] { "configuration.customer", "configuration.recipient", "identity", "requirements" }
        };

        using var request = JsonRequest(HttpMethod.Post, "https://api.stripe.com/v2/core/accounts", body, preview: true);
        var root = await SendAsync(request, ct);
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Stripe did not return a connected account id.");
        return id;
    }

    public async Task<string> CreateAccountSessionAsync(string connectedAccountId, CancellationToken ct)
    {
        EnsureConfigured();
        var fields = new List<KeyValuePair<string, string>>
        {
            new("account", connectedAccountId),
            new("components[account_onboarding][enabled]", "true"),
            new("components[account_onboarding][features][external_account_collection]", "true"),
            new("components[notification_banner][enabled]", "true"),
            new("components[account_management][enabled]", "true"),
            new("components[payments][enabled]", "true"),
            new("components[payouts][enabled]", "true")
        };

        using var request = FormRequest(HttpMethod.Post, "https://api.stripe.com/v1/account_sessions", fields);
        var root = await SendAsync(request, ct);
        var clientSecret = root.TryGetProperty("client_secret", out var secretElement) ? secretElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Stripe did not return an Account Session client secret.");
        return clientSecret;
    }

    public async Task<string> CreateExpressLoginLinkAsync(string connectedAccountId, CancellationToken ct)
    {
        EnsureConfigured();
        using var request = FormRequest(HttpMethod.Post,
            $"https://api.stripe.com/v1/accounts/{Uri.EscapeDataString(connectedAccountId)}/login_links", []);
        var root = await SendAsync(request, ct);
        var url = root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Stripe did not return an Express Dashboard login link.");
        return url;
    }

    public async Task<RestaurantStripeStatus> GetStatusAsync(string connectedAccountId, CancellationToken ct)
    {
        EnsureConfigured();
        var url = $"https://api.stripe.com/v2/core/accounts/{Uri.EscapeDataString(connectedAccountId)}" +
                  "?include%5B%5D=configuration.recipient&include%5B%5D=requirements";
        using var request = AuthorizedRequest(HttpMethod.Get, url, preview: true);
        var root = await SendAsync(request, ct);

        var transfers = CapabilityStatus(root, "stripe_transfers");
        var payouts = CapabilityStatus(root, "payouts");
        return new RestaurantStripeStatus(connectedAccountId, transfers == "active", payouts == "active",
            transfers == "active" && payouts == "active");
    }

    private string? CapabilityStatus(JsonElement root, string capability)
    {
        if (!root.TryGetProperty("configuration", out var configurationElement) ||
            !configurationElement.TryGetProperty("recipient", out var recipient) ||
            recipient.ValueKind != JsonValueKind.Object ||
            !recipient.TryGetProperty("capabilities", out var capabilities) ||
            !capabilities.TryGetProperty("stripe_balance", out var stripeBalance) ||
            !stripeBalance.TryGetProperty(capability, out var capabilityElement) ||
            !capabilityElement.TryGetProperty("status", out var status))
            return null;
        return status.GetString();
    }

    private HttpRequestMessage JsonRequest(HttpMethod method, string url, object body, bool preview = false)
    {
        var request = AuthorizedRequest(method, url, preview);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return request;
    }

    private HttpRequestMessage FormRequest(HttpMethod method, string url, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var request = AuthorizedRequest(method, url);
        request.Content = new FormUrlEncodedContent(fields);
        return request;
    }

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, bool preview = false)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        if (preview) request.Headers.TryAddWithoutValidation("Stripe-Version", previewApiVersion);
        return request;
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
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
            throw new InvalidOperationException("Stripe:SecretKey and Stripe:PublishableKey must be configured.");
    }
}
