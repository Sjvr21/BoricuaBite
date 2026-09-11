using BoricuaBite.Application.Common;
using BoricuaBite.Application.Menus;
using BoricuaBite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Infrastructure.Persistence;

public sealed class MenuService(BoricuaBiteDbContext db) : IMenuService
{
    private Task<bool> OwnsAsync(Guid ownerId, Guid restaurantId, CancellationToken ct) =>
        db.Restaurants.AnyAsync(x => x.Id == restaurantId && x.OwnerId == ownerId, ct);

    private IQueryable<MenuItem> OwnedItems(Guid ownerId, Guid restaurantId) =>
        db.MenuItems.Where(x => x.RestaurantId == restaurantId &&
            db.Restaurants.Any(r => r.Id == x.RestaurantId && r.OwnerId == ownerId));

    public async Task<Page<MenuItemResponse>?> ListAsync(Guid ownerId, Guid restaurantId, PageRequest page, CancellationToken ct)
    {
        if (!await OwnsAsync(ownerId, restaurantId, ct)) return null;
        var query = OwnedItems(ownerId, restaurantId).AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.Size)
            .Select(x => new MenuItemResponse(x.Id, x.Name, x.Description, x.ImageUrl, x.Price, x.IsAvailable)).ToListAsync(ct);
        return new(items, page.Number, page.Size, total);
    }

    public async Task<MenuItemResponse?> GetAsync(Guid ownerId, Guid restaurantId, Guid itemId, CancellationToken ct) =>
        await OwnedItems(ownerId, restaurantId).AsNoTracking().Where(x => x.Id == itemId)
            .Select(x => new MenuItemResponse(x.Id, x.Name, x.Description, x.ImageUrl, x.Price, x.IsAvailable)).SingleOrDefaultAsync(ct);

    public async Task<MenuItemResponse?> CreateAsync(Guid ownerId, Guid restaurantId, MenuItemDetails details, CancellationToken ct)
    {
        if (!await OwnsAsync(ownerId, restaurantId, ct)) return null;
        var item = new MenuItem(restaurantId, details.Name, details.Price!.Value, details.Description, details.ImageUrl);
        db.MenuItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task<MenuItemResponse?> UpdateAsync(Guid ownerId, Guid restaurantId, Guid itemId, MenuItemDetails details, CancellationToken ct)
    {
        var item = await OwnedItems(ownerId, restaurantId).SingleOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return null;
        item.Update(details.Name, details.Price!.Value, details.Description, details.ImageUrl);
        await db.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task<MenuItemResponse?> SetAvailabilityAsync(Guid ownerId, Guid restaurantId, Guid itemId, bool available, CancellationToken ct)
    {
        var item = await OwnedItems(ownerId, restaurantId).SingleOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return null;
        item.IsAvailable = available;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task<bool> DeleteAsync(Guid ownerId, Guid restaurantId, Guid itemId, CancellationToken ct)
    {
        var item = await OwnedItems(ownerId, restaurantId).SingleOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return false;
        item.IsDeleted = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static MenuItemResponse ToResponse(MenuItem item) => new(item.Id, item.Name, item.Description, item.ImageUrl, item.Price, item.IsAvailable);
}
