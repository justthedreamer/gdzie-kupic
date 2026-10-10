using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gdzie.Kupic.Marketplace;

public static class ModuleInstaller
{
    public static IServiceCollection InstallMarketplaceModule(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(MarketplaceSettings.SectionName).Get<MarketplaceSettings>()
                       ?? new MarketplaceSettings();

        services.Configure<MarketplaceSettings>(configuration.GetSection(MarketplaceSettings.SectionName));
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IMerchantService, MerchantService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IMerchantResponseService, MerchantResponseService>();
        services.AddScoped<ExpirePostsJob>();
        services.AddScoped<OutboxRelayJob>();
        services.AddScoped<NotifyMerchantsJob>();
        services.AddScoped<NotifyMerchantsBatchJob>();
        services.AddScoped<NotifyNewMerchantJob>();

        if (settings.JobsEnabled)
        {
            services.AddHostedService<MarketplaceJobsRegistrar>();
            services.AddHostedService<OutboxRelayHostedService>();
        }

        return services;
    }
}
