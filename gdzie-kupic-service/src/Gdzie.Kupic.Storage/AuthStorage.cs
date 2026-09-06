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
}
