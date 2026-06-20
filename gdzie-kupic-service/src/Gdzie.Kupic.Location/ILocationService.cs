namespace Gdzie.Kupic.Location;

public interface ILocationService
{
    public record LocationData(string Voivodeship, string PostalCode, string City, string Country);

    public Task<(LocationData? Location, string? ValidationError, bool InternalError)> GetLocationAsync(
        string longitude,
        string latitude);
}