using System.Collections.Concurrent;
using Gdzie.Kupic.Hangfire;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>
/// Runs the Hangfire module against a real PostgreSQL (Testcontainers) with the job server
/// started, as in production. Each test uses its own schema so tests cannot see each other's jobs.
/// </summary>
[TestFixture]
public class HangfireInfrastructureTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task StartPostgresAsync()
    {
        _postgres = new PostgreSqlBuilder("postgis/postgis:16-3.4").Build();
        await _postgres.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopPostgresAsync() => await _postgres.DisposeAsync();

    [Test]
    public async Task EnqueuedJob_IsExecuted()
    {
        var key = Guid.NewGuid().ToString("N");
        using var host = await StartHostAsync(SchemaName(), serverEnabled: true);

        host.Services.GetRequiredService<IJobScheduler>().Enqueue<ProbeJob>(j => j.SucceedAsync(key, Guid.NewGuid()));

        (await Probe.WaitAsync(key, 1, Timeout)).ShouldBeTrue();
    }

    [Test]
    public async Task EnqueuedJob_IsPersistedAndRunsAfterRestart()
    {
        var key = Guid.NewGuid().ToString("N");
        var schema = SchemaName();

        // First "process" only enqueues: no job server, so nothing runs.
        using (var producer = await StartHostAsync(schema, serverEnabled: false))
        {
            producer.Services.GetRequiredService<IJobScheduler>()
                .Enqueue<ProbeJob>(j => j.SucceedAsync(key, Guid.NewGuid()));
            await producer.StopAsync();
        }

        Probe.Count(key).ShouldBe(0);

        // Second "process" starts later against the same database and picks the job up.
        using var consumer = await StartHostAsync(schema, serverEnabled: true);

        (await Probe.WaitAsync(key, 1, Timeout)).ShouldBeTrue();
    }

    [Test]
    public async Task FailingJob_IsRetried()
    {
        var key = Guid.NewGuid().ToString("N");
        var logs = new CapturingLoggerProvider();
        using var host = await StartHostAsync(SchemaName(), serverEnabled: true, logs);

        host.Services.GetRequiredService<IJobScheduler>().Enqueue<ProbeJob>(j => j.FailOnceAsync(key));

        (await Probe.WaitAsync(key, 2, Timeout)).ShouldBeTrue();
        logs.Entries.ShouldContain(e => e.Level == LogLevel.Error && e.Message.Contains("failed"));
    }

    [Test]
    public async Task ScheduledJob_RunsAfterDelay()
    {
        var key = Guid.NewGuid().ToString("N");
        using var host = await StartHostAsync(SchemaName(), serverEnabled: true);

        host.Services.GetRequiredService<IJobScheduler>()
            .Schedule<ProbeJob>(j => j.SucceedAsync(key, Guid.NewGuid()), TimeSpan.FromSeconds(2));

        Probe.Count(key).ShouldBe(0);
        (await Probe.WaitAsync(key, 1, Timeout)).ShouldBeTrue();
    }

    [Test]
    public async Task Job_IsLoggedWithJobIdPostIdAndCorrelationId()
    {
        var key = Guid.NewGuid().ToString("N");
        var postId = Guid.NewGuid();
        var logs = new CapturingLoggerProvider();
        using var host = await StartHostAsync(SchemaName(), serverEnabled: true, logs);

        var jobId = host.Services.GetRequiredService<IJobScheduler>()
            .Enqueue<ProbeJob>(j => j.SucceedAsync(key, postId));

        await Probe.WaitAsync(key, 1, Timeout);
        var entry = await logs.WaitForAsync(e => e.Message.Contains("succeeded"), TimeSpan.FromSeconds(15));

        entry.ShouldNotBeNull();
        entry.Scope["JobId"].ShouldBe(jobId);
        entry.Scope["PostId"].ShouldBe(postId);
        entry.Scope["CorrelationId"].ShouldBeOfType<string>().ShouldNotBeNullOrWhiteSpace();
    }

    private static string SchemaName() => "hf_" + Guid.NewGuid().ToString("N")[..12];

    private async Task<IHost> StartHostAsync(string schema, bool serverEnabled, CapturingLoggerProvider? logs = null)
    {
        var host = Host.CreateDefaultBuilder()
            .UseEnvironment("Production")
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Hangfire:SchemaName"] = schema,
                ["Hangfire:ServerEnabled"] = serverEnabled.ToString(),
                ["Hangfire:WorkerCount"] = "2",
            }))
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                if (logs is not null)
                {
                    logging.AddProvider(logs);
                }
            })
            .ConfigureServices((context, services) =>
            {
                services.InstallHangfireModule(context.Configuration, context.HostingEnvironment);
                services.AddTransient<ProbeJob>();
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    public sealed class ProbeJob
    {
        public Task SucceedAsync(string key, Guid postId)
        {
            Probe.Hit(key);
            return Task.CompletedTask;
        }

        public Task FailOnceAsync(string key)
        {
            if (Probe.Hit(key) == 1)
            {
                throw new InvalidOperationException("Planned failure on the first attempt.");
            }

            return Task.CompletedTask;
        }
    }

    private static class Probe
    {
        private static readonly ConcurrentDictionary<string, int> Hits = new();

        public static int Hit(string key) => Hits.AddOrUpdate(key, 1, (_, current) => current + 1);

        public static int Count(string key) => Hits.GetValueOrDefault(key);

        public static async Task<bool> WaitAsync(string key, int expected, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (Count(key) >= expected)
                {
                    return true;
                }

                await Task.Delay(100);
            }

            return false;
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Scope);

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

        public async Task<LogEntry?> WaitForAsync(Func<LogEntry, bool> predicate, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var match = _entries.FirstOrDefault(predicate);
                if (match is not null)
                {
                    return match;
                }

                await Task.Delay(100);
            }

            return null;
        }

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
                owner._scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var scope = new Dictionary<string, object?>();
                owner._scopes.ForEachScope(
                    (item, target) =>
                    {
                        if (item is IEnumerable<KeyValuePair<string, object?>> pairs)
                        {
                            foreach (var pair in pairs)
                            {
                                target[pair.Key] = pair.Value;
                            }
                        }
                    },
                    scope);

                owner._entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), scope));
            }
        }
    }
}
