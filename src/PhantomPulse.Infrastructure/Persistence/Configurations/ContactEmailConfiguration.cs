using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Crm.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class ContactEmailConfiguration : IEntityTypeConfiguration<ContactEmail>
{
    public void Configure(EntityTypeBuilder<ContactEmail> b)
    {
        b.Property(e => e.Email).HasMaxLength(256).IsRequired();
        b.Property(e => e.Label).HasMaxLength(50).IsRequired();

        // Prevent duplicate email addresses within the same contact (ignores soft-deleted rows)
        b.HasIndex(e => new { e.ContactId, e.Email })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_contact_emails_unique_email");

        // At most one primary email per contact (ignores soft-deleted rows)
        b.HasIndex(e => e.ContactId)
            .IsUnique()
            .HasFilter("is_primary = true AND is_deleted = false")
            .HasDatabaseName("ix_contact_emails_one_primary");

        b.HasOne(e => e.Contact)
            .WithMany(c => c.Emails)
            .HasForeignKey(e => e.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
