namespace Gdzie.Kupic.Hangfire;

public sealed class HangfireSettings
{
    public const string SectionName = "Hangfire";

    public const string PostgresStorage = "Postgres";
    public const string InMemoryStorage = "InMemory";

    public string Storage { get; set; } = PostgresStorage;

    public string SchemaName { get; set; } = "hangfire";

    public bool ServerEnabled { get; set; } = true;

    /// <summary>Defaults to ProcessorCount x 5 when not configured.</summary>
    public int? WorkerCount { get; set; }

    public int PollingIntervalSeconds { get; set; } = 1;

    public int RetryAttempts { get; set; } = 10;

    /// <summary>Delays between retries; when empty Hangfire's exponential back-off is used.</summary>
    public int[] RetryDelaysInSeconds { get; set; } = [];

    public int EffectiveWorkerCount => WorkerCount is > 0 ? WorkerCount.Value : Environment.ProcessorCount * 5;
}
