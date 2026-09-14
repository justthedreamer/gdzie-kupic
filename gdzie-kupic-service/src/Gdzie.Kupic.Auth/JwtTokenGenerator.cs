using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gdzie.Kupic.Domain.Model;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gdzie.Kupic.Auth;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(Guid userId, Role role);

    /// <summary>
    /// Generates an access token with an explicit expiry instead of the configured
    /// <see cref="JwtSettings.AccessTokenLifetimeDays"/>. Used only to pre-generate the
    /// effectively non-expiring mock tokens documented in local-dev.md (see #25) - never
    /// exposed via a public API endpoint.
    /// </summary>
    (string Token, DateTime ExpiresAt) GenerateAccessToken(Guid userId, Role role, DateTime expiresAt);
}

internal sealed class JwtTokenGenerator(IOptions<JwtSettings> jwtOptions) : IJwtTokenGenerator
{
    private readonly JwtSettings _settings = jwtOptions.Value;

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(Guid userId, Role role) =>
        GenerateAccessToken(userId, role, DateTime.UtcNow.AddDays(_settings.AccessTokenLifetimeDays));

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(Guid userId, Role role, DateTime expiresAt)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
