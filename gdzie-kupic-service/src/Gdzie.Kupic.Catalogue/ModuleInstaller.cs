using Microsoft.Extensions.DependencyInjection;

namespace Gdzie.Kupic.Catalogue;

public static class ModuleInstaller
{
    public static IServiceCollection InstallCatalogueModule(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<ICatalogueService, CatalogueService>();

        return services;
    }
}