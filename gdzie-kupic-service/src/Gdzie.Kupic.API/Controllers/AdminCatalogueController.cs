namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Catalogue;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Service.API.Contract.Catalogue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = nameof(Role.Admin))]
[Route("api/admin")]
public class AdminCatalogueController(ICatalogueService catalogueService) : ControllerBase
{
    [HttpPost("categories")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory([FromBody] NameRequest request, CancellationToken ct)
    {
        var result = await catalogueService.CreateCategoryAsync(request.Name, ct);
        if (!result.IsSuccess) return CatalogueMapping.ToProblem(this, result);

        return Created($"/api/catalogue/categories", CatalogueMapping.ToResponse(result.Value!));
    }

    [HttpPut("categories/{categoryId:guid}")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenameCategory(Guid categoryId, [FromBody] NameRequest request, CancellationToken ct)
    {
        var result = await catalogueService.RenameCategoryAsync(categoryId, request.Name, ct);

        return result.IsSuccess ? Ok(CatalogueMapping.ToResponse(result.Value!)) : CatalogueMapping.ToProblem(this, result);
    }

    [HttpPost("categories/{categoryId:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DisableCategory(Guid categoryId, CancellationToken ct) =>
        SetCategoryDisabled(categoryId, true, ct);

    [HttpPost("categories/{categoryId:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> EnableCategory(Guid categoryId, CancellationToken ct) =>
        SetCategoryDisabled(categoryId, false, ct);

    [HttpPost("categories/{categoryId:guid}/tags")]
    [ProducesResponseType<TagResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTag(Guid categoryId, [FromBody] NameRequest request, CancellationToken ct)
    {
        var result = await catalogueService.CreateTagAsync(categoryId, request.Name, ct);
        if (!result.IsSuccess) return CatalogueMapping.ToProblem(this, result);

        return Created($"/api/catalogue/categories", CatalogueMapping.ToResponse(result.Value!));
    }

    [HttpPut("tags/{tagId:guid}")]
    [ProducesResponseType<TagResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenameTag(Guid tagId, [FromBody] NameRequest request, CancellationToken ct)
    {
        var result = await catalogueService.RenameTagAsync(tagId, request.Name, ct);

        return result.IsSuccess ? Ok(CatalogueMapping.ToResponse(result.Value!)) : CatalogueMapping.ToProblem(this, result);
    }

    [HttpPost("tags/{tagId:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DisableTag(Guid tagId, CancellationToken ct) => SetTagDisabled(tagId, true, ct);

    [HttpPost("tags/{tagId:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> EnableTag(Guid tagId, CancellationToken ct) => SetTagDisabled(tagId, false, ct);

    private async Task<IActionResult> SetCategoryDisabled(Guid categoryId, bool isDisabled, CancellationToken ct)
    {
        var result = await catalogueService.SetCategoryDisabledAsync(categoryId, isDisabled, ct);

        return result.IsSuccess ? NoContent() : CatalogueMapping.ToProblem(this, result);
    }

    private async Task<IActionResult> SetTagDisabled(Guid tagId, bool isDisabled, CancellationToken ct)
    {
        var result = await catalogueService.SetTagDisabledAsync(tagId, isDisabled, ct);

        return result.IsSuccess ? NoContent() : CatalogueMapping.ToProblem(this, result);
    }
}