namespace Gdzie.Kupic.Service.API.Contract.Location;

public sealed class SearchAddress
{
    public sealed record Request(string? Address);

    public sealed record Response(double Latitude, double Longitude, string FormattedAddress);
}
