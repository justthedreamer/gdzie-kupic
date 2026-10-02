using System.Net;
using System.Net.Http.Headers;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>
/// Verifies issue #27: every <c>LocationController</c> endpoint requires a valid access token.
/// Uses coordinates that fail validation before any external Google Geocoding call is made, so
/// these tests only exercise the authentication/authorization gate, not the geocoding provider.
/// </summary>
public class LocationControllerTests : IntegrationTestBase
{
    private const string LocationUrl = "/api/location?latitude=&longitude=";

    [Test]
    public async Task GetLocation_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync(LocationUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetLocation_WithInvalidToken_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this-is-not-a-valid-jwt");

        var response = await Client.GetAsync(LocationUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetLocation_WithExpiredToken_ReturnsUnauthorized()
    {
        var userId = await CreateUserAsync(Role.Buyer);
        var token = GenerateToken(userId, Role.Buyer, DateTime.UtcNow.AddDays(-1));
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync(LocationUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(Role.Buyer)]
    [TestCase(Role.Merchant)]
    [TestCase(Role.Admin)]
    public async Task GetLocation_WithValidToken_IsAuthorized(Role role)
    {
        var userId = await CreateUserAsync(role);
        var token = GenerateToken(userId, role, DateTime.UtcNow.AddDays(1));
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync(LocationUrl);

        // Reaches controller/service logic (validation error for empty coordinates) instead of
        // being rejected at the authentication/authorization gate.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> CreateUserAsync(Role role)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User(
            id: Guid.NewGuid(),
            email: $"{Guid.NewGuid():N}@example.com",
            passwordHash: null,
            role: role,
            createdAt: DateTimeOffset.UtcNow);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    private static string GenerateToken(Guid userId, Role role, DateTime expiresAt)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var jwtTokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        return jwtTokenGenerator.GenerateAccessToken(userId, role, expiresAt).Token;
    }
}

