namespace Gdzie.Kupic.Realtime;

using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Service.API.Contract.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Collects the explicit list of channel registrations (see <c>Program.cs</c>).</summary>
public sealed class RealtimeBuilder(IServiceCollection services)
{
    /// <summary>Registers <typeparamref name="TImplementation"/> as the singleton implementation of a module's channel.</summary>
    public RealtimeBuilder AddChannel<TInterface, TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddSingleton<TInterface, TImplementation>();

        return this;
    }
}

public static class ModuleInstaller
{
    public static IServiceCollection AddRealtimeModule(this IServiceCollection services, Action<RealtimeBuilder> configure)
    {
        services.AddSignalR();
        services.AddSingleton<IRealtimeSender, HubRealtimeSender>();
        services.AddSingleton<ConnectionPresenceTracker>();
        services.AddSingleton<IPresenceTracker>(sp => sp.GetRequiredService<ConnectionPresenceTracker>());

        configure(new RealtimeBuilder(services));

        return services;
    }

    public static IEndpointRouteBuilder MapRealtimeHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<AppHub>(RealtimeEvents.HubPath).RequireAuthorization();

        return endpoints;
    }
}