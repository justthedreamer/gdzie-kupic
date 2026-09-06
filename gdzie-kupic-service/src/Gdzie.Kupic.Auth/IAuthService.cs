using Gdzie.Kupic.Domain.Model;

namespace Gdzie.Kupic.Auth;

public record SignInResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string? InvalidCredentialsError);

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
    string? InvalidRefreshTokenError);

public interface IAuthService
{
    Task<SignInResult> SignInAsync(string email, string password);
    Task<SignUpResult> SignUpAsync(string requestEmail, string requestPassword, Role role);
    Task<RefreshResult> RefreshAsync(string refreshToken);
}