using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> b)
    {
        b.Property(a => a.Name).HasMaxLength(200).IsRequired();
        b.Property(a => a.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(a => a.Slug).IsUnique();

        b.HasMany(a => a.SubAccounts)
            .WithOne(sa => sa.Agency)
            .HasForeignKey(sa => sa.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
