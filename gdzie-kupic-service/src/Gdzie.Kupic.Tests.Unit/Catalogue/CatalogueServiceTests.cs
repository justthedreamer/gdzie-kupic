using Gdzie.Kupic.Catalogue;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Catalogue;

public class CatalogueServiceTests
{
    private AppDbContext _db = null!;
    private MemoryCache _cache = null!;
    private CatalogueService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        _service = new CatalogueService(new CatalogueStorage(_db), _cache);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    [Test]
    public async Task CreateCategory_DuplicateName_ReturnsConflict()
    {
        await _service.CreateCategoryAsync("Audio");

        var result = await _service.CreateCategoryAsync(" audio ");

        result.Error.ShouldBe(CatalogueError.Conflict);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateCategory_BlankName_ReturnsValidationError(string name)
    {
        var result = await _service.CreateCategoryAsync(name);

        result.Error.ShouldBe(CatalogueError.Validation);
    }

    [Test]
    public async Task RenameCategory_ToExistingName_ReturnsConflict()
    {
        await _service.CreateCategoryAsync("Audio");
        var other = (await _service.CreateCategoryAsync("Video")).Value!;

        var result = await _service.RenameCategoryAsync(other.Id, "Audio");

        result.Error.ShouldBe(CatalogueError.Conflict);
    }

    [Test]
    public async Task RenameCategory_ToOwnName_Succeeds()
    {
        var category = (await _service.CreateCategoryAsync("Audio")).Value!;

        var result = await _service.RenameCategoryAsync(category.Id, "Audio");

        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task RenameCategory_Unknown_ReturnsNotFound()
    {
        var result = await _service.RenameCategoryAsync(Guid.NewGuid(), "X");

        result.Error.ShouldBe(CatalogueError.NotFound);
    }

    [Test]
    public async Task CreateTag_DuplicateWithinCategory_ReturnsConflict()
    {
        var category = (await _service.CreateCategoryAsync("Audio")).Value!;
        await _service.CreateTagAsync(category.Id, "Microphone");

        var result = await _service.CreateTagAsync(category.Id, "microphone");

        result.Error.ShouldBe(CatalogueError.Conflict);
    }

    [Test]
    public async Task CreateTag_SameNameInDifferentCategories_Succeeds()
    {
        var audio = (await _service.CreateCategoryAsync("Audio")).Value!;
        var video = (await _service.CreateCategoryAsync("Video")).Value!;
        await _service.CreateTagAsync(audio.Id, "Cable");

        var result = await _service.CreateTagAsync(video.Id, "Cable");

        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task CreateTag_UnknownCategory_ReturnsNotFound()
    {
        var result = await _service.CreateTagAsync(Guid.NewGuid(), "Cable");

        result.Error.ShouldBe(CatalogueError.NotFound);
    }

    [Test]
    public async Task RenameTag_ToExistingNameInSameCategory_ReturnsConflict()
    {
        var category = (await _service.CreateCategoryAsync("Audio")).Value!;
        await _service.CreateTagAsync(category.Id, "Microphone");
        var tag = (await _service.CreateTagAsync(category.Id, "Speaker")).Value!;

        var result = await _service.RenameTagAsync(tag.Id, "Microphone");

        result.Error.ShouldBe(CatalogueError.Conflict);
    }

    [Test]
    public async Task DisableAndEnableTag_IsReversibleAndKeepsOtherData()
    {
        var category = (await _service.CreateCategoryAsync("Audio")).Value!;
        var tag = (await _service.CreateTagAsync(category.Id, "Microphone")).Value!;

        await _service.SetTagDisabledAsync(tag.Id, true);
        var disabled = (await _service.GetCategoriesAsync()).Single().Tags.Single();
        await _service.SetTagDisabledAsync(tag.Id, false);
        var enabled = (await _service.GetCategoriesAsync()).Single().Tags.Single();

        disabled.IsDisabled.ShouldBeTrue();
        disabled.Name.ShouldBe("Microphone");
        enabled.IsDisabled.ShouldBeFalse();
        (await _db.Categories.SingleAsync()).IsDisabled.ShouldBeFalse();
    }

    [Test]
    public async Task DisableCategory_DoesNotTouchItsTags()
    {
        var category = (await _service.CreateCategoryAsync("Audio")).Value!;
        await _service.CreateTagAsync(category.Id, "Microphone");

        await _service.SetCategoryDisabledAsync(category.Id, true);

        var result = (await _service.GetCategoriesAsync()).Single();
        result.IsDisabled.ShouldBeTrue();
        result.Tags.Single().IsDisabled.ShouldBeFalse();
    }

    [Test]
    public async Task Disable_Unknown_ReturnsNotFound()
    {
        (await _service.SetCategoryDisabledAsync(Guid.NewGuid(), true)).Error.ShouldBe(CatalogueError.NotFound);
        (await _service.SetTagDisabledAsync(Guid.NewGuid(), true)).Error.ShouldBe(CatalogueError.NotFound);
    }

    [Test]
    public async Task GetCategories_OrderedByName_WithTagsOrderedByName()
    {
        var b = (await _service.CreateCategoryAsync("B")).Value!;
        await _service.CreateCategoryAsync("A");
        await _service.CreateTagAsync(b.Id, "Z");
        await _service.CreateTagAsync(b.Id, "Y");

        var result = await _service.GetCategoriesAsync();

        result.Select(c => c.Name).ShouldBe(["A", "B"]);
        result[1].Tags.Select(t => t.Name).ShouldBe(["Y", "Z"]);
    }

    [Test]
    public async Task GetCategories_IsServedFromCacheUntilAnAdminWrite()
    {
        var category = (await _service.CreateCategoryAsync("Audio")).Value!;
        await _service.GetCategoriesAsync();

        // Change bypassing the service: the cached result must still be returned.
        _db.Categories.Add(new Category(Guid.NewGuid(), "Sneaky", false, DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync();
        (await _service.GetCategoriesAsync()).Count.ShouldBe(1);

        await _service.RenameCategoryAsync(category.Id, "Audio2");

        var refreshed = await _service.GetCategoriesAsync();
        refreshed.Select(c => c.Name).ShouldBe(["Audio2", "Sneaky"]);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task EveryAdminWrite_InvalidatesCache(bool tagWrite)
    {
        var category = (await _service.CreateCategoryAsync("Audio")).Value!;
        var tag = (await _service.CreateTagAsync(category.Id, "Mic")).Value!;

        await _service.GetCategoriesAsync();
        if (tagWrite) await _service.SetTagDisabledAsync(tag.Id, true);
        else await _service.SetCategoryDisabledAsync(category.Id, true);

        var result = (await _service.GetCategoriesAsync()).Single();
        (tagWrite ? result.Tags.Single().IsDisabled : result.IsDisabled).ShouldBeTrue();
    }
}