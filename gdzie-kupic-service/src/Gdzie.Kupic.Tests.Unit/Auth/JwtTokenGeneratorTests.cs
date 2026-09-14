namespace Gdzie.Kupic.Tests.Unit.Auth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Microsoft.Extensions.Options;
using Shouldly;

[TestFixture]
public class JwtTokenGeneratorTests
{
    [Test]
    public void GenerateAccessToken_ReturnsToken_WithUserIdAndRoleClaims()
    {
        var sut = new Fixture();
        var userId = Guid.NewGuid();

        var (token, _) = sut.Service.GenerateAccessToken(userId, Role.Merchant);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Subject.ShouldBe(userId.ToString());
        jwt.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == Role.Merchant.ToString());
    }

    [Test]
    public void GenerateAccessToken_ReturnsExpiresAt_MatchingConfiguredLifetime()
    {
        var sut = new Fixture(accessTokenLifetimeDays: 7);

        var (_, expiresAt) = sut.Service.GenerateAccessToken(Guid.NewGuid(), Role.Buyer);

        expiresAt.ShouldBeInRange(
            DateTime.UtcNow.AddDays(7).AddMinutes(-1),
            DateTime.UtcNow.AddDays(7).AddMinutes(1));
    }

    [Test]
    public void GenerateAccessToken_ReturnsToken_SignedWithConfiguredIssuerAndAudience()
    {
        var sut = new Fixture(issuer: "test-issuer", audience: "test-audience");

        var (token, _) = sut.Service.GenerateAccessToken(Guid.NewGuid(), Role.Buyer);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Issuer.ShouldBe("test-issuer");
        jwt.Audiences.ShouldContain("test-audience");
    }

    private class Fixture
    {
        public readonly JwtTokenGenerator Service;

        public Fixture(
            string secret = "unit-test-secret-value-that-is-long-enough-for-hs256",
            string issuer = "GdzieKupicService",
            string audience = "GdzieKupicClient",
            int accessTokenLifetimeDays = 7)
        {
            var settings = new JwtSettings
            {
                Secret = secret,
                Issuer = issuer,
                Audience = audience,
                AccessTokenLifetimeDays = accessTokenLifetimeDays,
            };

            this.Service = new JwtTokenGenerator(Options.Create(settings));
        }
    }
}
