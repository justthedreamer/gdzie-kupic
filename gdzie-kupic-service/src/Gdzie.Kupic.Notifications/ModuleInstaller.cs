using Microsoft.Extensions.DependencyInjection;

namespace Gdzie.Kupic.Notifications;

public static class ModuleInstaller
{
    public static IServiceCollection InstallNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<INotificationDispatcher, NoOpNotificationDispatcher>();

        return services;
    }
}
