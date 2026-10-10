using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gdzie.Kupic.Chat;

public static class ModuleInstaller
{
    public static IServiceCollection InstallChatModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));
        services.Configure<ChatSettings>(configuration.GetSection(ChatSettings.SectionName));
        services.TryAddSingleton<IObjectStorage, S3ObjectStorage>();
        services.AddHostedService<ObjectStorageInitializer>();

        services.TryAddSingleton<IChatChannel, NullChatChannel>();
        services.AddScoped<ChatEvents>();
        services.AddScoped<IChatThreadEvents>(sp => sp.GetRequiredService<ChatEvents>());
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}