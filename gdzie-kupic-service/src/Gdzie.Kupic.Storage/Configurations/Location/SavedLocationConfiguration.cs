using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Domain.Model.Location;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Location;

internal sealed class SavedLocationConfiguration : IEntityTypeConfiguration<SavedLocation>
{
    public void Configure(EntityTypeBuilder<SavedLocation> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.DisplayName).IsRequired();

        builder.Property(l => l.Coordinates)
            .HasConversion(c => c.ToPoint(), p => Coordinates.FromPoint(p))
            .HasColumnType("geography (point, 4326)")
            .IsRequired();

        builder.HasIndex(l => l.Coordinates).HasMethod("gist");
        builder.HasIndex(l => l.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}