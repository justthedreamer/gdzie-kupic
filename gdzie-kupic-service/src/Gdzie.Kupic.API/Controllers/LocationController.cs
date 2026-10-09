namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Location;
using Gdzie.Kupic.Service.API;
using Gdzie.Kupic.Service.API.Contract.Location;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/location")]
public class LocationController(ILocationService locationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<GetLocation.Response>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetLocation.Response>> GetLocation([FromQuery] GetLocation.Request request)
    {
        var (location, validationError, internalError) =
            await locationService.GetLocationAsync(request.Longitude, request.Latitude);

        if (validationError is not null)
        {
            return this.Problem(
                statusCode: 400,
                title: "Validation error",
                detail: validationError);
        }

        if (location  is null)
        {
            return this.Problem(
                statusCode: 404,
                title: "Not found",
                detail: "Location not found for the provided coordinates.");
        }

        if (internalError)
        {
            return this.Problem(
                statusCode: 500,
                title: "Internal error",
                detail: Constants.INTERNAL_ERROR_MESSAGES.LOCATION_FETCH_ERROR);
        }

        return this.Ok(new GetLocation.Response(
            Voivodeship: location.Voivodeship,
            PostalCode: location.PostalCode,
            City: location.City,
            Country: location.Country));
    }

    /// <summary>
    /// Resolves a typed address to coordinates without storing anything, so clients can show the
    /// match to the user before they save it.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType<SearchAddress.Response>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SearchAddress.Response>> Search([FromQuery] SearchAddress.Request request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return this.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: "Address is required.");
        }

        var result = await locationService.GeocodeAddressAsync(request.Address.Trim());

        if (result.Failure == GeocodeFailure.NotFound)
        {
            return this.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not found",
                detail: "The address could not be found.");
        }

        if (!result.IsSuccess)
        {
            return this.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Geocoding failed",
                detail: "We can't resolve the address at the moment. Try again later or use your current location.");
        }

        return this.Ok(new SearchAddress.Response(
            result.Address!.Latitude,
            result.Address.Longitude,
            result.Address.FormattedAddress));
    }
}