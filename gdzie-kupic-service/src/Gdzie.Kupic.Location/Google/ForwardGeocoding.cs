namespace Gdzie.Kupic.Location.Google;

internal abstract class ForwardGeocoding
{
    public record Request(string Address);

    public record Response(Response.ResponseResult[]? Results)
    {
        public sealed record ResponseResult(LatLng? Location, string? FormattedAddress);

        public sealed record LatLng(double Latitude, double Longitude);
    }
}