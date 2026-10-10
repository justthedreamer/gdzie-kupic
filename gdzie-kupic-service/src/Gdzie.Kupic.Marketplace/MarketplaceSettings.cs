namespace Gdzie.Kupic.Marketplace;

public sealed class MarketplaceSettings
{
    public const string SectionName = "Marketplace";

    /// <summary>Whether the background schedulers (outbox relay, post expiry) are registered at all.</summary>
    public bool JobsEnabled { get; set; } = true;

    public int DefaultPostLifetimeHours { get; set; } = 72;

    public int LongLivedPostDays { get; set; } = 14;

    /// <summary>Cron expression of the periodic post expiry job.</summary>
    public string ExpirePostsCron { get; set; } = "* * * * *";

    public int OutboxRelayIntervalSeconds { get; set; } = 5;

    public int OutboxRelayBatchSize { get; set; } = 100;

    public TimeSpan DefaultPostLifetime => TimeSpan.FromHours(DefaultPostLifetimeHours);

    public TimeSpan LongLivedPostLifetime => TimeSpan.FromDays(LongLivedPostDays);
}
