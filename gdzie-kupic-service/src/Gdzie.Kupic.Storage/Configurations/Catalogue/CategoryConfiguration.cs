using Gdzie.Kupic.Domain.Model.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Catalogue;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.IsDisabled).HasDefaultValue(false);
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
