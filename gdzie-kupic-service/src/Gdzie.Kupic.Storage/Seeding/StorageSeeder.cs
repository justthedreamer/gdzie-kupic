using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Domain.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gdzie.Kupic.Storage.Seeding;

internal sealed class StorageSeeder(
    AppDbContext db,
    IOptions<AdminSeedSettings> adminOptions,
    ILogger<StorageSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedAdminAsync(ct);
        await SeedMockAccountAsync(MockAccounts.Buyer.Id, MockAccounts.Buyer.Email, MockAccounts.Buyer.Password, Role.Buyer, ct);
        await SeedMockAccountAsync(MockAccounts.Merchant.Id, MockAccounts.Merchant.Email, MockAccounts.Merchant.Password, Role.Merchant, ct);
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        var settings = adminOptions.Value;

        var exists = await db.Users.AnyAsync(u => u.Email == settings.Email, ct);
        if (exists) return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(settings.Password);
        var admin = new User(MockAccounts.Admin.Id, settings.Email, passwordHash, Role.Admin, DateTimeOffset.UtcNow);

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded admin account {Email}", settings.Email);
    }

    /// <summary>
    /// Seeds a fixed test account (Buyer or Merchant), analogous to <see cref="SeedAdminAsync"/>.
    /// The account uses a deterministic <see cref="MockAccounts"/> ID so a pre-generated JWT access
    /// token committed to documentation always matches this seeded account. See
    /// <see cref="MockAccounts"/> for the accepted trade-off this relies on.
    /// </summary>
    private async Task SeedMockAccountAsync(Guid id, string email, string password, Role role, CancellationToken ct)
    {
        var exists = await db.Users.AnyAsync(u => u.Email == email, ct);
        if (exists) return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = new User(id, email, passwordHash, role, DateTimeOffset.UtcNow);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded mock {Role} account {Email}", role, email);
    }
}

