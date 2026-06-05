using Microsoft.EntityFrameworkCore;
using Xunit;
using PhantomPulse.Crm.Entities;
using PhantomPulse.Infrastructure.Persistence;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.TenantIsolation.Tests;

public class ContactLeakTests
{
    private static AppDbContext BuildDb(Guid tenantId)
    {
        var tenant = new TenantContext();
        tenant.Set(UserScope.SubAccount, tenantId);
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(opts, tenant);
    }

    [Fact]
    public async Task Contact_query_never_returns_other_tenants_data()
    {
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
        using (var db = BuildDb(tenantA))
        {
            db.Contacts.Add(new Contact { TenantId = tenantA, FirstName = "Alice", Phone = "+911111111111" });
            db.Contacts.Add(new Contact { TenantId = tenantB, FirstName = "Bob",   Phone = "+912222222222" });
            await db.SaveChangesAsync();
        }
        using var dbA = BuildDb(tenantA);
        var a = await dbA.Contacts.ToListAsync();
        Assert.Single(a); Assert.Equal("Alice", a[0].FirstName);

        using var dbB = BuildDb(tenantB);
        var b = await dbB.Contacts.ToListAsync();
        Assert.Single(b); Assert.Equal("Bob", b[0].FirstName);
    }
}
