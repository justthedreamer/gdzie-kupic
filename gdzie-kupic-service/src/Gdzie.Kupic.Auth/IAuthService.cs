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

public interface IAuthService
{
    Task<SignInResult> SignInAsync(string email, string password);
    Task<SignUpResult> SignUpAsync(string requestEmail, string requestPassword, Role role);
    Task<RefreshResult> RefreshAsync(string refreshToken);
    Task<GoogleSignInResult> GoogleSignInAsync(string providerKey, string email, Role role);
}