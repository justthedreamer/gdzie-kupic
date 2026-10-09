using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gdzie.Kupic.Storage.Seeding;

/// <summary>
/// Idempotent catalogue provisioning: inserts seed rows by their stable ID only when missing, so
/// later admin changes (renames, disabled flags) to seeded rows are never overwritten.
/// </summary>
internal sealed class CatalogueSeeder(AppDbContext db, ILogger<CatalogueSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existingCategoryIds = (await db.Categories.Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var existingTagIds = (await db.Tags.Select(t => t.Id).ToListAsync(ct)).ToHashSet();
        var now = DateTimeOffset.UtcNow;
        var added = 0;

        foreach (var seedCategory in CatalogueSeedData.Categories)
        {
            if (existingCategoryIds.Add(seedCategory.Id))
            {
                db.Categories.Add(new Category(seedCategory.Id, seedCategory.Name, false, now));
                added++;
            }

            foreach (var seedTag in seedCategory.Tags)
            {
                if (existingTagIds.Add(seedTag.Id))
                {
                    db.Tags.Add(new Tag(seedTag.Id, seedCategory.Id, seedTag.Name, false, now));
                    added++;
                }
            }
        }

        if (added == 0) return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} catalogue rows", added);
    }
}
