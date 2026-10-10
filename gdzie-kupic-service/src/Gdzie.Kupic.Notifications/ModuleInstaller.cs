using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gdzie.Kupic.Notifications;

public static class ModuleInstaller
{
    public static IServiceCollection InstallNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<INotificationDispatcher, NoOpNotificationDispatcher>();
        services.TryAddSingleton<INotificationChannel, NullNotificationChannel>();

        return services;
    }
}
