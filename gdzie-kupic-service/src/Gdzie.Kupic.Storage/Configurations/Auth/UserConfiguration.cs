using Gdzie.Kupic.Domain.Model.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Auth;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().IsRequired();
        builder.OwnsOne(u => u.BanDetails, b => { b.Property(bi => bi.BannedAt).HasColumnName("BannedAt"); });
    }
}
