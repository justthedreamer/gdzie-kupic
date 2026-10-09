namespace Gdzie.Kupic.Location;

public enum GeocodeFailure
{
    None,
    NotFound,
    ProviderError
}

public sealed record GeocodedAddress(double Latitude, double Longitude, string FormattedAddress);

public sealed record GeocodeResult(GeocodedAddress? Address, GeocodeFailure Failure)
{
    public bool IsSuccess => Failure == GeocodeFailure.None;
}

public interface ILocationService
{
    public record LocationData(string Voivodeship, string PostalCode, string City, string Country);

    public Task<(LocationData? Location, string? ValidationError, bool InternalError)> GetLocationAsync(
        string longitude,
        string latitude);

    /// <summary>Forward geocoding (address to coordinates); intended to be called once, at save time.</summary>
    Task<GeocodeResult> GeocodeAddressAsync(string address);
}