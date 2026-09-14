using System.Security.Cryptography;

namespace Gdzie.Kupic.Auth;

public interface IRefreshTokenGenerator
{
    string GenerateToken();
    string Hash(string token);
}

internal sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenSizeInBytes = 32;

    public string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenSizeInBytes))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    public string Hash(string token)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
