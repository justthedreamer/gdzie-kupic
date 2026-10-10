using Gdzie.Kupic.Domain.Model.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Infrastructure;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("Outbox");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).IsRequired();
        builder.Property(o => o.Payload).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(o => o.CreatedAt)
            .HasFilter("\"ProcessedAt\" IS NULL")
            .HasDatabaseName("IX_Outbox_Unprocessed");
    }
}
