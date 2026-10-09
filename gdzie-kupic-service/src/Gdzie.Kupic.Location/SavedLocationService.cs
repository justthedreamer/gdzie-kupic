namespace Gdzie.Kupic.Location;

using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Storage;

internal sealed class SavedLocationService(
    ILocationStorage storage,
    ILocationInputResolver resolver) : ISavedLocationService
{
    public const int MaxDisplayNameLength = 100;

    public Task<IReadOnlyList<SavedLocation>> GetAsync(Guid userId, CancellationToken ct = default) =>
        storage.GetSavedLocationsAsync(userId, ct);

    public async Task<SavedLocationResult<SavedLocation>> CreateAsync(
        Guid userId,
        string? displayName,
        double? latitude,
        double? longitude,
        string? address,
        CancellationToken ct = default)
    {
        var name = displayName?.Trim() ?? string.Empty;
        if (name.Length == 0) return Invalid("Display name must not be blank.");
        if (name.Length > MaxDisplayNameLength)
            return Invalid($"Display name must not exceed {MaxDisplayNameLength} characters.");

        var resolved = await resolver.ResolveAsync(latitude, longitude, address);
        if (resolved.Error == LocationInputError.Validation) return Invalid(resolved.Message!);
        if (!resolved.IsSuccess)
            return new SavedLocationResult<SavedLocation>(null, SavedLocationError.GeocodingFailed, resolved.Message);

        var location = new SavedLocation(Guid.NewGuid(), userId, name, resolved.Coordinates!, resolved.AddressDisplayName, DateTimeOffset.UtcNow);
        await storage.AddSavedLocationAsync(location, ct);

        return new SavedLocationResult<SavedLocation>(location, SavedLocationError.None);
    }

    public Task<bool> DeleteAsync(Guid userId, Guid locationId, CancellationToken ct = default) =>
        storage.DeleteSavedLocationAsync(userId, locationId, ct);

    private static SavedLocationResult<SavedLocation> Invalid(string message) =>
        new(null, SavedLocationError.Validation, message);
}