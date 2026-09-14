using Gdzie.Kupic.Domain.Model.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gdzie.Kupic.Storage.Configurations.Auth;

internal sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).IsRequired();
        builder.Property(e => e.ProviderKey).IsRequired();

        // One external identity links to exactly one account.
        builder.HasIndex(e => new { e.Provider, e.ProviderKey }).IsUnique();

        // One local account cannot link more than once to the same provider.
        builder.HasIndex(e => new { e.UserId, e.Provider }).IsUnique();

        builder.HasOne(e => e.User)
            .WithMany(u => u.ExternalLogins)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
