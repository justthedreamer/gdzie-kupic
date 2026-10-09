namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Catalogue;

public interface ICatalogueStorage
{
    Task<IReadOnlyList<Category>> GetCategoriesWithTagsAsync(CancellationToken ct = default);

    Task<Category?> FindCategoryAsync(Guid categoryId, CancellationToken ct = default);

    Task<Tag?> FindTagAsync(Guid tagId, CancellationToken ct = default);

    Task<bool> CategoryNameExistsAsync(string name, Guid? excludeCategoryId, CancellationToken ct = default);

    Task<bool> TagNameExistsAsync(Guid categoryId, string name, Guid? excludeTagId, CancellationToken ct = default);

    Task AddCategoryAsync(Category category, CancellationToken ct = default);

    Task AddTagAsync(Tag tag, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}