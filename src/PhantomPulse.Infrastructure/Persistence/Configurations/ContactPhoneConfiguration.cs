using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Crm.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class ContactPhoneConfiguration : IEntityTypeConfiguration<ContactPhone>
{
    public void Configure(EntityTypeBuilder<ContactPhone> b)
    {
        b.Property(p => p.Phone).HasMaxLength(50).IsRequired();
        b.Property(p => p.Label).HasMaxLength(50).IsRequired();

        // Prevent duplicate phone numbers within the same contact (ignores soft-deleted rows)
        b.HasIndex(p => new { p.ContactId, p.Phone })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_contact_phones_unique_phone");

        // At most one primary phone per contact (ignores soft-deleted rows)
        b.HasIndex(p => p.ContactId)
            .IsUnique()
            .HasFilter("is_primary = true AND is_deleted = false")
            .HasDatabaseName("ix_contact_phones_one_primary");

        b.HasOne(p => p.Contact)
            .WithMany(c => c.Phones)
            .HasForeignKey(p => p.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
