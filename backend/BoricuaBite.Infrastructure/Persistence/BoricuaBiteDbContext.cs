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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var restaurant = builder.Entity<Restaurant>();
        restaurant.HasQueryFilter(x => !x.IsDeleted);
        restaurant.Property(x => x.Name).HasMaxLength(150).IsRequired();
        restaurant.Property(x => x.Description).HasMaxLength(2000);
        restaurant.Property(x => x.PhoneNumber).HasMaxLength(30);
        restaurant.Property(x => x.LogoUrl).HasMaxLength(2048);
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
        menuItem.Property(x => x.Price).HasPrecision(12, 2);
        menuItem.HasOne<Restaurant>().WithMany().HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
