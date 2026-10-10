using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Gdzie.Kupic.Notifications;

public static class ModuleInstaller
{
    public static IServiceCollection InstallNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<VapidSettings>(configuration.GetSection(VapidSettings.SectionName));
        services.AddHostedService<VapidConfigurationCheck>();
        services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();

        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.TryAddSingleton<IPresenceTracker, NullPresenceTracker>();
        services.TryAddSingleton<IWebPushSender, LibWebPushSender>();
        services.AddScoped<SendWebPushJob>();
        services.AddScoped<CleanPushSubscriptionsJob>();
        services.TryAddSingleton<INotificationChannel, NullNotificationChannel>();

        return services;
    }
}
