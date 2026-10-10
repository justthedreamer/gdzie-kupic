namespace Gdzie.Kupic.Notifications;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Warns at startup when Web Push is switched off because the VAPID configuration is incomplete.</summary>
internal sealed class VapidConfigurationCheck(IOptions<VapidSettings> settings, ILogger<VapidConfigurationCheck> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!settings.Value.IsConfigured)
        {
            logger.LogWarning(
                "VAPID is not configured (Vapid:PublicKey, Vapid:PrivateKey, Vapid:Subject); Web Push is disabled");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
