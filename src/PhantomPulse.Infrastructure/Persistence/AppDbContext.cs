using Microsoft.EntityFrameworkCore;
using PhantomPulse.Automation.Entities;
using PhantomPulse.Campaigns.Entities;
using PhantomPulse.Crm.Entities;
using PhantomPulse.Foundation.Entities;
using PhantomPulse.Messaging.Entities;
using PhantomPulse.SharedKernel.Domain;
using System.Reflection;

namespace PhantomPulse.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenant)
    : DbContext(options)
{
    private readonly ITenantContext _tenant = tenant;

    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<SubAccount> SubAccounts => Set<SubAccount>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Contact>      Contacts      => Set<Contact>();
    public DbSet<ContactEmail> ContactEmails => Set<ContactEmail>();
    public DbSet<ContactPhone> ContactPhones => Set<ContactPhone>();
    public DbSet<Deal>         Deals         => Set<Deal>();

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<ChatbotSession> ChatbotSessions => Set<ChatbotSession>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<MessageTemplate> Templates => Set<MessageTemplate>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in b.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            GetType()
                .GetMethod(
                    nameof(ApplyTenantFilter),
                    BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [b]);
        }

        b.Entity<Contact>()
            .Property(x => x.CustomFields)
            .HasColumnType("jsonb");

        b.Entity<Deal>()
            .Property(x => x.CustomFields)
            .HasColumnType("jsonb");
    }

    private void ApplyTenantFilter<T>(ModelBuilder b)
        where T : BaseEntity
    {
        b.Entity<T>()
            .HasQueryFilter(x =>
            _tenant.Scope == UserScope.Platform ||
                x.TenantId == _tenant.TenantId &&
                !x.IsDeleted);
    }
}