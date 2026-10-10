namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Recurring job: tells every opted-in merchant user how many feed requests are not processed yet (the "new" counter of
/// the feed). Sent regardless of presence. A failure for one recipient does not stop the others; the job then throws at
/// the end so that Hangfire retries, and the per-slot delivery record keeps the retry from sending duplicates.
/// </summary>
internal sealed class MerchantDigestJob(
    INotificationStorage storage,
    IFeedStorage feed,
    IEmailSender sender,
    TimeProvider clock,
    IOptions<DigestSettings> settings,
    IOptions<AppLinksSettings> app,
    ILogger<MerchantDigestJob> logger)
{
    public async Task RunAsync()
    {
        var now = clock.GetUtcNow();
        var slot = DigestSlot.Current(now, settings.Value.Cron, DigestSlot.ResolveZone(settings.Value.TimeZone, logger));
        var counts = new Dictionary<Guid, int>();
        var failures = 0;

        foreach (var recipient in await storage.FindDigestRecipientsAsync())
        {
            try
            {
                if (await storage.WasDigestSentAsync(recipient.UserId, slot)) continue;

                if (!counts.TryGetValue(recipient.MerchantId, out var count))
                    counts[recipient.MerchantId] = count = (await feed.GetSummaryAsync(recipient.MerchantId, now)).NewCount;
                if (count == 0) continue;

                var user = new EmailRecipient(recipient.UserId, recipient.Email, recipient.FirstName, Role.Merchant, true, false);
                await sender.SendAsync(EmailComposer.Digest(user, count, app.Value));
                await storage.RecordDigestSentAsync(recipient.UserId, slot, now);
            }
            catch (Exception ex)
            {
                failures++;
                logger.LogWarning(ex, "Failed to send the digest to user {UserId}", recipient.UserId);
            }
        }

        if (failures > 0) throw new InvalidOperationException($"The digest failed for {failures} recipient(s).");
    }
}
