using Microsoft.Extensions.Caching.Memory;

namespace Gdzie.Kupic.Auth;

using Gdzie.Kupic.Storage;

public interface IAccountStatusCache
{
    Task<bool> IsBannedAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Caches a user's banned/active status in-process for a short, fixed window so that account
/// status enforcement (checked on every authenticated request) doesn't require a DB round-trip
/// per request. Cache entries expire after an absolute (not sliding) window.
/// </summary>
public sealed class AccountStatusCache(
    IAuthStorage authStorage,
    IMemoryCache memoryCache) : IAccountStatusCache
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private static string CacheKey(Guid userId) => $"account-status:{userId}";

    public async Task<bool> IsBannedAsync(Guid userId, CancellationToken ct = default)
    {
        if (memoryCache.TryGetValue(CacheKey(userId), out bool cachedIsBanned))
        {
            return cachedIsBanned;
        }

        var isBanned = await authStorage.IsUserBannedAsync(userId, ct);

        memoryCache.Set(
            CacheKey(userId),
            isBanned,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration });

        return isBanned;
    }
}
