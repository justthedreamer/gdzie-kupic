namespace Gdzie.Kupic.Location;

using Gdzie.Kupic.Domain.Model.Location;

public enum LocationInputError
{
    None,
    Validation,
    GeocodingFailed
}

/// <param name="AddressDisplayName">
/// Human-readable address: the formatted address when an address was geocoded, or the postal code, city and
/// country found by reverse geocoding when coordinates were supplied. Null when it could not be determined.
/// </param>
public sealed record ResolvedLocation(
    Coordinates? Coordinates,
    string? AddressDisplayName,
    LocationInputError Error,
    string? Message = null)
{
    public bool IsSuccess => Error == LocationInputError.None;
}

/// <summary>
/// Turns either coordinates or a typed address into coordinates. Exactly one input method must be
/// supplied; addresses are geocoded here (once, at save time).
/// </summary>
public interface ILocationInputResolver
{
    Task<ResolvedLocation> ResolveAsync(double? latitude, double? longitude, string? address);
}