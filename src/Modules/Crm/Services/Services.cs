using Microsoft.EntityFrameworkCore;
using PhantomPulse.Crm.Dtos.Requests;
using PhantomPulse.Crm.Entities;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;
using System;
using System.Linq;

namespace PhantomPulse.Crm.Services;

public class ContactService(DbContext db, ITenantContext tenant) : IContactService
{
    public Task<List<Contact>> GetAllAsync(CancellationToken ct = default) => db.Set<Contact>().Include(c => c.Deals).ToListAsync(ct);
    public Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Set<Contact>().Include(c => c.Deals).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<ContactDto?> GetByPhoneAsync(string phone, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FirstOrDefaultAsync(x => x.Phone == phone, ct);
        return c is null ? null : new ContactDto(c.Id, $"{c.FirstName} {c.LastName}", c.Phone, c.Email, c.TenantId);
    }

    public async Task<Contact> CreateAsync(string firstName, string lastName, string email, string phone, string source, CancellationToken ct = default)
    {
        var c = new Contact { TenantId = tenant.TenantId!.Value, FirstName = firstName, LastName = lastName, Email = email, Phone = phone, Source = source };
        db.Set<Contact>().Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }

    public async Task UpdateFieldAsync(Guid contactId, string field, string value, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync(new object[] { contactId }, ct) ?? throw new KeyNotFoundException();
        c.CustomFields[field] = value;
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task AddTagAsync(Guid contactId, string tag, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync(new object[] { contactId }, ct) ?? throw new KeyNotFoundException();
        c.Tags = c.Tags.Append(tag).ToArray();
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync(new object[] { id }, ct) ?? throw new KeyNotFoundException();
        db.Set<Contact>().Remove(c);
        await db.SaveChangesAsync(ct);
    }
}

public class LeadService(DbContext db, ITenantContext tenant)
{
    public async Task<List<Contact>> GetLeadsAsync(
        string? search, string? tag, string? status, CancellationToken ct = default)
    {
        var q = db.Set<Contact>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(c =>
                c.FirstName.ToLower().Contains(s) ||
                c.LastName.ToLower().Contains(s)  ||
                c.Email.ToLower().Contains(s)     ||
                c.Company.ToLower().Contains(s)   ||
                c.Phone.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(tag))
            q = q.Where(c => c.Tags.Contains(tag));

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(c => c.Status == status);

        return await q.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
    }

    public async Task<Contact> CreateAsync(CreateLeadRequest req, CancellationToken ct = default)
    {
        var c = new Contact
        {
            TenantId       = tenant.TenantId!.Value,
            FirstName      = req.FirstName,
            LastName       = req.LastName,
            Email          = req.Email,
            Phone          = req.Phone,
            Company        = req.Company,
            Title          = req.Title,
            Source         = req.Source,
            Notes          = req.Notes,
            OwnerId        = req.OwnerId,
            OwnerName      = req.OwnerName,
            Score          = 50,
            Status         = "open",
            LastActivityAt = DateTime.UtcNow,
        };
        db.Set<Contact>().Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }

    public async Task<Contact> UpdateScoreAsync(Guid id, int delta, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync([id], ct) ?? throw new KeyNotFoundException();
        c.Score        = Math.Max(0, Math.Min(100, c.Score + delta));
        c.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return c;
    }

    public async Task<Contact> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync([id], ct) ?? throw new KeyNotFoundException();
        c.Status    = status;
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return c;
    }
}

public class PipelineService(DbContext db, ITenantContext tenant)
{
    public Task<List<Deal>> GetAllAsync(CancellationToken ct = default) => db.Set<Deal>().Include(d => d.Contact).ToListAsync(ct);

    public async Task<Deal> CreateAsync(Guid contactId, string title, decimal value, string currency, CancellationToken ct = default)
    {
        var d = new Deal { TenantId = tenant.TenantId!.Value, ContactId = contactId, Title = title, Value = value, Currency = currency };
        db.Set<Deal>().Add(d);
        await db.SaveChangesAsync(ct);
        return d;
    }

    public async Task<Deal> MoveStageAsync(Guid dealId, string stage, CancellationToken ct = default)
    {
        var d = await db.Set<Deal>().FindAsync(new object[] { dealId }, ct) ?? throw new KeyNotFoundException();
        d.Stage = stage; d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return d;
    }
}
