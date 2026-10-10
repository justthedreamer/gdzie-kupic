using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Marketplace;

internal sealed class MerchantResponseConfiguration : IEntityTypeConfiguration<MerchantResponse>
{
    public void Configure(EntityTypeBuilder<MerchantResponse> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.State).HasConversion<string>().IsRequired();

        builder.HasIndex(r => new { r.PostId, r.MerchantId }).IsUnique();
        builder.HasIndex(r => new { r.MerchantId, r.UpdatedAt });

        builder.HasOne(r => r.Post)
            .WithMany()
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Merchant>()
            .WithMany()
            .HasForeignKey(r => r.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}