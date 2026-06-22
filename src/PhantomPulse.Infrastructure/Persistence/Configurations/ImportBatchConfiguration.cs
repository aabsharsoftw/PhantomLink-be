using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Crm.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> b)
    {
        b.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        b.Property(x => x.Channel).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasMaxLength(50).IsRequired();
        b.Property(x => x.ErrorsJson).HasColumnType("text").IsRequired();
    }
}
