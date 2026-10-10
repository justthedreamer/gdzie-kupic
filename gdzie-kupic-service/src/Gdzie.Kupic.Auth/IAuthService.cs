using Gdzie.Kupic.Domain.Model;

namespace Gdzie.Kupic.Auth;

public record SignInResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string? InvalidCredentialsError,
    string? AccountBannedError = null);

public record SignUpResult(
    string AccessToken,
    string RefreshToken,
    string? ValidationError,
    string? EmailAlreadyExistsError,
    DateTime ExpiresAt);

public record RefreshResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string? InvalidRefreshTokenError,
    string? AccountBannedError = null);

public record GoogleSignInResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string? AccountBannedError = null);

public record ProfileResult(string Email, string? FirstName, Role Role);

public record UpdateProfileResult(ProfileResult? Profile, string? ValidationError, bool NotFound = false);

public interface IAuthService
{
    Task<SignInResult> SignInAsync(string email, string password);
    Task<SignUpResult> SignUpAsync(string requestEmail, string requestPassword, Role role, string? firstName = null);
    Task<RefreshResult> RefreshAsync(string refreshToken);

    /// <param name="firstName">The first name Google reported; used only when it is valid and the account has none yet.</param>
    Task<GoogleSignInResult> GoogleSignInAsync(string providerKey, string email, Role role, string? firstName = null);

    /// <returns>The account's profile, or <c>null</c> when no such user exists.</returns>
    Task<ProfileResult?> GetProfileAsync(Guid userId);

    /// <summary>Sets (or, when empty, clears) the user's first name.</summary>
    Task<UpdateProfileResult> UpdateFirstNameAsync(Guid userId, string? firstName);
}