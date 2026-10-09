namespace Gdzie.Kupic.Catalogue;

public enum CatalogueError
{
    None,
    Validation,
    NotFound,
    Conflict
}

public sealed record CatalogueResult<T>(T? Value, CatalogueError Error, string? Message = null)
{
    public bool IsSuccess => Error == CatalogueError.None;
}

public sealed record TagView(Guid Id, string Name, bool IsDisabled);

public sealed record CategoryView(Guid Id, string Name, bool IsDisabled, IReadOnlyList<TagView> Tags);

public interface ICatalogueService
{
    Task<IReadOnlyList<CategoryView>> GetCategoriesAsync(CancellationToken ct = default);

    Task<CatalogueResult<CategoryView>> CreateCategoryAsync(string name, CancellationToken ct = default);

    Task<CatalogueResult<CategoryView>> RenameCategoryAsync(Guid categoryId, string name, CancellationToken ct = default);

    Task<CatalogueResult<bool>> SetCategoryDisabledAsync(Guid categoryId, bool isDisabled, CancellationToken ct = default);

    Task<CatalogueResult<TagView>> CreateTagAsync(Guid categoryId, string name, CancellationToken ct = default);

    Task<CatalogueResult<TagView>> RenameTagAsync(Guid tagId, string name, CancellationToken ct = default);

    Task<CatalogueResult<bool>> SetTagDisabledAsync(Guid tagId, bool isDisabled, CancellationToken ct = default);
}