namespace BoricuaBite.Application.Orders;

public sealed class MarketplacePricingOptions
{
    public const string SectionName = "MarketplacePricing";

    public decimal TaxRate { get; init; }
    public decimal CustomerServiceFeeRate { get; init; }
    public decimal CustomerServiceFeeFlat { get; init; }
    public decimal RestaurantCommissionRate { get; init; }
}
