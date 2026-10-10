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
    IAccountStatusCache accountStatusCache,
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

        if (await accountStatusCache.IsBannedAsync(user.Id))
        {
            return new SignInResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                InvalidCredentialsError: null,
                AccountBannedError: "This account has been banned.");
        }

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(user);

        return new SignInResult(accessToken, refreshToken, expiresAt, InvalidCredentialsError: null);
    }

    public async Task<SignUpResult> SignUpAsync(string requestEmail, string requestPassword, Role role, string? firstName = null)
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

        var (name, nameError) = FirstName.Normalize(firstName);

        if (nameError is not null)
        {
            return new SignUpResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                EmailAlreadyExistsError: null,
                ValidationError: nameError);
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
        var user = new User(Guid.NewGuid(), requestEmail, passwordHash, role, DateTimeOffset.UtcNow)
        {
            FirstName = name,
        };

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

        if (existingToken is null)
        {
            return new RefreshResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                InvalidRefreshTokenError: "Invalid or expired refresh token.");
        }

        if (existingToken.RevokedAt is not null)
        {
            // Reuse of a token that was already rotated is a theft signal: the token has leaked,
            // so every currently valid refresh token for this user is revoked, forcing a fresh login.
            await authStorage.RevokeAllRefreshTokensForUserAsync(existingToken.UserId, DateTimeOffset.UtcNow);

            return new RefreshResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                InvalidRefreshTokenError: "Invalid or expired refresh token.");
        }

        if (existingToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return new RefreshResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                InvalidRefreshTokenError: "Invalid or expired refresh token.");
        }

        if (await accountStatusCache.IsBannedAsync(existingToken.UserId))
        {
            return new RefreshResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                InvalidRefreshTokenError: null,
                AccountBannedError: "This account has been banned.");
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

    public async Task<GoogleSignInResult> GoogleSignInAsync(string providerKey, string email, Role role, string? firstName = null)
    {
        // A name Google reports that does not meet our rules is ignored rather than failing the sign-in.
        var (googleName, googleNameError) = FirstName.Normalize(firstName);
        if (googleNameError is not null) googleName = null;

        var user = await authStorage.FindUserByExternalLoginAsync(GoogleProvider, providerKey);

        if (user is null)
        {
            user = await authStorage.FindUserByEmailAsync(email);

            if (user is null)
            {
                user = new User(Guid.NewGuid(), email, passwordHash: null, role, DateTimeOffset.UtcNow)
                {
                    FirstName = googleName,
                };
                await authStorage.AddUserAsync(user);
            }

            await authStorage.AddExternalLoginAsync(new ExternalLogin(
                Guid.NewGuid(),
                user.Id,
                GoogleProvider,
                providerKey,
                DateTimeOffset.UtcNow));
        }

        if (await accountStatusCache.IsBannedAsync(user.Id))
        {
            return new GoogleSignInResult(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAt: default,
                AccountBannedError: "This account has been banned.");
        }

        // Only fills a gap: a name already on the account is never overwritten.
        if (user.FirstName is null && googleName is not null)
        {
            user.FirstName = googleName;
            await authStorage.UpdateUserAsync(user);
        }

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(user);

        return new GoogleSignInResult(accessToken, refreshToken, expiresAt);
    }

    public async Task<ProfileResult?> GetProfileAsync(Guid userId)
    {
        var user = await authStorage.FindUserByIdAsync(userId);

        return user is null ? null : new ProfileResult(user.Email, user.FirstName, user.Role);
    }

    public async Task<UpdateProfileResult> UpdateFirstNameAsync(Guid userId, string? firstName)
    {
        var (name, nameError) = FirstName.Normalize(firstName);

        if (nameError is not null) return new UpdateProfileResult(null, nameError);

        var user = await authStorage.FindUserByIdAsync(userId);

        if (user is null) return new UpdateProfileResult(null, null, NotFound: true);

        user.FirstName = name;
        await authStorage.UpdateUserAsync(user);

        return new UpdateProfileResult(new ProfileResult(user.Email, user.FirstName, user.Role), null);
    }
}