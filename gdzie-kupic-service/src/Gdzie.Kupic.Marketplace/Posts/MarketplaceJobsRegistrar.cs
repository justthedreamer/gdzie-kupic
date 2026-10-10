namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>Registers the recurring Marketplace jobs once the host starts.</summary>
internal sealed class MarketplaceJobsRegistrar(IJobScheduler scheduler, IOptions<MarketplaceSettings> settings) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        scheduler.AddOrUpdateRecurring<ExpirePostsJob>("expire-posts", job => job.RunAsync(), settings.Value.ExpirePostsCron);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
