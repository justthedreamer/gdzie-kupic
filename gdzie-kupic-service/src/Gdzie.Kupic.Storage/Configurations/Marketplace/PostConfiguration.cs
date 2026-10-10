using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Marketplace;

internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().IsRequired();
        builder.Property(p => p.NotificationDispatchStatus).HasConversion<string>().IsRequired();
        builder.Ignore(p => p.IsUrgent);

        builder.Property(p => p.Coordinates)
            .HasConversion(c => c.ToPoint(), p => Coordinates.FromPoint(p))
            .HasColumnType("geography (point, 4326)")
            .IsRequired();

        builder.HasIndex(p => p.Coordinates).HasMethod("gist");
        builder.HasIndex(p => new { p.Status, p.ExpiresAt });
        builder.HasIndex(p => p.BuyerId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.BuyerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Tag)
            .WithMany()
            .HasForeignKey(p => p.TagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
