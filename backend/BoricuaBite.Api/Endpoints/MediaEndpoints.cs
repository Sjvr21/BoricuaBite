using System.Security.Claims;
using BoricuaBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Api.Endpoints;

public static class MediaEndpoints
{
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/owner/restaurants/{restaurantId:guid}/media", async (
            Guid restaurantId, HttpRequest request, ClaimsPrincipal principal,
            BoricuaBiteDbContext db, IWebHostEnvironment environment, CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!await db.Restaurants.AnyAsync(x => x.Id == restaurantId && x.OwnerId == ownerId, ct))
                return Results.NotFound();

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Use multipart/form-data with a file field." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Image file is required." });
            if (file.Length > 5 * 1024 * 1024)
                return Results.BadRequest(new { error = "Images must be 5 MB or smaller." });
            if (!Extensions.TryGetValue(file.ContentType, out var extension))
                return Results.BadRequest(new { error = "Only JPEG, PNG, and WebP images are accepted." });

            var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
            var relativeFolder = Path.Combine("uploads", "restaurants", restaurantId.ToString("N"));
            var folder = Path.Combine(webRoot, relativeFolder);
            Directory.CreateDirectory(folder);
            var filename = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, filename);

            await using (var stream = File.Create(fullPath))
                await file.CopyToAsync(stream, ct);

            var url = "/" + Path.Combine(relativeFolder, filename).Replace(Path.DirectorySeparatorChar, '/');
            return Results.Ok(new { url });
        }).RequireAuthorization("ApiBearer").WithTags("Media");
    }
}
