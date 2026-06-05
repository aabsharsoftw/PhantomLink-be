using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class SubAccountConfiguration : IEntityTypeConfiguration<SubAccount>
{
    public void Configure(EntityTypeBuilder<SubAccount> b)
    {
        b.Property(sa => sa.Name).HasMaxLength(200).IsRequired();
        b.Property(sa => sa.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(sa => sa.Slug).IsUnique();
        b.HasIndex(sa => sa.AgencyId);
    }
}
