namespace Gdzie.Kupic.Location.Google;

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

internal class GoogleGeocodingHttpClient(
    ILogger<GoogleGeocodingHttpClient> logger,
    HttpClient client) : IGoogleGeocodingHttpClient
{
    public async Task<(ReverseGeocoding.Response Response, bool ThirdPartyError, bool InternalError)>
        ReverseGeocodeAsync(
            ReverseGeocoding.Request request)
    {
        try
        {
            var url = $"geocode/location/{request.Latitude},{request.Longitude}";
            client.DefaultRequestHeaders.Add("X-Goog-FieldMask", "results.addressComponents");

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Failed to get successful response from Google Geocoding API. Status code: {StatusCode}. Response content: {Content}",
                    response.StatusCode,
                    response.Content != null ? await response.Content.ReadAsStringAsync() : "No content");

                return (default, ThirdPartyError: true, InternalError: false);
            }

            var (content, error) = await this.TryDeserializeResponseAsync<ReverseGeocoding.Response>(response);

            if (error)
            {
                logger.LogError("Failed to deserialize response from Google Geocoding API. Response content: {Content}",
                    response.Content != null ? await response.Content.ReadAsStringAsync() : "No content");

                return (default, ThirdPartyError: false, InternalError: true);
            }

            return (content, false, false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<(ForwardGeocoding.Response? Response, bool ThirdPartyError, bool InternalError)>
        ForwardGeocodeAsync(ForwardGeocoding.Request request)
    {
        try
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Get,
                $"geocode/address/{Uri.EscapeDataString(request.Address)}");
            message.Headers.Add("X-Goog-FieldMask", "results.location,results.formattedAddress");

            using var response = await client.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Google Geocoding API (address) returned {StatusCode}. Response content: {Content}",
                    response.StatusCode,
                    await response.Content.ReadAsStringAsync());

                return (null, ThirdPartyError: true, InternalError: false);
            }

            var (content, error) = await this.TryDeserializeResponseAsync<ForwardGeocoding.Response>(response);

            return error ? (null, false, true) : (content, false, false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(e, "Google Geocoding API (address) request failed");
            return (null, ThirdPartyError: true, InternalError: false);
        }
    }

    private async Task<(TContent Result, bool error)> TryDeserializeResponseAsync<TContent>(
        HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadFromJsonAsync<TContent?>();
            return (content, false);
        }
        catch (Exception e)
        {
            logger.LogError("Failed to deserialize response: {Message}", e.Message);
            return (default, true);
        }
    }
}