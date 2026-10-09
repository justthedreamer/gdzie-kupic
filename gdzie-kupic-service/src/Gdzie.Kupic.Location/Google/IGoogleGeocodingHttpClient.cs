namespace Gdzie.Kupic.Location.Google;

internal interface IGoogleGeocodingHttpClient
{
    Task<(ReverseGeocoding.Response Response, bool ThirdPartyError, bool InternalError)> ReverseGeocodeAsync(
        ReverseGeocoding.Request request);

    Task<(ForwardGeocoding.Response? Response, bool ThirdPartyError, bool InternalError)> ForwardGeocodeAsync(
        ForwardGeocoding.Request request);
}
