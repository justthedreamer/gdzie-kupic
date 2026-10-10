using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Chat;

internal sealed class ChatThreadConfiguration : IEntityTypeConfiguration<ChatThread>
{
    public void Configure(EntityTypeBuilder<ChatThread> builder)
    {
        builder.HasKey(t => t.Id);

        // One thread per merchant per post; also what makes thread creation idempotent.
        builder.HasIndex(t => new { t.PostId, t.MerchantId }).IsUnique();
        builder.HasIndex(t => t.MerchantId);

        builder.HasOne(t => t.Post)
            .WithMany()
            .HasForeignKey(t => t.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Merchant>()
            .WithMany()
            .HasForeignKey(t => t.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Messages)
            .WithOne(m => m.Thread)
            .HasForeignKey(m => m.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}