using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Marketplace;

internal sealed class MerchantSubscriptionConfiguration : IEntityTypeConfiguration<MerchantSubscription>
{
    public void Configure(EntityTypeBuilder<MerchantSubscription> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.MerchantId);

        builder.HasIndex(s => new { s.MerchantId, s.CategoryId, s.TagId }).IsUnique();

        // PostgreSQL treats NULLs as distinct, so category-level subscriptions need their own unique index.
        builder.HasIndex(s => new { s.MerchantId, s.CategoryId })
            .IsUnique()
            .HasFilter("\"TagId\" IS NULL")
            .HasDatabaseName("IX_MerchantSubscriptions_MerchantId_CategoryId_NoTag");

        builder.HasOne<Merchant>()
            .WithMany()
            .HasForeignKey(s => s.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(s => s.TagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}