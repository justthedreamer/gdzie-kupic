namespace Gdzie.Kupic.Tests.Unit.Auth;

using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;

[TestFixture]
public class AuthServiceTests
{
    [Test]
    public async Task SignUpAsync_CreatesUser_WithHashedPassword()
    {
        var sut = new Fixture();

        var result = await sut.Service.SignUpAsync("buyer@example.com", "password123", Role.Buyer);

        result.EmailAlreadyExistsError.ShouldBeNull();
        result.ValidationError.ShouldBeNull();
        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBeNullOrEmpty();

        var user = await sut.Db.Users.SingleAsync(u => u.Email == "buyer@example.com");
        user.Role.ShouldBe(Role.Buyer);
        user.PasswordHash.ShouldNotBeNullOrEmpty();
        user.PasswordHash.ShouldNotBe("password123");
    }

    [Test]
    public async Task SignUpAsync_ReturnsValidationError_WhenPasswordIsShorterThan8Characters()
    {
        var sut = new Fixture();

        var result = await sut.Service.SignUpAsync("buyer@example.com", "short", Role.Buyer);

        result.ValidationError.ShouldNotBeNull();

        var exists = await sut.Db.Users.AnyAsync(u => u.Email == "buyer@example.com");
        exists.ShouldBeFalse();
    }

    [Test]
    public async Task SignUpAsync_ReturnsEmailAlreadyExistsError_WhenEmailIsTaken()
    {
        var sut = new Fixture();
        await sut.Service.SignUpAsync("buyer@example.com", "password123", Role.Buyer);

        var result = await sut.Service.SignUpAsync("buyer@example.com", "password456", Role.Merchant);

        result.EmailAlreadyExistsError.ShouldNotBeNull();
    }

    [Test]
    public async Task SignInAsync_ReturnsTokens_WhenCredentialsAreValid()
    {
        var sut = new Fixture();
        await sut.Service.SignUpAsync("buyer@example.com", "password123", Role.Buyer);

        var result = await sut.Service.SignInAsync("buyer@example.com", "password123");

        result.InvalidCredentialsError.ShouldBeNull();
        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task SignInAsync_ReturnsInvalidCredentialsError_WhenPasswordIsWrong()
    {
        var sut = new Fixture();
        await sut.Service.SignUpAsync("buyer@example.com", "password123", Role.Buyer);

        var result = await sut.Service.SignInAsync("buyer@example.com", "wrong-password");

        result.InvalidCredentialsError.ShouldNotBeNull();
    }

    [Test]
    public async Task SignInAsync_ReturnsInvalidCredentialsError_WhenEmailDoesNotExist()
    {
        var sut = new Fixture();

        var result = await sut.Service.SignInAsync("unknown@example.com", "password123");

        result.InvalidCredentialsError.ShouldNotBeNull();
    }

    [Test]
    public async Task RefreshAsync_ReturnsNewTokenPair_AndRevokesPreviousToken_WhenTokenIsValid()
    {
        var sut = new Fixture();
        var signUp = await sut.Service.SignUpAsync("buyer@example.com", "password123", Role.Buyer);

        var result = await sut.Service.RefreshAsync(signUp.RefreshToken);

        result.InvalidRefreshTokenError.ShouldBeNull();
        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBe(signUp.RefreshToken);

        var previousTokenHash = new RefreshTokenGenerator().Hash(signUp.RefreshToken);
        var previousToken = await sut.Db.RefreshTokens.SingleAsync(t => t.TokenHash == previousTokenHash);
        previousToken.RevokedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task RefreshAsync_ReturnsInvalidRefreshTokenError_WhenTokenWasAlreadyRotated()
    {
        var sut = new Fixture();
        var signUp = await sut.Service.SignUpAsync("buyer@example.com", "password123", Role.Buyer);
        await sut.Service.RefreshAsync(signUp.RefreshToken);

        var result = await sut.Service.RefreshAsync(signUp.RefreshToken);

        result.InvalidRefreshTokenError.ShouldNotBeNull();
    }

    [Test]
    public async Task RefreshAsync_ReturnsInvalidRefreshTokenError_WhenTokenIsUnknown()
    {
        var sut = new Fixture();

        var result = await sut.Service.RefreshAsync("unknown-refresh-token");

        result.InvalidRefreshTokenError.ShouldNotBeNull();
    }

    [Test]
    public async Task RefreshAsync_ReturnsInvalidRefreshTokenError_WhenTokenIsExpired()
    {
        var sut = new Fixture();
        var signUp = await sut.Service.SignUpAsync("buyer@example.com", "password123", Role.Buyer);

        var tokenHash = new RefreshTokenGenerator().Hash(signUp.RefreshToken);
        var token = await sut.Db.RefreshTokens.SingleAsync(t => t.TokenHash == tokenHash);
        sut.Db.Entry(token).Property(t => t.ExpiresAt).CurrentValue = DateTimeOffset.UtcNow.AddDays(-1);
        await sut.Db.SaveChangesAsync();

        var result = await sut.Service.RefreshAsync(signUp.RefreshToken);

        result.InvalidRefreshTokenError.ShouldNotBeNull();
    }

    [Test]
    public async Task GoogleSignInAsync_CreatesNewUserAndLinksExternalLogin_WhenNoAccountExists()
    {
        var sut = new Fixture();

        var result = await sut.Service.GoogleSignInAsync("google-subject-1", "buyer@example.com", Role.Buyer);

        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBeNullOrEmpty();

        var user = await sut.Db.Users.SingleAsync(u => u.Email == "buyer@example.com");
        user.Role.ShouldBe(Role.Buyer);
        user.PasswordHash.ShouldBeNull();

        var externalLogin = await sut.Db.ExternalLogins.SingleAsync(e => e.UserId == user.Id);
        externalLogin.Provider.ShouldBe("Google");
        externalLogin.ProviderKey.ShouldBe("google-subject-1");
    }

    [Test]
    public async Task GoogleSignInAsync_LinksToExistingAccount_WhenEmailMatchesExistingUser()
    {
        var sut = new Fixture();
        await sut.Service.SignUpAsync("buyer@example.com", "password123", Role.Buyer);

        var result = await sut.Service.GoogleSignInAsync("google-subject-1", "buyer@example.com", Role.Buyer);

        result.AccessToken.ShouldNotBeNullOrEmpty();

        var usersWithEmail = await sut.Db.Users.Where(u => u.Email == "buyer@example.com").ToListAsync();
        usersWithEmail.Count.ShouldBe(1);
        usersWithEmail[0].PasswordHash.ShouldNotBeNull();

        var externalLogin = await sut.Db.ExternalLogins.SingleAsync(e => e.ProviderKey == "google-subject-1");
        externalLogin.UserId.ShouldBe(usersWithEmail[0].Id);
    }

    [Test]
    public async Task GoogleSignInAsync_ReturnsSameAccount_OnRepeatedGoogleLogin()
    {
        var sut = new Fixture();
        var firstSignIn = await sut.Service.GoogleSignInAsync("google-subject-1", "buyer@example.com", Role.Buyer);

        var secondSignIn = await sut.Service.GoogleSignInAsync("google-subject-1", "buyer@example.com", Role.Buyer);

        secondSignIn.AccessToken.ShouldNotBeNullOrEmpty();
        secondSignIn.RefreshToken.ShouldNotBe(firstSignIn.RefreshToken);

        var users = await sut.Db.Users.Where(u => u.Email == "buyer@example.com").ToListAsync();
        users.Count.ShouldBe(1);

        var externalLogins = await sut.Db.ExternalLogins.Where(e => e.ProviderKey == "google-subject-1").ToListAsync();
        externalLogins.Count.ShouldBe(1);
    }

    private class Fixture
    {
        public readonly AppDbContext Db;
        public readonly AuthService Service;

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            Db = new AppDbContext(options);

            var jwtSettings = new JwtSettings
            {
                Secret = "unit-test-secret-value-that-is-long-enough-for-hs256",
                Issuer = "GdzieKupicService",
                Audience = "GdzieKupicClient",
                AccessTokenLifetimeDays = 7,
            };
            var refreshTokenSettings = new RefreshTokenSettings { LifetimeDays = 30 };

            Service = new AuthService(
                new AuthStorage(Db),
                new PasswordHasher(),
                new JwtTokenGenerator(Options.Create(jwtSettings)),
                new RefreshTokenGenerator(),
                Options.Create(refreshTokenSettings));
        }
    }
}
