using Microsoft.EntityFrameworkCore;

namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Infrastructure;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;

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
    public DbSet<MerchantSubscription> MerchantSubscriptions => Set<MerchantSubscription>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<MerchantResponse> MerchantResponses => Set<MerchantResponse>();

    // Notifications
    public DbSet<PostNotification> PostNotifications => Set<PostNotification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    // Chat
    public DbSet<ChatThread> ChatThreads => Set<ChatThread>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // Infrastructure
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

