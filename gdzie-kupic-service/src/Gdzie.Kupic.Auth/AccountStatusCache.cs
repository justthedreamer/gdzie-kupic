using Microsoft.Extensions.Caching.Memory;

namespace Gdzie.Kupic.Auth;

using Gdzie.Kupic.Storage;

public interface IAccountStatusCache
{
    /// <summary>
    /// Returns <c>true</c> when the account is banned or no longer exists, i.e. when its tokens must be rejected.
    /// </summary>
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

        // A token for a user that doesn't exist (e.g. deleted account) is rejected like a banned one.
        var isBanned = await authStorage.IsUserBannedAsync(userId, ct) ?? true;

        memoryCache.Set(
            CacheKey(userId),
            isBanned,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration });

        return isBanned;
    }
}
