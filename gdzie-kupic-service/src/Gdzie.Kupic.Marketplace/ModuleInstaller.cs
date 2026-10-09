using Microsoft.Extensions.DependencyInjection;

namespace Gdzie.Kupic.Marketplace;

public static class ModuleInstaller
{
    public static IServiceCollection InstallMarketplaceModule(this IServiceCollection services)
    {
        services.AddScoped<IMerchantService, MerchantService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        return services;
    }
}