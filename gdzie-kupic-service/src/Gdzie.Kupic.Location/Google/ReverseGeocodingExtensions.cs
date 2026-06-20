namespace Gdzie.Kupic.Location.Google;

internal static class ReverseGeocodingExtensions
{
    public static (string Voivodeship, string PostalCode, string City, string Country) TryGetLocationData(
        this ReverseGeocoding.Response.ResponseResults responseResults)
    {
        return (
            Voivodeship: GetValueOrUnknown(responseResults, "administrative_area_level_1"),
            PostalCode: GetValueOrUnknown(responseResults, "postal_code"),
            City: GetValueOrUnknown(responseResults, "locality"),
            Country: GetValueOrUnknown(responseResults, "country"));
    }

    private static string GetValueOrUnknown(ReverseGeocoding.Response.ResponseResults responseResults, string type)
    {
        var value = responseResults.AddressComponents.FirstOrDefault(ac => ac.Types.Contains(type))?.LongText;
        if (string.IsNullOrEmpty(value))
        {
            return "Unknown";
        }

        return value;
    }
}