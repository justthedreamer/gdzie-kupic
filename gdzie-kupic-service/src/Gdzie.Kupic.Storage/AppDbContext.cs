using Microsoft.EntityFrameworkCore;

namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Storage.Entities.Auth;
using Gdzie.Kupic.Storage.Entities.Catalogue;
using Gdzie.Kupic.Storage.Entities.Chat;
using Gdzie.Kupic.Storage.Entities.Infrastructure;
using Gdzie.Kupic.Storage.Entities.Location;
using Gdzie.Kupic.Storage.Entities.Marketplace;
using Gdzie.Kupic.Storage.Entities.Notifications;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    // Auth
    public DbSet<User> Users => Set<User>();
    public DbSet<BuyerProfile> BuyerProfiles => Set<BuyerProfile>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Coordinates
    public DbSet<SavedLocation> SavedLocations => Set<SavedLocation>();

    // Catalogue
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();

    // Marketplace
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantAccount> MerchantAccounts => Set<MerchantAccount>();
    public DbSet<MerchantBranch> MerchantBranches => Set<MerchantBranch>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<MerchantSubscription> MerchantSubscriptions => Set<MerchantSubscription>();
    public DbSet<MerchantResponse> MerchantResponses => Set<MerchantResponse>();

    // Notifications
    public DbSet<PostNotification> PostNotifications => Set<PostNotification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    // Chat
    public DbSet<ChatThread> ChatThreads => Set<ChatThread>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // Outbox
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Auth ────────────────────────────────────────────────────────────

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).IsRequired();
            e.OwnsOne(u => u.BanDetails, b => { b.Property(bi => bi.BannedAt).HasColumnName("BannedAt"); });
        });

        modelBuilder.Entity<BuyerProfile>(e =>
        {
            e.HasKey(bp => bp.UserId);
            e.Property(bp => bp.DisplayName).IsRequired();
            e.HasOne(bp => bp.User)
                .WithOne(u => u.BuyerProfile)
                .HasForeignKey<BuyerProfile>(bp => bp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExternalLogin>(e =>
        {
            e.HasKey(el => el.Id);
            e.HasIndex(el => new { el.Provider, el.ProviderUserId }).IsUnique();
            e.Property(el => el.Provider).HasConversion<string>().IsRequired();
            e.Property(el => el.ProviderUserId).IsRequired();
            e.HasOne(el => el.BuyerProfile)
                .WithMany(bp => bp.ExternalLogins)
                .HasForeignKey(el => el.BuyerProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasOne(t => t.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Coordinates ────────────────────────────────────────────────────────

        modelBuilder.Entity<SavedLocation>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Coordinates)
                .HasConversion(c => c.ToPoint(), p => Coordinates.FromPoint(p))
                .HasColumnType("geography(Point,4326)");
            e.HasIndex(l => l.Coordinates).HasMethod("GIST");
            e.HasOne(l => l.BuyerProfile)
                .WithMany(bp => bp.SavedLocations)
                .HasForeignKey(l => l.BuyerProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Catalogue ───────────────────────────────────────────────────────

        modelBuilder.Entity<Category>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.Name).IsRequired();
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.CategoryId, t.Name }).IsUnique();
            e.Property(t => t.Name).IsRequired();
            e.HasOne(t => t.Category)
                .WithMany(c => c.Tags)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Marketplace ─────────────────────────────────────────────────────

        modelBuilder.Entity<Merchant>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Name).IsRequired();
            e.OwnsOne(m => m.BanDetails, b => { b.Property(bi => bi.BannedAt).HasColumnName("BannedAt"); });
        });

        modelBuilder.Entity<MerchantAccount>(e =>
        {
            e.HasKey(ma => ma.Id);
            e.HasIndex(ma => ma.UserId).IsUnique();
            e.HasOne(ma => ma.Merchant)
                .WithMany(m => m.MerchantAccounts)
                .HasForeignKey(ma => ma.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ma => ma.User)
                .WithOne(u => u.MerchantAccount)
                .HasForeignKey<MerchantAccount>(ma => ma.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MerchantBranch>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.DisplayName).IsRequired();
            e.Property(b => b.Coordinates)
                .HasConversion(c => c.ToPoint(), p => Coordinates.FromPoint(p))
                .HasColumnType("geography(Point,4326)");
            e.HasIndex(b => b.Coordinates).HasMethod("GIST");
            e.OwnsOne(b => b.ContactDetails, c =>
            {
                c.Property(ci => ci.Phone).HasColumnName("Phone");
                c.Property(ci => ci.Website).HasColumnName("Website");
            });
            e.HasOne(b => b.Merchant)
                .WithMany(m => m.MerchantBranches)
                .HasForeignKey(b => b.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Post>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).IsRequired();
            e.Property(p => p.Coordinates)
                .HasConversion(c => c.ToPoint(), p => Coordinates.FromPoint(p))
                .HasColumnType("geography(Point,4326)");
            e.HasIndex(p => p.Coordinates).HasMethod("GIST");
            e.OwnsOne(p => p.Urgency, u => { u.Property(ui => ui.Deadline).HasColumnName("UrgentDeadline"); });
            e.Property(p => p.Status).HasConversion<string>().IsRequired();
            e.Property(p => p.NotificationDispatchStatus).HasConversion<string>().IsRequired();
            e.HasOne(p => p.BuyerProfile)
                .WithMany(bp => bp.Posts)
                .HasForeignKey(p => p.BuyerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Tag)
                .WithMany()
                .HasForeignKey(p => p.TagId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MerchantSubscription>(e =>
        {
            e.HasKey(ms => ms.Id);
            e.HasIndex(ms => new { ms.MerchantId, ms.CategoryId, ms.TagId }).IsUnique();
            e.HasOne(ms => ms.Merchant)
                .WithMany(m => m.Subscriptions)
                .HasForeignKey(ms => ms.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ms => ms.Category)
                .WithMany()
                .HasForeignKey(ms => ms.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ms => ms.Tag)
                .WithMany()
                .HasForeignKey(ms => ms.TagId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MerchantResponse>(e =>
        {
            e.HasKey(mr => mr.Id);
            e.HasIndex(mr => new { mr.PostId, mr.MerchantId }).IsUnique();
            e.Property(mr => mr.State).HasConversion<string>().IsRequired();
            e.HasOne(mr => mr.Post)
                .WithMany(p => p.MerchantResponses)
                .HasForeignKey(mr => mr.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(mr => mr.Merchant)
                .WithMany()
                .HasForeignKey(mr => mr.MerchantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Notifications ───────────────────────────────────────────────────

        modelBuilder.Entity<PostNotification>(e =>
        {
            e.HasKey(pn => pn.Id);
            e.HasIndex(pn => new { pn.PostId, pn.MerchantId }).IsUnique();
            e.Property(pn => pn.Channel).HasConversion<string>().IsRequired();
            e.HasOne(pn => pn.Post)
                .WithMany()
                .HasForeignKey(pn => pn.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pn => pn.Merchant)
                .WithMany()
                .HasForeignKey(pn => pn.MerchantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PushSubscription>(e =>
        {
            e.HasKey(ps => ps.Id);
            e.Property(ps => ps.Endpoint).IsRequired();
            e.OwnsOne(ps => ps.Keys, k =>
            {
                k.Property(wk => wk.P256dhKey).HasColumnName("P256dhKey").IsRequired();
                k.Property(wk => wk.AuthKey).HasColumnName("AuthKey").IsRequired();
            });
            e.HasOne(ps => ps.User)
                .WithMany(u => u.PushSubscriptions)
                .HasForeignKey(ps => ps.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Chat ────────────────────────────────────────────────────────────

        modelBuilder.Entity<ChatThread>(e =>
        {
            e.HasKey(ct => ct.Id);
            e.HasIndex(ct => new { ct.PostId, ct.MerchantId }).IsUnique();
            e.HasOne(ct => ct.Post)
                .WithMany()
                .HasForeignKey(ct => ct.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ct => ct.Merchant)
                .WithMany()
                .HasForeignKey(ct => ct.MerchantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(cm => cm.Id);
            e.HasOne(cm => cm.Thread)
                .WithMany(ct => ct.Messages)
                .HasForeignKey(cm => cm.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(cm => cm.Sender)
                .WithMany()
                .HasForeignKey(cm => cm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Outbox ──────────────────────────────────────────────────────────

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Type).IsRequired();
            e.Property(o => o.Payload).IsRequired();
        });
    }
}