namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Catalogue;
using Gdzie.Kupic.Service.API.Contract.Catalogue;
using Microsoft.AspNetCore.Mvc;

internal static class CatalogueMapping
{
    public static TagResponse ToResponse(TagView tag) => new(tag.Id, tag.Name, tag.IsDisabled);

    public static CategoryResponse ToResponse(CategoryView category) =>
        new(category.Id, category.Name, category.IsDisabled, category.Tags.Select(ToResponse).ToList());

    public static IActionResult ToProblem<T>(ControllerBase controller, CatalogueResult<T> result)
    {
        var (status, title) = result.Error switch
        {
            CatalogueError.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            CatalogueError.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status400BadRequest, "Validation error"),
        };

        return controller.Problem(statusCode: status, title: title, detail: result.Message);
    }
}