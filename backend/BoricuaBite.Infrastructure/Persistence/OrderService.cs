using BoricuaBite.Application.Orders;
using BoricuaBite.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BoricuaBite.Infrastructure.Persistence;

public sealed class OrderService(
    BoricuaBiteDbContext db,
    IOptions<MarketplacePricingOptions> pricingOptions,
    ICheckoutProvider checkoutProvider) : IOrderService
{
    private readonly MarketplacePricingOptions pricing = pricingOptions.Value;

    public async Task<OrderResponse> CreateAsync(Guid customerId, string? customerEmail, CreateOrderRequest request, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.SingleOrDefaultAsync(x => x.Id == request.RestaurantId, ct)
            ?? throw new InvalidOperationException("Restaurant was not found.");

        if (!restaurant.IsActive || !restaurant.IsPublished || !restaurant.IsOpen || restaurant.OwnerId is null ||
            restaurant.SubscriptionStatus != RestaurantSubscriptionStatus.Active)
            throw new InvalidOperationException("This restaurant is not currently accepting marketplace orders.");

        var grouped = request.Items.GroupBy(x => x.MenuItemId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity));
        if (grouped.Count == 0) throw new InvalidOperationException("Your cart is empty.");
        if (grouped.Values.Any(quantity => quantity is <= 0 or > 99))
            throw new InvalidOperationException("Each item quantity must be from 1 to 99.");

        var ids = grouped.Keys.ToArray();
        var menuItems = await db.MenuItems.Where(x => ids.Contains(x.Id) && x.RestaurantId == restaurant.Id).ToListAsync(ct);
        if (menuItems.Count != ids.Length) throw new InvalidOperationException("One or more menu items no longer exist.");
        if (menuItems.Any(x => !x.IsAvailable)) throw new InvalidOperationException("One or more menu items are currently unavailable.");

        var snapshots = menuItems.Select(item => new MarketplaceOrderItem(item.Id, item.Name, item.Price, grouped[item.Id])).ToArray();
        var subtotal = Money(snapshots.Sum(x => x.Subtotal));
        var tax = Money(subtotal * pricing.TaxRate);
        var serviceFee = Money(subtotal * pricing.CustomerServiceFeeRate + pricing.CustomerServiceFeeFlat);
        var commission = Money(subtotal * pricing.RestaurantCommissionRate);

        if (request.PaymentMethod == OrderPaymentMethod.Online)
        {
            if (!checkoutProvider.IsConfigured)
                throw new InvalidOperationException("Online checkout is not configured yet. Choose pay at store or configure Stripe.");
            if (string.IsNullOrWhiteSpace(restaurant.StripeConnectedAccountId))
                throw new InvalidOperationException("This restaurant is not configured for online payouts yet.");
            if (string.IsNullOrWhiteSpace(customerEmail))
                throw new InvalidOperationException("An email address is required for online checkout.");
        }

        var order = new MarketplaceOrder(restaurant.Id, customerId, request.PaymentMethod,
            subtotal, tax, serviceFee, commission, snapshots);
        db.MarketplaceOrders.Add(order);
        await db.SaveChangesAsync(ct);

        string? checkoutUrl = null;
        if (request.PaymentMethod == OrderPaymentMethod.Online)
        {
            try
            {
                var checkout = await checkoutProvider.CreateOrderCheckoutAsync(order, restaurant, customerEmail!, ct);
                order.AttachCheckoutSession(checkout.SessionId);
                checkoutUrl = checkout.CheckoutUrl;
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                order.MarkPaymentFailed();
                await db.SaveChangesAsync(ct);
                throw;
            }
        }

        return ToResponse(order, restaurant.Name, customerEmail, checkoutUrl);
    }

    public async Task<IReadOnlyList<OrderResponse>> ListCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var orders = await db.MarketplaceOrders.AsNoTracking().Include(x => x.Items)
            .Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var names = await RestaurantNames(orders.Select(x => x.RestaurantId), ct);
        return orders.Select(x => ToResponse(x, names.GetValueOrDefault(x.RestaurantId, "Restaurant"), null)).ToArray();
    }

    public async Task<IReadOnlyList<OrderResponse>> ListOwnerAsync(Guid ownerId, Guid? restaurantId, CancellationToken ct)
    {
        var query = db.MarketplaceOrders.AsNoTracking().Include(x => x.Items)
            .Where(x => db.Restaurants.Any(r => r.Id == x.RestaurantId && r.OwnerId == ownerId));
        if (restaurantId.HasValue) query = query.Where(x => x.RestaurantId == restaurantId.Value);

        var orders = await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var names = await RestaurantNames(orders.Select(x => x.RestaurantId), ct);
        var emails = await db.Users.AsNoTracking().Where(x => orders.Select(o => o.CustomerId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email, ct);
        return orders.Select(x => ToResponse(x, names.GetValueOrDefault(x.RestaurantId, "Restaurant"), emails.GetValueOrDefault(x.CustomerId))).ToArray();
    }

    public async Task<OrderResponse?> UpdateOwnerStatusAsync(Guid ownerId, Guid orderId, MarketplaceOrderStatus status, CancellationToken ct)
    {
        var order = await db.MarketplaceOrders.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == orderId && db.Restaurants.Any(r => r.Id == x.RestaurantId && r.OwnerId == ownerId), ct);
        if (order is null) return null;
        order.TransitionTo(status);
        await db.SaveChangesAsync(ct);
        var restaurantName = await db.Restaurants.Where(x => x.Id == order.RestaurantId).Select(x => x.Name).SingleAsync(ct);
        var email = await db.Users.Where(x => x.Id == order.CustomerId).Select(x => x.Email).SingleOrDefaultAsync(ct);
        return ToResponse(order, restaurantName, email);
    }

    public async Task<OrderResponse?> MarkPaidByCheckoutSessionAsync(string sessionId, CancellationToken ct)
    {
        var order = await db.MarketplaceOrders.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.StripeCheckoutSessionId == sessionId, ct);
        if (order is null) return null;
        order.MarkPaid();
        await db.SaveChangesAsync(ct);
        var restaurantName = await db.Restaurants.Where(x => x.Id == order.RestaurantId).Select(x => x.Name).SingleAsync(ct);
        return ToResponse(order, restaurantName, null);
    }

    private async Task<Dictionary<Guid, string>> RestaurantNames(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var values = ids.Distinct().ToArray();
        return await db.Restaurants.AsNoTracking().Where(x => values.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    }

    private static OrderResponse ToResponse(MarketplaceOrder order, string restaurantName, string? customerEmail, string? checkoutUrl = null) => new(
        order.Id, order.RestaurantId, restaurantName, order.CustomerId, customerEmail, order.Status,
        order.PaymentMethod, order.PaymentStatus, order.Subtotal, order.TaxAmount, order.ServiceFee,
        order.CommissionAmount, order.Total, order.EstimatedRestaurantProceeds, order.Currency, checkoutUrl,
        order.CreatedAtUtc, order.Items.Select(x => new OrderItemResponse(x.MenuItemId, x.Name, x.UnitPrice, x.Quantity, Money(x.Subtotal))).ToArray());

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
