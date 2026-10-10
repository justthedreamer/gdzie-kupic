namespace Gdzie.Kupic.Marketplace;

using System.Text.Json;
using Gdzie.Kupic.Domain.Model.Infrastructure;
using Gdzie.Kupic.Hangfire;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Turns unprocessed outbox entries into background jobs.</summary>
internal sealed class OutboxRelayJob(
    IOutboxStorage outbox,
    IJobScheduler scheduler,
    IOptions<MarketplaceSettings> settings,
    TimeProvider clock,
    ILogger<OutboxRelayJob> logger)
{
    public async Task<int> RunAsync()
    {
        var handled = await outbox.ProcessPendingAsync(settings.Value.OutboxRelayBatchSize, clock.GetUtcNow(), Dispatch);

        if (handled > 0)
            logger.LogInformation("Relayed {Count} outbox entries", handled);

        return handled;
    }

    private Task Dispatch(OutboxMessage message)
    {
        using var payload = JsonDocument.Parse(message.Payload);

        switch (message.Type)
        {
            case OutboxMessageTypes.NotifyMerchants:
                var postId = payload.RootElement.GetProperty("postId").GetGuid();
                scheduler.Enqueue<NotifyMerchantsJob>(job => job.RunAsync(postId));
                break;
            case OutboxMessageTypes.NotifyNewMerchant:
                var merchantId = payload.RootElement.GetProperty("merchantId").GetGuid();
                scheduler.Enqueue<NotifyNewMerchantJob>(job => job.RunAsync(merchantId));
                break;
            default:
                logger.LogError("Unknown outbox message type {Type} for entry {OutboxId}", message.Type, message.Id);
                break;
        }

        return Task.CompletedTask;
    }
}
