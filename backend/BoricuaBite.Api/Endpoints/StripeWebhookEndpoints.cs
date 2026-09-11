using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoricuaBite.Application.Orders;

namespace BoricuaBite.Api.Endpoints;

public static class StripeWebhookEndpoints
{
    public static void MapStripeWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payments/stripe/webhook", async (HttpRequest request, IConfiguration configuration,
            IOrderService orders, CancellationToken ct) =>
        {
            var secret = configuration["Stripe:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(secret))
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync(ct);
            var signature = request.Headers["Stripe-Signature"].ToString();
            if (!IsValidStripeSignature(payload, signature, secret))
                return Results.Unauthorized();

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var eventType = root.GetProperty("type").GetString();
            if (eventType is "checkout.session.completed" or "checkout.session.async_payment_succeeded")
            {
                var session = root.GetProperty("data").GetProperty("object");
                var paymentStatus = session.TryGetProperty("payment_status", out var status) ? status.GetString() : null;
                var sessionId = session.GetProperty("id").GetString();
                if (!string.IsNullOrWhiteSpace(sessionId) && paymentStatus == "paid")
                    await orders.MarkPaidByCheckoutSessionAsync(sessionId, ct);
            }

            return Results.Ok();
        }).AllowAnonymous().WithTags("Payments");
    }

    private static bool IsValidStripeSignature(string payload, string header, string secret)
    {
        if (string.IsNullOrWhiteSpace(header)) return false;
        string? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in header.Split(','))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;
            if (pair[0] == "t") timestamp = pair[1];
            if (pair[0] == "v1") signatures.Add(pair[1]);
        }
        if (timestamp is null || signatures.Count == 0 || !long.TryParse(timestamp, out var unix)) return false;
        var eventTime = DateTimeOffset.FromUnixTimeSeconds(unix);
        if (Math.Abs((DateTimeOffset.UtcNow - eventTime).TotalMinutes) > 5) return false;

        var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}.{payload}");
        var key = Encoding.UTF8.GetBytes(secret);
        var expected = Convert.ToHexString(HMACSHA256.HashData(key, signedPayload)).ToLowerInvariant();

        foreach (var candidate in signatures)
        {
            if (candidate.Length != expected.Length) continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(expected)))
                return true;
        }
        return false;
    }
}
