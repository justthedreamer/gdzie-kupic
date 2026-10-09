using Gdzie.Kupic.Location.Google;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>Replaces the Google HTTP client so tests never reach the external geocoding provider.</summary>
internal sealed class FakeGeocodingClient : IGoogleGeocodingHttpClient
{
    public int ForwardCalls { get; private set; }

    public Func<string, (ForwardGeocoding.Response? Response, bool ThirdPartyError, bool InternalError)> Forward { get; set; } =
        _ => (new ForwardGeocoding.Response([]), false, false);

    public void Reset()
    {
        ForwardCalls = 0;
        Forward = _ => (new ForwardGeocoding.Response([]), false, false);
    }

    public Task<(ReverseGeocoding.Response Response, bool ThirdPartyError, bool InternalError)> ReverseGeocodeAsync(
        ReverseGeocoding.Request request) =>
        Task.FromResult<(ReverseGeocoding.Response, bool, bool)>((new ReverseGeocoding.Response([]), false, false));

    public Task<(ForwardGeocoding.Response? Response, bool ThirdPartyError, bool InternalError)> ForwardGeocodeAsync(
        ForwardGeocoding.Request request)
    {
        ForwardCalls++;
        return Task.FromResult(Forward(request.Address));
    }
}