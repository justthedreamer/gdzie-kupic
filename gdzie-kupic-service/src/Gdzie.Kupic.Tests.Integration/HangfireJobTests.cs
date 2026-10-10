using Gdzie.Kupic.Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

[TestFixture]
public class HangfireJobTests
{
    [Test]
    public async Task Enqueued_job_is_executed()
    {
        var probe = new JobProbe();
        using var host = await StartHostAsync(probe);

        host.Services.GetRequiredService<IJobScheduler>().Enqueue<ProbeJob>(j => j.RunAsync(Guid.NewGuid()));

        await WaitUntilAsync(() => probe.Successes == 1);
    }

    [Test]
    public async Task Failing_job_is_retried_until_it_succeeds()
    {
        var probe = new JobProbe { FailuresBeforeSuccess = 2 };
        using var host = await StartHostAsync(probe);

        host.Services.GetRequiredService<IJobScheduler>().Enqueue<ProbeJob>(j => j.RunAsync(Guid.NewGuid()));

        await WaitUntilAsync(() => probe.Successes == 1);
        probe.Attempts.ShouldBe(3);
    }

    [Test]
    public void Testing_environment_runs_no_job_server()
    {
        IntegrationTestSetup.Factory.Services.GetServices<IHostedService>()
            .Select(s => s.GetType().Name)
            .ShouldNotContain("BackgroundJobServerHostedService");
        IntegrationTestSetup.Factory.Services.GetRequiredService<IJobScheduler>().ShouldNotBeNull();
    }

    private static async Task<IHost> StartHostAsync(JobProbe probe)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Hangfire:Storage"] = "InMemory",
            ["Hangfire:ServerEnabled"] = "true",
            ["Hangfire:WorkerCount"] = "2",
            ["Hangfire:RetryAttempts"] = "5",
            ["Hangfire:RetryDelaysInSeconds:0"] = "1",
        });
        builder.Services.InstallHangfireModule(builder.Configuration);
        builder.Services.AddSingleton(probe);
        builder.Services.AddTransient<ProbeJob>();

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Timed out waiting for the background job.");
            await Task.Delay(100);
        }
    }

    public sealed class JobProbe
    {
        public int FailuresBeforeSuccess { get; init; }
        public int Attempts;
        public int Successes;
    }

    public sealed class ProbeJob(JobProbe probe)
    {
        public Task RunAsync(Guid postId)
        {
            if (Interlocked.Increment(ref probe.Attempts) <= probe.FailuresBeforeSuccess)
                throw new InvalidOperationException("Simulated failure");

            Interlocked.Increment(ref probe.Successes);
            return Task.CompletedTask;
        }
    }
}
