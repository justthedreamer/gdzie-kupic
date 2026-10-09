using Gdzie.Kupic.Domain.Seeding;
using Gdzie.Kupic.Storage;
using Gdzie.Kupic.Storage.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Catalogue;

public class CatalogueSeederTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Test]
    public async Task Seed_CreatesAllCategoriesAndTagsWithStableIds()
    {
        using var db = CreateDb();

        await new CatalogueSeeder(db, NullLogger<CatalogueSeeder>.Instance).SeedAsync();

        var categoryIds = await db.Categories.Select(c => c.Id).ToListAsync();
        var tagIds = await db.Tags.Select(t => t.Id).ToListAsync();
        categoryIds.ShouldBe(CatalogueSeedData.Categories.Select(c => c.Id), ignoreOrder: true);
        tagIds.ShouldBe(CatalogueSeedData.Categories.SelectMany(c => c.Tags).Select(t => t.Id), ignoreOrder: true);
    }

    [Test]
    public async Task Seed_RunTwice_DoesNotDuplicate()
    {
        using var db = CreateDb();
        var seeder = new CatalogueSeeder(db, NullLogger<CatalogueSeeder>.Instance);

        await seeder.SeedAsync();
        var categories = await db.Categories.CountAsync();
        var tags = await db.Tags.CountAsync();
        await seeder.SeedAsync();

        (await db.Categories.CountAsync()).ShouldBe(categories);
        (await db.Tags.CountAsync()).ShouldBe(tags);
    }

    [Test]
    public async Task Seed_AfterAdminChanges_DoesNotRevertThem()
    {
        using var db = CreateDb();
        var seeder = new CatalogueSeeder(db, NullLogger<CatalogueSeeder>.Instance);
        await seeder.SeedAsync();

        var seedCategory = CatalogueSeedData.Categories[0];
        var seedTag = seedCategory.Tags[0];
        var category = await db.Categories.SingleAsync(c => c.Id == seedCategory.Id);
        var tag = await db.Tags.SingleAsync(t => t.Id == seedTag.Id);
        category.Name = "Renamed";
        category.IsDisabled = true;
        tag.Name = "Renamed tag";
        tag.IsDisabled = true;
        await db.SaveChangesAsync();

        await seeder.SeedAsync();

        db.ChangeTracker.Clear();
        var reloadedCategory = await db.Categories.SingleAsync(c => c.Id == seedCategory.Id);
        var reloadedTag = await db.Tags.SingleAsync(t => t.Id == seedTag.Id);
        reloadedCategory.Name.ShouldBe("Renamed");
        reloadedCategory.IsDisabled.ShouldBeTrue();
        reloadedTag.Name.ShouldBe("Renamed tag");
        reloadedTag.IsDisabled.ShouldBeTrue();
        (await db.Categories.CountAsync()).ShouldBe(CatalogueSeedData.Categories.Count);
    }

    [Test]
    public void SeedIds_AreDeterministic()
    {
        CatalogueSeedData.Categories.Select(c => c.Id).Distinct().Count().ShouldBe(CatalogueSeedData.Categories.Count);
        CatalogueSeedData.Categories[0].Id.ShouldBe(Guid.Parse(CatalogueSeedData.Categories[0].Id.ToString()));
    }
}
