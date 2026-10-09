using Microsoft.EntityFrameworkCore;

namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    // Auth
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    // Catalogue
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();

    // Location
    public DbSet<SavedLocation> SavedLocations => Set<SavedLocation>();

    // Marketplace
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantAccount> MerchantAccounts => Set<MerchantAccount>();
    public DbSet<MerchantBranch> MerchantBranches => Set<MerchantBranch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

