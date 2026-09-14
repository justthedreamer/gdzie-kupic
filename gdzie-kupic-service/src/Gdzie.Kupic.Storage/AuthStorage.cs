namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Auth;
using Microsoft.EntityFrameworkCore;

internal sealed class AuthStorage(AppDbContext db) : IAuthStorage
{
    public Task<User?> FindUserByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> UserEmailExistsAsync(string email, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Email == email, ct);

    public async Task AddUserAsync(User user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct = default)
    {
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken refreshToken, DateTimeOffset revokedAt, CancellationToken ct = default)
    {
        db.Entry(refreshToken).Property(t => t.RevokedAt).CurrentValue = revokedAt;
        await db.SaveChangesAsync(ct);
    }

    public Task<User?> FindUserByExternalLoginAsync(string provider, string providerKey, CancellationToken ct = default) =>
        db.ExternalLogins
            .Where(e => e.Provider == provider && e.ProviderKey == providerKey)
            .Select(e => e.User)
            .SingleOrDefaultAsync(ct);

    public async Task AddExternalLoginAsync(ExternalLogin externalLogin, CancellationToken ct = default)
    {
        db.ExternalLogins.Add(externalLogin);
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> IsUserBannedAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.BanDetails != null).SingleAsync(ct);

    public async Task RevokeAllRefreshTokensForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken ct = default)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            db.Entry(token).Property(t => t.RevokedAt).CurrentValue = revokedAt;
        }

        await db.SaveChangesAsync(ct);
    }
}
