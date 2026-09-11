using BoricuaBite.Domain.Entities;
using BoricuaBite.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Infrastructure.Persistence;

public sealed class BoricuaBiteDbContext(DbContextOptions<BoricuaBiteDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MarketplaceOrder> MarketplaceOrders => Set<MarketplaceOrder>();
    public DbSet<MarketplaceOrderItem> MarketplaceOrderItems => Set<MarketplaceOrderItem>();
    public DbSet<RestaurantReview> RestaurantReviews => Set<RestaurantReview>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var restaurant = builder.Entity<Restaurant>();
        restaurant.HasQueryFilter(x => !x.IsDeleted);
        restaurant.Property(x => x.Name).HasMaxLength(150).IsRequired();
        restaurant.Property(x => x.Description).HasMaxLength(2000);
        restaurant.Property(x => x.PhoneNumber).HasMaxLength(30);
        restaurant.Property(x => x.LogoUrl).HasMaxLength(2048);
        restaurant.Property(x => x.CoverImageUrl).HasMaxLength(2048);
        restaurant.Property(x => x.StripeConnectedAccountId).HasMaxLength(255);
        restaurant.Property(x => x.StripeSubscriptionId).HasMaxLength(255);
        restaurant.Property(x => x.SubscriptionStatus).HasConversion<string>().HasMaxLength(32);
        restaurant.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        restaurant.OwnsOne(x => x.Address, address =>
        {
            address.Property(x => x.AddressLine1).HasMaxLength(200).IsRequired();
            address.Property(x => x.AddressLine2).HasMaxLength(200);
            address.Property(x => x.City).HasMaxLength(100).IsRequired();
            address.Property(x => x.StateOrTerritory).HasMaxLength(100).IsRequired();
            address.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
            address.Property(x => x.Country).HasMaxLength(100).IsRequired();
        });
        restaurant.Navigation(x => x.Address).IsRequired();

        var menuItem = builder.Entity<MenuItem>();
        menuItem.HasQueryFilter(x => !x.IsDeleted);
        menuItem.Property(x => x.RestaurantId);
        menuItem.Property(x => x.Name).HasMaxLength(150).IsRequired();
        menuItem.Property(x => x.Description).HasMaxLength(1000);
        menuItem.Property(x => x.ImageUrl).HasMaxLength(2048);
        menuItem.Property(x => x.Price).HasPrecision(12, 2);
        menuItem.HasOne<Restaurant>().WithMany().HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        var order = builder.Entity<MarketplaceOrder>();
        order.HasQueryFilter(x => !x.IsDeleted);
        order.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        order.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(32);
        order.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(32);
        order.Property(x => x.Subtotal).HasPrecision(12, 2);
        order.Property(x => x.TaxAmount).HasPrecision(12, 2);
        order.Property(x => x.ServiceFee).HasPrecision(12, 2);
        order.Property(x => x.CommissionAmount).HasPrecision(12, 2);
        order.Property(x => x.Total).HasPrecision(12, 2);
        order.Property(x => x.Currency).HasMaxLength(3);
        order.Property(x => x.StripeCheckoutSessionId).HasMaxLength(255);
        order.Property(x => x.StripePaymentIntentId).HasMaxLength(255);
        order.HasIndex(x => x.StripeCheckoutSessionId).IsUnique();
        order.HasIndex(x => x.StripePaymentIntentId).IsUnique();
        order.HasOne<Restaurant>().WithMany().HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Restrict);
        order.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        order.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);

        var orderItem = builder.Entity<MarketplaceOrderItem>();
        orderItem.HasKey(x => x.Id);
        orderItem.Property(x => x.Name).HasMaxLength(150).IsRequired();
        orderItem.Property(x => x.UnitPrice).HasPrecision(12, 2);

        var review = builder.Entity<RestaurantReview>();
        review.HasQueryFilter(x => !x.IsDeleted);
        review.Property(x => x.Comment).HasMaxLength(2000);
        review.HasIndex(x => x.OrderId).IsUnique();
        review.HasOne<Restaurant>().WithMany().HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Restrict);
        review.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        review.HasOne<MarketplaceOrder>().WithOne().HasForeignKey<RestaurantReview>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
