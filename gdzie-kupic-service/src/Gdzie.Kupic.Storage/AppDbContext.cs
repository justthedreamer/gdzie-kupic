using Microsoft.EntityFrameworkCore;

namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Domain.Model.Catalogue;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

