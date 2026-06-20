namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Location;
using Gdzie.Kupic.Service.API;
using Gdzie.Kupic.Service.API.Contract.Location;
using Microsoft.AspNetCore.Mvc;

[Route("api/location")]
public class LocationController(ILocationService locationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GetLocation.Response>> GetLocation([FromBody] GetLocation.Request request)
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

        if (locationService is null)
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
}