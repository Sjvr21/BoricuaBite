using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoricuaBite.Application.Orders;
using BoricuaBite.Application.Subscriptions;
using BoricuaBite.Domain.Entities;
using BoricuaBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Api.Endpoints;

public static class StripeWebhookEndpoints
{
    public static void MapStripeWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payments/stripe/webhook", async (
            HttpRequest request,
            IConfiguration configuration,
            IOrderService orders,
            IRestaurantSubscriptionService subscriptions,
            ICheckoutProvider checkoutProvider,
            BoricuaBiteDbContext db,
            CancellationToken ct) =>
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
            var eventId = root.TryGetProperty("id", out var eventIdElement) ? eventIdElement.GetString() : null;
            var eventType = root.TryGetProperty("type", out var eventTypeElement) ? eventTypeElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
                return Results.BadRequest(new { error = "Stripe event id and type are required." });

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO "StripeWebhookEvents" ("EventId", "EventType", "ProcessedAtUtc")
                VALUES ({{eventId}}, {{eventType}}, {{DateTime.UtcNow}})
                ON CONFLICT ("EventId") DO NOTHING;
                """, ct);

            if (inserted == 0)
                return Results.Ok();

            var dataObject = root.GetProperty("data").GetProperty("object");

            if (eventType is "checkout.session.completed" or "checkout.session.async_payment_succeeded")
            {
                var mode = StringProperty(dataObject, "mode");
                if (mode == "subscription")
                {
                    var restaurantId = MetadataGuid(dataObject, "restaurant_id");
                    var subscriptionId = StringProperty(dataObject, "subscription");
                    if (restaurantId.HasValue && !string.IsNullOrWhiteSpace(subscriptionId))
                        await subscriptions.ApplyStripeStateAsync(
                            restaurantId.Value, subscriptionId, RestaurantSubscriptionStatus.Active, ct);
                }
                else
                {
                    var paymentStatus = StringProperty(dataObject, "payment_status");
                    var sessionId = StringProperty(dataObject, "id");
                    var paymentIntentId = StringProperty(dataObject, "payment_intent");
                    if (!string.IsNullOrWhiteSpace(sessionId) && paymentStatus == "paid")
                        await orders.MarkPaidByCheckoutSessionAsync(sessionId, paymentIntentId, ct);
                }
            }
            else if (eventType is "customer.subscription.created" or "customer.subscription.updated")
            {
                await ApplySubscriptionEventAsync(dataObject, subscriptions, ct);
            }
            else if (eventType == "customer.subscription.deleted")
            {
                var subscriptionId = StringProperty(dataObject, "id");
                if (!string.IsNullOrWhiteSpace(subscriptionId))
                    await subscriptions.ApplyStripeStateBySubscriptionIdAsync(
                        subscriptionId, RestaurantSubscriptionStatus.Cancelled, ct);
            }
            else if (eventType is "invoice.paid" or "invoice.payment_failed")
            {
                var subscriptionId = InvoiceSubscriptionId(dataObject);
                if (!string.IsNullOrWhiteSpace(subscriptionId))
                {
                    var status = eventType == "invoice.paid"
                        ? RestaurantSubscriptionStatus.Active
                        : RestaurantSubscriptionStatus.PastDue;
                    await subscriptions.ApplyStripeStateBySubscriptionIdAsync(subscriptionId, status, ct);
                }
            }
            else if (eventType == "charge.dispute.created")
            {
                var chargeId = StringProperty(dataObject, "charge");
                if (!string.IsNullOrWhiteSpace(chargeId))
                    await checkoutProvider.ReverseTransferForChargeAsync(
                        chargeId, $"boricuabite-dispute-{eventId}", ct);
            }

            await transaction.CommitAsync(ct);
            return Results.Ok();
        }).AllowAnonymous().WithTags("Payments");
    }

    private static async Task ApplySubscriptionEventAsync(
        JsonElement subscription,
        IRestaurantSubscriptionService subscriptions,
        CancellationToken ct)
    {
        var subscriptionId = StringProperty(subscription, "id");
        if (string.IsNullOrWhiteSpace(subscriptionId)) return;

        var status = StringProperty(subscription, "status") switch
        {
            "trialing" or "active" => RestaurantSubscriptionStatus.Active,
            "past_due" or "incomplete" or "paused" => RestaurantSubscriptionStatus.PastDue,
            "unpaid" or "canceled" or "incomplete_expired" => RestaurantSubscriptionStatus.Cancelled,
            _ => (RestaurantSubscriptionStatus?)null
        };
        if (!status.HasValue) return;

        var restaurantId = MetadataGuid(subscription, "restaurant_id");
        if (restaurantId.HasValue)
            await subscriptions.ApplyStripeStateAsync(restaurantId.Value, subscriptionId, status.Value, ct);
        else
            await subscriptions.ApplyStripeStateBySubscriptionIdAsync(subscriptionId, status.Value, ct);
    }

    private static string? InvoiceSubscriptionId(JsonElement invoice)
    {
        var direct = StringProperty(invoice, "subscription");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;

        if (invoice.TryGetProperty("parent", out var parent) && parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty("subscription_details", out var details) && details.ValueKind == JsonValueKind.Object)
            return StringProperty(details, "subscription");

        return null;
    }

    private static Guid? MetadataGuid(JsonElement element, string key)
    {
        if (!element.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        return Guid.TryParse(value.GetString(), out var id) ? id : null;
    }

    private static string? StringProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

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
