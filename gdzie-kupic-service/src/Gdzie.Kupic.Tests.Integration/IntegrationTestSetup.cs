namespace Gdzie.Kupic.Tests.Integration;

/// <summary>
/// Runs once for the whole test assembly: starts a single InMemory-backed
/// <see cref="IntegrationTestFactory"/> shared by all integration test fixtures, and tears
/// it down after the last test finishes.
/// </summary>
[SetUpFixture]
public class IntegrationTestSetup
{
    public static IntegrationTestFactory Factory { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        Factory = new IntegrationTestFactory();
        await Factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        await Factory.DisposeAsync();
    }
}
