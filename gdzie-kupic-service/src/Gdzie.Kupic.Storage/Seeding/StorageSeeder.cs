using Gdzie.Kupic.Storage.Entities.Auth;
using Gdzie.Kupic.Storage.Entities.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gdzie.Kupic.Storage.Seeding;

internal sealed class StorageSeeder(
    AppDbContext db,
    IOptions<AdminSeedSettings> adminOptions,
    ILogger<StorageSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedAdminAsync(ct);
        await SeedCategoriesAsync(ct);
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        var settings = adminOptions.Value;

        var exists = await db.Users.AnyAsync(u => u.Email == settings.Email, ct);
        if (exists) return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(settings.Password);
        var admin = new User(Guid.NewGuid(), settings.Email, passwordHash, DateTimeOffset.UtcNow);

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded admin account {Email}", settings.Email);
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        // TODO: replace with predefined categories once catalogue is defined
        Category[] categories = [];

        if (categories.Length == 0) return;

        var existing = await db.Categories
            .Select(c => c.Name)
            .ToListAsync(ct);

        var toAdd = categories
            .Where(c => !existing.Contains(c.Name))
            .ToList();

        if (toAdd.Count == 0) return;

        db.Categories.AddRange(toAdd);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded {Count} categories", toAdd.Count);
    }
}
