using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(rt => rt.Token).HasMaxLength(512).IsRequired();
        b.HasIndex(rt => rt.Token).IsUnique();
        b.Property(rt => rt.ReplacedByToken).HasMaxLength(512);
    }
}
