using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Crm.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class ContactNoteConfiguration : IEntityTypeConfiguration<ContactNote>
{
    public void Configure(EntityTypeBuilder<ContactNote> b)
    {
        b.Property(n => n.Body).HasMaxLength(4000).IsRequired();

        b.HasOne(n => n.Contact)
            .WithMany(c => c.ContactNotes)
            .HasForeignKey(n => n.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
