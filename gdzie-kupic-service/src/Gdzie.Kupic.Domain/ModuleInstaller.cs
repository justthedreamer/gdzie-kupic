using Gdzie.Kupic.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Gdzie.Kupic.Domain;

public static class ModuleInstaller
{
    public static IServiceCollection InstallDomain(this IServiceCollection services)
    {
        services.AddSingleton<IDomainMapper, DomainMapper>();
        
        return services;
    }
}