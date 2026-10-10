namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Registers the recurring merchant digest once the host starts.</summary>
internal sealed class NotificationJobsRegistrar(
    IJobScheduler scheduler,
    IOptions<DigestSettings> settings,
    ILogger<NotificationJobsRegistrar> logger) : IHostedService
{
    public const string DigestJobId = "merchant-digest";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var digest = settings.Value;
        scheduler.AddOrUpdateRecurring<MerchantDigestJob>(
            DigestJobId, job => job.RunAsync(), digest.Cron, DigestSlot.ResolveZone(digest.TimeZone, logger));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
