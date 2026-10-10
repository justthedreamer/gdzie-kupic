using Gdzie.Kupic.Domain.Model.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Notifications;

internal sealed class DigestDeliveryConfiguration : IEntityTypeConfiguration<DigestDelivery>
{
    public void Configure(EntityTypeBuilder<DigestDelivery> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => new { d.UserId, d.Slot }).IsUnique();
        builder.HasOne<Gdzie.Kupic.Domain.Model.Auth.User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
