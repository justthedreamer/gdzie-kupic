using Gdzie.Kupic.Domain.Model.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Notifications;

internal sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Endpoint).IsRequired().HasMaxLength(PushSubscription.MaxEndpointLength);

        // A device endpoint belongs to exactly one user at a time.
        builder.HasIndex(s => s.Endpoint).IsUnique();
        builder.HasIndex(s => s.UserId);

        builder.OwnsOne(s => s.Keys, keys =>
        {
            keys.Property(k => k.P256dhKey).HasColumnName("P256dhKey").IsRequired().HasMaxLength(PushSubscription.MaxKeyLength);
            keys.Property(k => k.AuthKey).HasColumnName("AuthKey").IsRequired().HasMaxLength(PushSubscription.MaxKeyLength);
        });
        builder.Navigation(s => s.Keys).IsRequired();

        builder.HasOne(s => s.User)
            .WithMany(u => u.PushSubscriptions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
