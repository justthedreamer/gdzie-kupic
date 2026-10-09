namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Location;
using Microsoft.EntityFrameworkCore;

internal sealed class LocationStorage(AppDbContext db) : ILocationStorage
{
    public async Task<IReadOnlyList<SavedLocation>> GetSavedLocationsAsync(Guid userId, CancellationToken ct = default) =>
        await db.SavedLocations
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task AddSavedLocationAsync(SavedLocation location, CancellationToken ct = default)
    {
        db.SavedLocations.Add(location);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteSavedLocationAsync(Guid userId, Guid locationId, CancellationToken ct = default)
    {
        var location = await db.SavedLocations.SingleOrDefaultAsync(l => l.Id == locationId && l.UserId == userId, ct);
        if (location is null) return false;

        db.SavedLocations.Remove(location);
        await db.SaveChangesAsync(ct);
        return true;
    }
}