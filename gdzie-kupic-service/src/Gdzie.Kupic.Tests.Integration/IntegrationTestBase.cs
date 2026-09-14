using System.Net.Http.Json;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>
/// Base class for integration test fixtures: exposes an <see cref="HttpClient"/> against the
/// shared in-process API host and resets the database to a clean state before every test.
/// </summary>
[TestFixture]
public abstract class IntegrationTestBase
{
    protected HttpClient Client { get; private set; } = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        await IntegrationTestSetup.Factory.ResetDatabaseAsync();
        Client = IntegrationTestSetup.Factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        Client.Dispose();
    }

    protected static async Task<T?> ReadAsAsync<T>(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<T>();
}
