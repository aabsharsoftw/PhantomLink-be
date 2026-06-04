using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        b.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.PasswordHash).IsRequired();
        b.Property(u => u.Scope).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Email unique per agency (an email cannot exist twice within the same agency)
        b.HasIndex(u => new { u.AgencyId, u.Email }).IsUnique();

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
