namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Location;

public interface ILocationStorage
{
    Task<IReadOnlyList<SavedLocation>> GetSavedLocationsAsync(Guid userId, CancellationToken ct = default);

    Task AddSavedLocationAsync(SavedLocation location, CancellationToken ct = default);

    Task<bool> DeleteSavedLocationAsync(Guid userId, Guid locationId, CancellationToken ct = default);
}