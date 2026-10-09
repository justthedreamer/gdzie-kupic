using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Marketplace;

internal sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired();
        builder.OwnsOne(m => m.BanDetails, b => { b.Property(bi => bi.BannedAt).HasColumnName("BannedAt"); });
    }
}

internal sealed class MerchantAccountConfiguration : IEntityTypeConfiguration<MerchantAccount>
{
    public void Configure(EntityTypeBuilder<MerchantAccount> builder)
    {
        builder.HasKey(a => a.Id);

        // A user belongs to at most one merchant, so a user can onboard only once.
        builder.HasIndex(a => a.UserId).IsUnique();
        builder.HasIndex(a => a.MerchantId);

        builder.HasOne<Merchant>()
            .WithMany(m => m.Accounts)
            .HasForeignKey(a => a.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MerchantBranchConfiguration : IEntityTypeConfiguration<MerchantBranch>
{
    public void Configure(EntityTypeBuilder<MerchantBranch> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.DisplayName).IsRequired();

        builder.Property(b => b.Coordinates)
            .HasConversion(c => c.ToPoint(), p => Gdzie.Kupic.Domain.Model.Location.Coordinates.FromPoint(p))
            .HasColumnType("geography (point, 4326)")
            .IsRequired();

        builder.HasIndex(b => b.Coordinates).HasMethod("gist");
        builder.HasIndex(b => b.MerchantId);

        builder.HasOne<Merchant>()
            .WithMany(m => m.Branches)
            .HasForeignKey(b => b.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}