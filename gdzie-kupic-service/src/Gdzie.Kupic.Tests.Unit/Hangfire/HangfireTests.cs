using Gdzie.Kupic.Hangfire;
using Hangfire.Common;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Hangfire;

[TestFixture]
public class HangfireSettingsTests
{
    [Test]
    public void Defaults_UseOwnSchemaAndOneSecondPolling()
    {
        var sut = new HangfireSettings();

        sut.SchemaName.ShouldBe("hangfire");
        sut.ServerEnabled.ShouldBeTrue();
        sut.ResolvePollingInterval().ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Test]
    public void ResolveWorkerCount_DefaultsToProcessorCountTimesFive()
    {
        new HangfireSettings().ResolveWorkerCount().ShouldBe(Environment.ProcessorCount * 5);
    }

    [TestCase(null)]
    [TestCase(0)]
    [TestCase(-3)]
    public void ResolveWorkerCount_FallsBackToDefault_WhenNotPositive(int? configured)
    {
        new HangfireSettings { WorkerCount = configured }.ResolveWorkerCount()
            .ShouldBe(Environment.ProcessorCount * 5);
    }

    [Test]
    public void ResolveWorkerCount_UsesConfiguredValue()
    {
        new HangfireSettings { WorkerCount = 3 }.ResolveWorkerCount().ShouldBe(3);
    }

    [TestCase(0, 1)]
    [TestCase(2, 2)]
    public void ResolvePollingInterval_IsAtLeastOneSecond(int configured, int expectedSeconds)
    {
        new HangfireSettings { PollingIntervalSeconds = configured }.ResolvePollingInterval()
            .ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }
}

[TestFixture]
public class JobLoggingFilterTests
{
    private sealed class SampleJob
    {
        public Task WithPostIdAsync(Guid postId) => Task.CompletedTask;

        public Task WithoutPostIdAsync(string value) => Task.CompletedTask;
    }

    [Test]
    public void Describe_IncludesJobIdCorrelationIdAndPostId_WhenJobTakesPostId()
    {
        var postId = Guid.NewGuid();
        var job = Job.FromExpression<SampleJob>(j => j.WithPostIdAsync(postId));

        var properties = JobLoggingFilter.Describe("42", job, "corr-1");

        properties["JobId"].ShouldBe("42");
        properties["CorrelationId"].ShouldBe("corr-1");
        properties["PostId"].ShouldBe(postId);
    }

    [Test]
    public void Describe_OmitsPostId_WhenJobHasNoPostIdArgument()
    {
        var job = Job.FromExpression<SampleJob>(j => j.WithoutPostIdAsync("x"));

        var properties = JobLoggingFilter.Describe("7", job, "corr-2");

        properties.ContainsKey("PostId").ShouldBeFalse();
        properties["JobId"].ShouldBe("7");
    }
}
