namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Catalogue;
using Gdzie.Kupic.Service.API.Contract.Catalogue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/catalogue")]
public class CatalogueController(ICatalogueService catalogueService) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetCategories(CancellationToken ct)
    {
        var categories = await catalogueService.GetCategoriesAsync(ct);

        return Ok(categories.Select(CatalogueMapping.ToResponse).ToList());
    }
}