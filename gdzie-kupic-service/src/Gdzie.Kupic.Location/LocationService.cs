namespace Gdzie.Kupic.Location;

using Gdzie.Kupic.Location.Google;
using Microsoft.Extensions.Logging;

internal class LocationService(
    ILogger<LocationService> logger,
    IGoogleGeocodingHttpClient client) : ILocationService
{
    public async Task<(ILocationService.LocationData? Location, string? ValidationError, bool InternalError)>
        GetLocationAsync(
            string longitude,
            string latitude)
    {
        logger.LogInformation("Getting location for coordinates: {Longitude}, {Latitude}", longitude, latitude);

        if (string.IsNullOrEmpty(longitude) || string.IsNullOrEmpty(latitude))
        {
            return (
                Loction: default,
                ValidationError: $"Parameters {nameof(longitude)} and ${nameof(latitude)} cannot be empty.",
                InternalError: false);
        }

        var (response, thirdPartyError, internalError) =
            await client.ReverseGeocodeAsync(new ReverseGeocoding.Request(longitude, latitude));

        if (thirdPartyError || internalError)
        {
            return (
                Loction: default,
                ValidationError: null,
                InternalError: true);
        }

        if (!response.Results.Any())
        {
            logger.LogWarning("No results found for coordinates: {Longitude}, {Latitude}", longitude, latitude);

            return (
                Loction: default,
                ValidationError: null,
                InternalError: false);
        }

        // Set "Unknown" for any empty fields to ensure consistent output
        // We're getting first result as Google returns results ordered by relevance, so the first one should be the most accurate?
        var (voivodeship, postalCode, city, country) = response.Results.First().TryGetLocationData();

        return (
            Loction: new ILocationService.LocationData(
                Voivodeship: voivodeship,
                PostalCode: postalCode,
                City: city,
                Country: country),
            ValidationError: null,
            InternalError: false);
    }
}