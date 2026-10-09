using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace Gdzie.Kupic.Catalogue;

/// <summary>
/// Admin writes go straight to storage and invalidate the cached taxonomy; reads are served from
/// an in-memory cache that also expires on its own after <see cref="CacheDuration"/>.
/// </summary>
internal sealed class CatalogueService(
    ICatalogueStorage storage,
    IMemoryCache cache) : ICatalogueService
{
    public const string CacheKey = "catalogue:categories";
    public const int MaxNameLength = 100;
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<CategoryView>> GetCategoriesAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<CategoryView>? cached) && cached is not null)
        {
            return cached;
        }

        var categories = await storage.GetCategoriesWithTagsAsync(ct);
        var views = categories
            .Select(c => new CategoryView(
                c.Id,
                c.Name,
                c.IsDisabled,
                c.Tags.OrderBy(t => t.Name, StringComparer.CurrentCulture)
                    .Select(t => new TagView(t.Id, t.Name, t.IsDisabled))
                    .ToList()))
            .ToList();

        cache.Set(CacheKey, (IReadOnlyList<CategoryView>)views,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration });

        return views;
    }

    public async Task<CatalogueResult<CategoryView>> CreateCategoryAsync(string name, CancellationToken ct = default)
    {
        if (!TryNormalize(name, out var normalized, out var error)) return Fail<CategoryView>(error!);

        if (await storage.CategoryNameExistsAsync(normalized, null, ct))
            return Conflict<CategoryView>("A category with this name already exists.");

        var category = new Category(Guid.NewGuid(), normalized, false, DateTimeOffset.UtcNow);
        await storage.AddCategoryAsync(category, ct);
        Invalidate();

        return Ok(ToView(category));
    }

    public async Task<CatalogueResult<CategoryView>> RenameCategoryAsync(Guid categoryId, string name, CancellationToken ct = default)
    {
        if (!TryNormalize(name, out var normalized, out var error)) return Fail<CategoryView>(error!);

        var category = await storage.FindCategoryAsync(categoryId, ct);
        if (category is null) return NotFound<CategoryView>("Category not found.");

        if (await storage.CategoryNameExistsAsync(normalized, categoryId, ct))
            return Conflict<CategoryView>("A category with this name already exists.");

        category.Name = normalized;
        await storage.SaveChangesAsync(ct);
        Invalidate();

        return Ok(ToView(category));
    }

    public async Task<CatalogueResult<bool>> SetCategoryDisabledAsync(Guid categoryId, bool isDisabled, CancellationToken ct = default)
    {
        var category = await storage.FindCategoryAsync(categoryId, ct);
        if (category is null) return NotFound<bool>("Category not found.");

        category.IsDisabled = isDisabled;
        await storage.SaveChangesAsync(ct);
        Invalidate();

        return Ok(true);
    }

    public async Task<CatalogueResult<TagView>> CreateTagAsync(Guid categoryId, string name, CancellationToken ct = default)
    {
        if (!TryNormalize(name, out var normalized, out var error)) return Fail<TagView>(error!);

        var category = await storage.FindCategoryAsync(categoryId, ct);
        if (category is null) return NotFound<TagView>("Category not found.");

        if (await storage.TagNameExistsAsync(categoryId, normalized, null, ct))
            return Conflict<TagView>("A tag with this name already exists in the category.");

        var tag = new Tag(Guid.NewGuid(), categoryId, normalized, false, DateTimeOffset.UtcNow);
        await storage.AddTagAsync(tag, ct);
        Invalidate();

        return Ok(new TagView(tag.Id, tag.Name, tag.IsDisabled));
    }

    public async Task<CatalogueResult<TagView>> RenameTagAsync(Guid tagId, string name, CancellationToken ct = default)
    {
        if (!TryNormalize(name, out var normalized, out var error)) return Fail<TagView>(error!);

        var tag = await storage.FindTagAsync(tagId, ct);
        if (tag is null) return NotFound<TagView>("Tag not found.");

        if (await storage.TagNameExistsAsync(tag.CategoryId, normalized, tagId, ct))
            return Conflict<TagView>("A tag with this name already exists in the category.");

        tag.Name = normalized;
        await storage.SaveChangesAsync(ct);
        Invalidate();

        return Ok(new TagView(tag.Id, tag.Name, tag.IsDisabled));
    }

    public async Task<CatalogueResult<bool>> SetTagDisabledAsync(Guid tagId, bool isDisabled, CancellationToken ct = default)
    {
        var tag = await storage.FindTagAsync(tagId, ct);
        if (tag is null) return NotFound<bool>("Tag not found.");

        tag.IsDisabled = isDisabled;
        await storage.SaveChangesAsync(ct);
        Invalidate();

        return Ok(true);
    }

    private void Invalidate() => cache.Remove(CacheKey);

    private static CategoryView ToView(Category category) =>
        new(category.Id, category.Name, category.IsDisabled, []);

    private static bool TryNormalize(string? name, out string normalized, out string? error)
    {
        normalized = name?.Trim() ?? string.Empty;
        error = null;

        if (normalized.Length == 0) error = "Name must not be blank.";
        else if (normalized.Length > MaxNameLength) error = $"Name must not exceed {MaxNameLength} characters.";

        return error is null;
    }

    private static CatalogueResult<T> Ok<T>(T value) => new(value, CatalogueError.None);
    private static CatalogueResult<T> Fail<T>(string message) => new(default, CatalogueError.Validation, message);
    private static CatalogueResult<T> NotFound<T>(string message) => new(default, CatalogueError.NotFound, message);
    private static CatalogueResult<T> Conflict<T>(string message) => new(default, CatalogueError.Conflict, message);
}