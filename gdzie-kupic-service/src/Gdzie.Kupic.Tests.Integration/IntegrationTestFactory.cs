using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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

        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddScoped<IAuthStorage, AuthStorage>();
        });
    }
}
