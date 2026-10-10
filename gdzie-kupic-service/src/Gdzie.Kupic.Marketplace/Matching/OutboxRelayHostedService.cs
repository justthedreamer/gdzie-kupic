namespace Gdzie.Kupic.Marketplace;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Runs the outbox relay on a short interval. Hangfire recurring jobs cannot go below one minute,
/// so the sub-minute cadence is driven in-process; the relay itself is safe to run concurrently.
/// </summary>
internal sealed class OutboxRelayHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MarketplaceSettings> settings,
    ILogger<OutboxRelayHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, settings.Value.OutboxRelayIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await WaitAsync(timer, stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OutboxRelayJob>().RunAsync();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Outbox relay run failed");
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
