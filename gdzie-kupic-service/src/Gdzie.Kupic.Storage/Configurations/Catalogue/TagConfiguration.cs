using Gdzie.Kupic.Domain.Model.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Catalogue;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired();
        builder.Property(t => t.IsDisabled).HasDefaultValue(false);
        builder.HasIndex(t => new { t.CategoryId, t.Name }).IsUnique();

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Tags)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
