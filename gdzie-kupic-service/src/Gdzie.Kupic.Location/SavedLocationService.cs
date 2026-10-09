namespace Gdzie.Kupic.Location;

using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Storage;

internal sealed class SavedLocationService(
    ILocationStorage storage,
    ILocationService locationService) : ISavedLocationService
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

        var hasCoordinates = latitude.HasValue || longitude.HasValue;
        var hasAddress = !string.IsNullOrWhiteSpace(address);

        if (hasCoordinates == hasAddress)
            return Invalid("Provide either coordinates (latitude and longitude) or an address.");

        Coordinates coordinates;
        if (hasCoordinates)
        {
            if (latitude is null || longitude is null)
                return Invalid("Both latitude and longitude are required.");
            if (latitude is < -90 or > 90 || double.IsNaN(latitude.Value))
                return Invalid("Latitude must be between -90 and 90.");
            if (longitude is < -180 or > 180 || double.IsNaN(longitude.Value))
                return Invalid("Longitude must be between -180 and 180.");

            coordinates = new Coordinates(latitude.Value, longitude.Value);
        }
        else
        {
            var geocoded = await locationService.GeocodeAddressAsync(address!.Trim());
            if (geocoded.Failure == GeocodeFailure.NotFound)
                return Invalid("The address could not be found.");
            if (!geocoded.IsSuccess)
                return new SavedLocationResult<SavedLocation>(
                    null,
                    SavedLocationError.GeocodingFailed,
                    "We can't resolve the address at the moment. Try again later or use your current location.");

            coordinates = new Coordinates(geocoded.Address!.Latitude, geocoded.Address.Longitude);
        }

        var location = new SavedLocation(Guid.NewGuid(), userId, name, coordinates, DateTimeOffset.UtcNow);
        await storage.AddSavedLocationAsync(location, ct);

        return new SavedLocationResult<SavedLocation>(location, SavedLocationError.None);
    }

    public Task<bool> DeleteAsync(Guid userId, Guid locationId, CancellationToken ct = default) =>
        storage.DeleteSavedLocationAsync(userId, locationId, ct);

    private static SavedLocationResult<SavedLocation> Invalid(string message) =>
        new(null, SavedLocationError.Validation, message);
}