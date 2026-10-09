namespace Gdzie.Kupic.Location;

using Gdzie.Kupic.Domain.Model.Location;

public enum SavedLocationError
{
    None,
    Validation,
    NotFound,
    GeocodingFailed
}

public sealed record SavedLocationResult<T>(T? Value, SavedLocationError Error, string? Message = null)
{
    public bool IsSuccess => Error == SavedLocationError.None;
}

public interface ISavedLocationService
{
    Task<IReadOnlyList<SavedLocation>> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Exactly one of coordinates (latitude + longitude) or address must be supplied.</summary>
    Task<SavedLocationResult<SavedLocation>> CreateAsync(
        Guid userId,
        string? displayName,
        double? latitude,
        double? longitude,
        string? address,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid userId, Guid locationId, CancellationToken ct = default);
}