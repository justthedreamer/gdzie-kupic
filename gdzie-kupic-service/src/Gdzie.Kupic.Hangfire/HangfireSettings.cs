namespace Gdzie.Kupic.Hangfire;

public sealed class HangfireSettings
{
    public const string SectionName = "Hangfire";

    /// <summary>PostgreSQL schema that holds the job store.</summary>
    public string SchemaName { get; set; } = "hangfire";

    /// <summary>When false the application can enqueue jobs but does not run them.</summary>
    public bool ServerEnabled { get; set; } = true;

    /// <summary>Number of concurrent workers; defaults to <c>ProcessorCount × 5</c> when not set.</summary>
    public int? WorkerCount { get; set; }

    /// <summary>How often workers and the scheduler poll the job store, in seconds.</summary>
    public int PollingIntervalSeconds { get; set; } = 1;

    public int ResolveWorkerCount() =>
        WorkerCount is > 0 ? WorkerCount.Value : Environment.ProcessorCount * 5;

    public TimeSpan ResolvePollingInterval() =>
        TimeSpan.FromSeconds(Math.Max(1, PollingIntervalSeconds));
}
