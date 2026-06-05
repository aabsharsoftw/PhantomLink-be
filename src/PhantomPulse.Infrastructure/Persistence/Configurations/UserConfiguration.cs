using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        b.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.PasswordHash).IsRequired();
        b.Property(u => u.Scope).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Email unique within an agency; covers Agency + SubAccount users.
        b.HasIndex(u => new { u.AgencyId, u.Email }).IsUnique();

        // PostgreSQL treats NULLs as distinct in multi-column indexes, so the index
        // above does NOT enforce uniqueness when agency_id IS NULL (Platform users).
        // This filtered index closes that gap.
        b.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("agency_id IS NULL");

        // Enforce the scope/hierarchy invariant at the DB layer:
        //   Platform  → no agency, no sub-account
        //   Agency    → must have agency, must NOT have sub-account
        //   SubAccount→ must have both agency and sub-account
        b.HasCheckConstraint(
            "ck_users_scope_consistency",
            "(scope = 'Platform'   AND agency_id IS NULL     AND sub_account_id IS NULL) OR " +
            "(scope = 'Agency'     AND agency_id IS NOT NULL AND sub_account_id IS NULL) OR " +
            "(scope = 'SubAccount' AND agency_id IS NOT NULL AND sub_account_id IS NOT NULL)");

        b.HasOne(u => u.Agency)
            .WithMany(a => a.Users)
            .HasForeignKey(u => u.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(u => u.SubAccount)
            .WithMany(sa => sa.Users)
            .HasForeignKey(u => u.SubAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
