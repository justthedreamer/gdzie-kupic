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
    public async Task<GeocodeResult> GeocodeAddressAsync(string address)
    {
        var (response, thirdPartyError, internalError) =
            await client.ForwardGeocodeAsync(new ForwardGeocoding.Request(address));

        if (thirdPartyError || internalError)
        {
            return new GeocodeResult(null, GeocodeFailure.ProviderError);
        }

        var best = response?.Results?.FirstOrDefault(r => r.Location is not null);
        if (best?.Location is null)
        {
            logger.LogInformation("No geocoding results for the provided address");
            return new GeocodeResult(null, GeocodeFailure.NotFound);
        }

        return new GeocodeResult(
            new GeocodedAddress(best.Location.Latitude, best.Location.Longitude, best.FormattedAddress ?? address),
            GeocodeFailure.None);
    }
}
