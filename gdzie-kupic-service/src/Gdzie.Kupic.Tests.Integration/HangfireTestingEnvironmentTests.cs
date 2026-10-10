using Gdzie.Kupic.Hangfire;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>
/// In the <c>Testing</c> environment Hangfire uses in-memory storage and starts no job server,
/// so the API host stays fast and self-contained.
/// </summary>
public class HangfireTestingEnvironmentTests : IntegrationTestBase
{
    [Test]
    public void JobScheduler_IsResolvable_AndEnqueueReturnsJobId()
    {
        var scheduler = IntegrationTestSetup.Factory.Services.GetRequiredService<IJobScheduler>();

        var jobId = scheduler.Enqueue<NoOpJob>(j => j.RunAsync());

        jobId.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void JobServer_IsNotRunning()
    {
        IntegrationTestSetup.Factory.Services.GetServices<IHostedService>()
            .ShouldNotContain(service => service.GetType().Name.Contains("BackgroundJobServer"));
    }

    [Test]
    public async Task Dashboard_IsNotExposed()
    {
        var response = await Client.GetAsync("/hangfire");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    public sealed class NoOpJob
    {
        public Task RunAsync() => Task.CompletedTask;
    }
}
