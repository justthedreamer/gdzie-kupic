using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Options;

namespace Gdzie.Kupic.Auth;

public class AuthService(
    IAuthStorage authStorage,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IRefreshTokenGenerator refreshTokenGenerator,
    IOptions<RefreshTokenSettings> refreshTokenOptions) : IAuthService
{
    private const int MinimumPasswordLength = 8;
    private const string GoogleProvider = "Google";

    private readonly RefreshTokenSettings _refreshTokenSettings = refreshTokenOptions.Value;

    public async Task<SignInResult> SignInAsync(string email, string password)
    {
        var user = await authStorage.FindUserByEmailAsync(email);

        if (user is null || user.PasswordHash is null || !passwordHasher.Verify(password, user.PasswordHash))
        {
            return new SignInResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                InvalidCredentialsError: "Invalid email or password.");
        }

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(user);

        return new SignInResult(accessToken, refreshToken, expiresAt, InvalidCredentialsError: null);
    }

    public async Task<SignUpResult> SignUpAsync(string requestEmail, string requestPassword, Role role)
    {
        if (requestPassword.Length < MinimumPasswordLength)
        {
            return new SignUpResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                EmailAlreadyExistsError: null,
                ValidationError: $"Password must be at least {MinimumPasswordLength} characters.");
        }

        var emailExists = await authStorage.UserEmailExistsAsync(requestEmail);

        if (emailExists)
        {
            return new SignUpResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                ValidationError: null,
                EmailAlreadyExistsError: "An account with this email already exists.");
        }

        var passwordHash = passwordHasher.Hash(requestPassword);
        var user = new User(Guid.NewGuid(), requestEmail, passwordHash, role, DateTimeOffset.UtcNow);

        await authStorage.AddUserAsync(user);

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(user);

        return new SignUpResult(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: expiresAt,
            EmailAlreadyExistsError: null,
            ValidationError: null);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken)
    {
        var tokenHash = refreshTokenGenerator.Hash(refreshToken);

        var existingToken = await authStorage.FindRefreshTokenByHashAsync(tokenHash);

        if (existingToken is null
            || existingToken.RevokedAt is not null
            || existingToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return new RefreshResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                InvalidRefreshTokenError: "Invalid or expired refresh token.");
        }

        await authStorage.RevokeRefreshTokenAsync(existingToken, DateTimeOffset.UtcNow);

        var (accessToken, newRefreshToken, expiresAt) = await IssueTokensAsync(existingToken.User);

        return new RefreshResult(accessToken, newRefreshToken, expiresAt, InvalidRefreshTokenError: null);
    }

    private async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> IssueTokensAsync(User user)
    {
        var (accessToken, expiresAt) = jwtTokenGenerator.GenerateAccessToken(user.Id, user.Role);

        var refreshToken = refreshTokenGenerator.GenerateToken();
        var refreshTokenHash = refreshTokenGenerator.Hash(refreshToken);

        await authStorage.AddRefreshTokenAsync(new RefreshToken(
            Guid.NewGuid(),
            user.Id,
            refreshTokenHash,
            revokedAt: null,
            expiresAt: DateTimeOffset.UtcNow.AddDays(_refreshTokenSettings.LifetimeDays),
            createdAt: DateTimeOffset.UtcNow));

        return (accessToken, refreshToken, expiresAt);
    }

    public async Task<GoogleSignInResult> GoogleSignInAsync(string providerKey, string email, Role role)
    {
        var user = await authStorage.FindUserByExternalLoginAsync(GoogleProvider, providerKey);

        if (user is null)
        {
            user = await authStorage.FindUserByEmailAsync(email);

            if (user is null)
            {
                user = new User(Guid.NewGuid(), email, passwordHash: null, role, DateTimeOffset.UtcNow);
                await authStorage.AddUserAsync(user);
            }

            await authStorage.AddExternalLoginAsync(new ExternalLogin(
                Guid.NewGuid(),
                user.Id,
                GoogleProvider,
                providerKey,
                DateTimeOffset.UtcNow));
        }

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(user);

        return new GoogleSignInResult(accessToken, refreshToken, expiresAt);
    }
}