using System.Net.Http.Headers;
using System.Text.Json;
using BoricuaBite.Application.Orders;
using BoricuaBite.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace BoricuaBite.Infrastructure.Payments;

public sealed class StripeCheckoutProvider(HttpClient http, IConfiguration configuration) : ICheckoutProvider
{
    private readonly string? secretKey = configuration["Stripe:SecretKey"];
    private readonly string frontendBaseUrl = (configuration["Stripe:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');

    public bool IsConfigured => !string.IsNullOrWhiteSpace(secretKey);

    public async Task<CheckoutSessionResult> CreateOrderCheckoutAsync(
        MarketplaceOrder order, Restaurant restaurant, string customerEmail, CancellationToken ct)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(restaurant.StripeConnectedAccountId))
            throw new InvalidOperationException("Restaurant has no Stripe connected account.");

        var platformAmount = ToCents(order.ServiceFee + order.CommissionAmount);
        var fields = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("success_url", $"{frontendBaseUrl}/?checkout=success&order={order.Id}"),
            new("cancel_url", $"{frontendBaseUrl}/?checkout=cancelled&order={order.Id}"),
            new("customer_email", customerEmail),
            new("client_reference_id", order.Id.ToString()),
            new("metadata[order_id]", order.Id.ToString()),
            new("payment_intent_data[metadata][order_id]", order.Id.ToString()),
            new("payment_intent_data[transfer_data][destination]", restaurant.StripeConnectedAccountId),
            new("payment_intent_data[application_fee_amount]", platformAmount.ToString()),
            new("line_items[0][price_data][currency]", "usd"),
            new("line_items[0][price_data][product_data][name]", $"Food order from {restaurant.Name}"),
            new("line_items[0][price_data][unit_amount]", ToCents(order.Subtotal).ToString()),
            new("line_items[0][quantity]", "1")
        };

        var index = 1;
        if (order.TaxAmount > 0)
        {
            fields.Add(new($"line_items[{index}][price_data][currency]", "usd"));
            fields.Add(new($"line_items[{index}][price_data][product_data][name]", "Tax"));
            fields.Add(new($"line_items[{index}][price_data][unit_amount]", ToCents(order.TaxAmount).ToString()));
            fields.Add(new($"line_items[{index}][quantity]", "1"));
            index++;
        }
        if (order.ServiceFee > 0)
        {
            fields.Add(new($"line_items[{index}][price_data][currency]", "usd"));
            fields.Add(new($"line_items[{index}][price_data][product_data][name]", "BoricuaBite service fee"));
            fields.Add(new($"line_items[{index}][price_data][unit_amount]", ToCents(order.ServiceFee).ToString()));
            fields.Add(new($"line_items[{index}][quantity]", "1"));
        }

        var root = await SendFormAsync("https://api.stripe.com/v1/checkout/sessions", fields, ct);
        var id = root.GetProperty("id").GetString();
        var url = root.GetProperty("url").GetString();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Stripe did not return a checkout session URL.");

        return new CheckoutSessionResult(id, url);
    }

    public async Task RefundAsync(string paymentIntentId, CancellationToken ct)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new ArgumentException("Stripe payment intent id is required.", nameof(paymentIntentId));

        await SendFormAsync("https://api.stripe.com/v1/refunds",
        [
            new("payment_intent", paymentIntentId.Trim()),
            new("refund_application_fee", "true"),
            new("reverse_transfer", "true")
        ], ct);
    }

    public async Task<bool> ReverseTransferForChargeAsync(string chargeId, string idempotencyKey, CancellationToken ct)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(chargeId))
            throw new ArgumentException("Stripe charge id is required.", nameof(chargeId));

        var charge = await GetAsync($"https://api.stripe.com/v1/charges/{Uri.EscapeDataString(chargeId.Trim())}", ct);
        var transferId = charge.TryGetProperty("transfer", out var transfer) && transfer.ValueKind == JsonValueKind.String
            ? transfer.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(transferId)) return false;

        await SendFormAsync(
            $"https://api.stripe.com/v1/transfers/{Uri.EscapeDataString(transferId)}/reversals",
            [], ct, idempotencyKey);
        return true;
    }

    private async Task<JsonElement> GetAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        return await SendAsync(request, ct);
    }

    private async Task<JsonElement> SendFormAsync(string url, IEnumerable<KeyValuePair<string, string>> fields,
        CancellationToken ct, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await SendAsync(request, ct);
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Stripe request failed: {json}");

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured) throw new InvalidOperationException("Stripe:SecretKey is not configured.");
    }

    private static long ToCents(decimal amount) => checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
}
