using Gdzie.Kupic.Storage.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Gdzie.Kupic.Storage;

public static class ModuleInstaller
{
    public static void InstallStorageModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
                               ?? throw new InvalidOperationException(
                                   "Connection string 'Postgres' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.UseNetTopologySuite()));

        services.Configure<AdminSeedSettings>(configuration.GetSection("Seeding:Admin"));
        services.AddScoped<StorageSeeder>();
        services.AddScoped<CatalogueSeeder>();
        services.AddScoped<IAuthStorage, AuthStorage>();
        services.AddScoped<ICatalogueStorage, CatalogueStorage>();
        services.AddScoped<ILocationStorage, LocationStorage>();
        services.AddScoped<IMarketplaceStorage, MarketplaceStorage>();
        services.AddScoped<IPostStorage, PostStorage>();
        services.AddScoped<IMatchingStorage, MatchingStorage>();
        services.AddScoped<IOutboxStorage, OutboxStorage>();
    }

    public static async Task UseStorageModule(this IHost host, CancellationToken ct = default)
    {
        await using var scope = host.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);

        var seeder = scope.ServiceProvider.GetRequiredService<StorageSeeder>();
        await seeder.SeedAsync(ct);
    }
}
