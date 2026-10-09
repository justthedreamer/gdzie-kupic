namespace Gdzie.Kupic.Location;

using Gdzie.Kupic.Domain.Model.Location;

public enum LocationInputError
{
    None,
    Validation,
    GeocodingFailed
}

/// <param name="AddressDisplayName">Human-readable address; set only when the location came from geocoding an address.</param>
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