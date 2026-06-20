namespace Gdzie.Kupic.Service.API.Contract.Location;

public class GetLocation
{
    public sealed record Request(string Latitude, string Longitude);

    public sealed record Response(string Voivodeship, string PostalCode, string City, string Country);
}