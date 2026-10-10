using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Notifications;

internal sealed class PostNotificationConfiguration : IEntityTypeConfiguration<PostNotification>
{
    public void Configure(EntityTypeBuilder<PostNotification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Channel).HasConversion<string>();

        // Deduplication guard: a merchant is notified about a post at most once.
        builder.HasIndex(n => new { n.PostId, n.MerchantId }).IsUnique();

        builder.HasOne(n => n.Post)
            .WithMany()
            .HasForeignKey(n => n.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Merchant>()
            .WithMany()
            .HasForeignKey(n => n.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
