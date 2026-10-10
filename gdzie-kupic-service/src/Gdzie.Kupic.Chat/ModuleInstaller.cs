using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gdzie.Kupic.Chat;

public static class ModuleInstaller
{
    public static IServiceCollection InstallChatModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}