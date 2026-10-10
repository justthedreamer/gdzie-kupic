using Hangfire;
using Hangfire.Dashboard;
using Hangfire.InMemory;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gdzie.Kupic.Hangfire;

public static class ModuleInstaller
{
    public const string DashboardPath = "/hangfire";

    /// <summary>
    /// Registers Hangfire with a PostgreSQL job store (own schema, same database) and the
    /// <see cref="IJobScheduler"/> facade. In the <c>Testing</c> environment an in-memory store is
    /// used and no job server is started, so job classes can be invoked directly by tests.
    /// </summary>
    public static IServiceCollection InstallHangfireModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var settings = configuration.GetSection(HangfireSettings.SectionName).Get<HangfireSettings>()
                       ?? new HangfireSettings();
        var isTesting = environment.IsEnvironment("Testing");

        services.AddHangfire((serviceProvider, config) =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseFilter(new JobLoggingFilter(serviceProvider.GetRequiredService<ILoggerFactory>()));

            if (isTesting)
            {
                config.UseInMemoryStorage();
                return;
            }

            var connectionString = configuration.GetConnectionString("Postgres")
                                   ?? throw new InvalidOperationException(
                                       "Connection string 'Postgres' is not configured.");

            config.UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = settings.SchemaName,
                    QueuePollInterval = settings.ResolvePollingInterval(),
                    PrepareSchemaIfNecessary = true,
                });
        });

        if (settings.ServerEnabled && !isTesting)
        {
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = settings.ResolveWorkerCount();
                options.SchedulePollingInterval = settings.ResolvePollingInterval();
            });
        }

        services.AddSingleton<IJobScheduler, HangfireJobScheduler>();

        return services;
    }

    /// <summary>Exposes the Hangfire dashboard at <see cref="DashboardPath"/> in Development only.</summary>
    public static IApplicationBuilder UseHangfireModule(this IApplicationBuilder app, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            app.UseHangfireDashboard(DashboardPath, new DashboardOptions
            {
                // Development only: the app usually runs in a container, so requests are not "local".
                Authorization = [new AllowAllDashboardAuthorizationFilter()],
            });
        }

        return app;
    }

    private sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) => true;
    }
}