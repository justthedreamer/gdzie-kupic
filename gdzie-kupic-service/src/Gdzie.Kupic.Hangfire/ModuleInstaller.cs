using Hangfire;
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

    public static IServiceCollection InstallHangfireModule(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(HangfireSettings.SectionName).Get<HangfireSettings>()
                       ?? new HangfireSettings();

        // Hangfire registers its stock retry filter globally; replace it with the configured one.
        GlobalJobFilters.Filters.Remove<AutomaticRetryAttribute>();

        services.AddHangfire((sp, config) =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseFilter(new AutomaticRetryAttribute
                {
                    Attempts = settings.RetryAttempts,
                    DelaysInSeconds = settings.RetryDelaysInSeconds.Length > 0 ? settings.RetryDelaysInSeconds : null!,
                    LogEvents = true,
                    OnAttemptsExceeded = AttemptsExceededAction.Fail,
                })
                .UseFilter(new JobLoggingFilter(sp.GetRequiredService<ILogger<JobLoggingFilter>>()));

            if (string.Equals(settings.Storage, HangfireSettings.InMemoryStorage, StringComparison.OrdinalIgnoreCase))
            {
                config.UseInMemoryStorage();
                return;
            }

            var connectionString = configuration.GetConnectionString("Postgres")
                                   ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

            config.UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = settings.SchemaName,
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(settings.PollingIntervalSeconds),
                });
        });

        if (settings.ServerEnabled)
        {
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = settings.EffectiveWorkerCount;
                options.SchedulePollingInterval = TimeSpan.FromSeconds(settings.PollingIntervalSeconds);
            });
        }

        services.AddSingleton<IJobScheduler, JobScheduler>();

        return services;
    }

    /// <summary>Exposes the Hangfire dashboard in the Development environment only.</summary>
    public static IApplicationBuilder UseHangfireModule(this IApplicationBuilder app, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            app.UseHangfireDashboard(DashboardPath, new DashboardOptions
            {
                Authorization = [new AllowAllDashboardAuthorizationFilter()],
            });
        }

        return app;
    }
}
