namespace Gdzie.Kupic.Location.Google;

internal abstract class ReverseGeocoding
{
    public record Request(string Longitude, string Latitude);

    public record Response(Response.ResponseResults[] Results)
    {
        public sealed record ResponseResults(AddressComponent[] AddressComponents);

        public sealed record AddressComponent(
            string LongText,
            string ShortText,
            string[] Types
        );
    }
}