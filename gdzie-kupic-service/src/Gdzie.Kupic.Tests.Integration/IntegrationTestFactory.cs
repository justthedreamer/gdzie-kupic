using Gdzie.Kupic.Hangfire;
using Gdzie.Kupic.Location.Google;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>
/// Boots the API in-process (WebApplicationFactory) with the real DI/config wiring, but
/// replaces the <see cref="AppDbContext"/> registration with the EF Core InMemory provider
/// so tests don't need Docker/Postgres. A single instance is shared across the whole test
/// run (see <see cref="IntegrationTestSetup"/>); <see cref="ResetDatabaseAsync"/> recreates
/// the in-memory database between individual tests.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    public Task InitializeAsync() => ResetDatabaseAsync();

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vapid:PublicKey"] = "test-public-key",
            ["Vapid:PrivateKey"] = "test-private-key",
            ["Vapid:Subject"] = "mailto:test@example.com",
            ["App:BaseUrl"] = "https://app.example.com",
        }));

        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddScoped<IAuthStorage, AuthStorage>();
            services.AddScoped<ICatalogueStorage, CatalogueStorage>();
            services.AddScoped<ILocationStorage, LocationStorage>();
            services.AddScoped<IMarketplaceStorage, MarketplaceStorage>();
            services.AddScoped<IPostStorage, PostStorage>();
            services.AddScoped<IResponseStorage, ResponseStorage>();
            services.AddScoped<IChatStorage, ChatStorage>();
            services.AddScoped<IFeedStorage, FeedStorage>();
            services.AddScoped<IMatchingStorage, MatchingStorage>();
            services.AddScoped<IOutboxStorage, OutboxStorage>();
            services.AddScoped<INotificationStorage, NotificationStorage>();
            services.AddSingleton<FakeObjectStorage>();
            services.AddSingleton<Gdzie.Kupic.Chat.IObjectStorage>(sp => sp.GetRequiredService<FakeObjectStorage>());
            services.AddSingleton<RecordingJobScheduler>();
            services.AddSingleton<IJobScheduler>(sp => sp.GetRequiredService<RecordingJobScheduler>());
            services.AddSingleton<RecordingNotificationDispatcher>();
            services.AddScoped<NotificationDispatcher>();
            services.AddScoped<INotificationDispatcher>(sp => new RecordingDispatcherDecorator(
                sp.GetRequiredService<NotificationDispatcher>(), sp.GetRequiredService<RecordingNotificationDispatcher>()));
            services.AddSingleton<RecordingEmailSender>();
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<RecordingEmailSender>());
            services.AddSingleton<RecordingWebPushSender>();
            services.AddSingleton<IWebPushSender>(sp => sp.GetRequiredService<RecordingWebPushSender>());
            services.AddSingleton<RecordingPostFeedChannel>();
            services.AddSingleton<Gdzie.Kupic.Marketplace.IPostFeedChannel>(sp => sp.GetRequiredService<RecordingPostFeedChannel>());
            services.AddSingleton<RecordingChatChannel>();
            services.AddSingleton<Gdzie.Kupic.Chat.IChatChannel>(sp => sp.GetRequiredService<RecordingChatChannel>());
            services.AddSingleton<RecordingNotificationChannel>();
            services.AddSingleton<Gdzie.Kupic.Notifications.INotificationChannel>(sp => sp.GetRequiredService<RecordingNotificationChannel>());
            services.AddSingleton<FakeGeocodingClient>();
            services.AddScoped<IGoogleGeocodingHttpClient>(sp => sp.GetRequiredService<FakeGeocodingClient>());
        });
    }
}
