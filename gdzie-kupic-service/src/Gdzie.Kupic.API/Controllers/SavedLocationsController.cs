namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Location;
using Gdzie.Kupic.Service.API.Contract.Location;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = nameof(Role.Buyer))]
[Route("api/saved-locations")]
public class SavedLocationsController(ISavedLocationService savedLocations) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SavedLocations.Response>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SavedLocations.Response>>> Get(CancellationToken ct)
    {
        var locations = await savedLocations.GetAsync(User.GetUserId(), ct);

        return Ok(locations.Select(ToResponse).ToList());
    }

    [HttpPost]
    [ProducesResponseType<SavedLocations.Response>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Create([FromBody] SavedLocations.CreateRequest request, CancellationToken ct)
    {
        var result = await savedLocations.CreateAsync(
            User.GetUserId(), request.DisplayName, request.Latitude, request.Longitude, request.Address, ct);

        if (result.Error == SavedLocationError.Validation)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation error", detail: result.Message);
        }

        if (!result.IsSuccess)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Geocoding failed", detail: result.Message);
        }

        return Created("/api/saved-locations", ToResponse(result.Value!));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await savedLocations.DeleteAsync(User.GetUserId(), id, ct);

        return deleted
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "Saved location not found.");
    }

    private static SavedLocations.Response ToResponse(SavedLocation l) =>
        new(l.Id, l.DisplayName, l.Coordinates.Latitude, l.Coordinates.Longitude, l.AddressDisplayName, l.CreatedAt);
}