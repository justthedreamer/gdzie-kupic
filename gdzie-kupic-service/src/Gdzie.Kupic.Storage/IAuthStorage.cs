namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Auth;

public interface IAuthStorage
{
    Task<User?> FindUserByEmailAsync(string email, CancellationToken ct = default);

    Task<bool> UserEmailExistsAsync(string email, CancellationToken ct = default);

    Task AddUserAsync(User user, CancellationToken ct = default);

    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default);

    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct = default);

    Task RevokeRefreshTokenAsync(RefreshToken refreshToken, DateTimeOffset revokedAt, CancellationToken ct = default);

    Task<User?> FindUserByExternalLoginAsync(string provider, string providerKey, CancellationToken ct = default);

    Task AddExternalLoginAsync(ExternalLogin externalLogin, CancellationToken ct = default);
}
