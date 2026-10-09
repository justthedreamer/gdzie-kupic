namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Catalogue;
using Microsoft.EntityFrameworkCore;

internal sealed class CatalogueStorage(AppDbContext db) : ICatalogueStorage
{
    public async Task<IReadOnlyList<Category>> GetCategoriesWithTagsAsync(CancellationToken ct = default) =>
        await db.Categories
            .AsNoTracking()
            .Include(c => c.Tags)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public Task<Category?> FindCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
        db.Categories.SingleOrDefaultAsync(c => c.Id == categoryId, ct);

    public Task<Tag?> FindTagAsync(Guid tagId, CancellationToken ct = default) =>
        db.Tags.SingleOrDefaultAsync(t => t.Id == tagId, ct);

    public Task<bool> CategoryNameExistsAsync(string name, Guid? excludeCategoryId, CancellationToken ct = default)
    {
        var lowered = name.ToLower();
        return db.Categories.AnyAsync(c => c.Name.ToLower() == lowered && c.Id != excludeCategoryId, ct);
    }

    public Task<bool> TagNameExistsAsync(Guid categoryId, string name, Guid? excludeTagId, CancellationToken ct = default)
    {
        var lowered = name.ToLower();
        return db.Tags.AnyAsync(
            t => t.CategoryId == categoryId && t.Name.ToLower() == lowered && t.Id != excludeTagId, ct);
    }

    public async Task AddCategoryAsync(Category category, CancellationToken ct = default)
    {
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddTagAsync(Tag tag, CancellationToken ct = default)
    {
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}