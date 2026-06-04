using Microsoft.EntityFrameworkCore;
using PhantomPulse.SharedKernel.Domain;
using PhantomPulse.Foundation.Entities;
using PhantomPulse.Crm.Entities;
using PhantomPulse.Messaging.Entities;
using PhantomPulse.Automation.Entities;
using PhantomPulse.Campaigns.Entities;

namespace PhantomPulse.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<Agency>        Agencies       => Set<Agency>();
    public DbSet<SubAccount>    SubAccounts    => Set<SubAccount>();
    public DbSet<User>          Users          => Set<User>();
    public DbSet<Role>          Roles          => Set<Role>();
    public DbSet<Permission>    Permissions    => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken>  RefreshTokens  => Set<RefreshToken>();
    public DbSet<Contact>       Contacts       => Set<Contact>();
    public DbSet<Deal>          Deals          => Set<Deal>();
    public DbSet<Conversation>  Conversations  => Set<Conversation>();
    public DbSet<Message>       Messages       => Set<Message>();
    public DbSet<Workflow>      Workflows      => Set<Workflow>();
    public DbSet<ChatbotSession> ChatbotSessions => Set<ChatbotSession>();
    public DbSet<Campaign>      Campaigns      => Set<Campaign>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var et in b.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(et.ClrType)) continue;
            var m = GetType()
                .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(et.ClrType);
            m.Invoke(null, [b, tenant]);
        }

        b.Entity<Contact>().Property(c => c.CustomFields).HasColumnType("jsonb");
        b.Entity<Deal>().Property(d => d.CustomFields).HasColumnType("jsonb");
    }

    private static void ApplyTenantFilter<T>(ModelBuilder b, ITenantContext ctx) where T : BaseEntity
        => b.Entity<T>().HasQueryFilter(e => e.TenantId == ctx.TenantId && !e.IsDeleted);
}
