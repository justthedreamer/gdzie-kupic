namespace Gdzie.Kupic.Location;

using Gdzie.Kupic.Domain.Model.Location;

internal sealed class LocationInputResolver(ILocationService locationService) : ILocationInputResolver
{
    public async Task<ResolvedLocation> ResolveAsync(double? latitude, double? longitude, string? address)
    {
        var hasCoordinates = latitude.HasValue || longitude.HasValue;
        var hasAddress = !string.IsNullOrWhiteSpace(address);

        if (hasCoordinates == hasAddress)
            return Invalid("Provide either coordinates (latitude and longitude) or an address.");

        if (hasCoordinates)
        {
            if (latitude is null || longitude is null)
                return Invalid("Both latitude and longitude are required.");
            if (double.IsNaN(latitude.Value) || latitude is < -90 or > 90)
                return Invalid("Latitude must be between -90 and 90.");
            if (double.IsNaN(longitude.Value) || longitude is < -180 or > 180)
                return Invalid("Longitude must be between -180 and 180.");

            return new ResolvedLocation(new Coordinates(latitude.Value, longitude.Value), null, LocationInputError.None);
        }

        var geocoded = await locationService.GeocodeAddressAsync(address!.Trim());
        if (geocoded.Failure == GeocodeFailure.NotFound)
            return Invalid("The address could not be found.");
        if (!geocoded.IsSuccess)
            return new ResolvedLocation(
                null,
                null,
                LocationInputError.GeocodingFailed,
                "We can't resolve the address at the moment. Try again later or use your current location.");

        return new ResolvedLocation(
            new Coordinates(geocoded.Address!.Latitude, geocoded.Address.Longitude),
            geocoded.Address.FormattedAddress,
            LocationInputError.None);
    }

    private static ResolvedLocation Invalid(string message) =>
        new(null, null, LocationInputError.Validation, message);
}