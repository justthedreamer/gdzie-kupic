using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>
/// Base class for integration test fixtures: exposes an <see cref="HttpClient"/> against the
/// shared in-process API host and resets the database to a clean state before every test.
/// </summary>
[TestFixture]
public abstract class IntegrationTestBase
{
    protected HttpClient Client { get; private set; } = null!;
    internal FakeGeocodingClient Geocoder { get; private set; } = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        await IntegrationTestSetup.Factory.ResetDatabaseAsync();
        Client = IntegrationTestSetup.Factory.CreateClient();
        Geocoder = IntegrationTestSetup.Factory.Services.GetRequiredService<FakeGeocodingClient>();
        Geocoder.Reset();
        IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingJobScheduler>().Reset();
        IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingNotificationDispatcher>().Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Client.Dispose();
    }

    protected static async Task<T?> ReadAsAsync<T>(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<T>();
    /// <summary>Creates a user with the given role and sets its access token on <see cref="Client"/>.</summary>
    protected async Task<Guid> AuthenticateAsync(Role role, Guid? userId = null)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User(userId ?? Guid.NewGuid(), $"{Guid.NewGuid():N}@example.com", null, role, DateTimeOffset.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>()
            .GenerateAccessToken(user.Id, role, DateTime.UtcNow.AddDays(1)).Token;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return user.Id;
    }
}