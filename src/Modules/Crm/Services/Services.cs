using Microsoft.EntityFrameworkCore;
using PhantomPulse.Crm.Dtos.Requests;
using PhantomPulse.Crm.Entities;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Crm.Services;

public class ContactService(DbContext db, ITenantContext tenant) : IContactService
{
    // ── Queries ───────────────────────────────────────────────────────────────

    public Task<List<Contact>> GetAllAsync(CancellationToken ct = default)
        => db.Set<Contact>()
             .Include(c => c.Deals)
             .Include(c => c.Emails)
             .Include(c => c.Phones)
             .ToListAsync(ct);

    public Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Set<Contact>()
             .Include(c => c.Deals)
             .Include(c => c.Emails)
             .Include(c => c.Phones)
             .FirstOrDefaultAsync(c => c.Id == id, ct);

    // IContactService: look up contact by any phone number
    public async Task<ContactDto?> GetByPhoneAsync(string phone, CancellationToken ct = default)
    {
        var cp = await db.Set<ContactPhone>()
            .Include(p => p.Contact)
                .ThenInclude(c => c.Emails)
            .FirstOrDefaultAsync(p => p.Phone == phone, ct);

        if (cp is null) return null;

        var c            = cp.Contact;
        var primaryEmail = c.Emails.FirstOrDefault(e => e.IsPrimary)?.Email
                        ?? c.Emails.FirstOrDefault()?.Email
                        ?? "";

        return new ContactDto(c.Id, $"{c.FirstName} {c.LastName}".Trim(), phone, primaryEmail, c.TenantId);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<Contact> CreateAsync(
        string firstName, string lastName, string source,
        IEnumerable<EmailInput>? emails,
        IEnumerable<PhoneInput>? phones,
        CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId!.Value;
        var c = new Contact
        {
            TenantId       = tenantId,
            FirstName      = firstName,
            LastName       = lastName,
            Source         = source,
            LastActivityAt = DateTime.UtcNow,
        };

        AttachEmails(c, tenantId, emails);
        AttachPhones(c, tenantId, phones);

        db.Set<Contact>().Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task UpdateFieldAsync(Guid contactId, string field, string value, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync([contactId], ct) ?? throw new KeyNotFoundException();
        c.CustomFields[field] = value;
        c.UpdatedAt           = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task AddTagAsync(Guid contactId, string tag, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync([contactId], ct) ?? throw new KeyNotFoundException();
        if (!c.Tags.Contains(tag))
            c.Tags = [.. c.Tags, tag];
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync([id], ct) ?? throw new KeyNotFoundException();
        db.Set<Contact>().Remove(c);
        await db.SaveChangesAsync(ct);
    }

    // ── Email management ──────────────────────────────────────────────────────

    public async Task<ContactEmail> AddEmailAsync(
        Guid contactId, string email, string label, bool isPrimary, CancellationToken ct = default)
    {
        var contact = await db.Set<Contact>().FindAsync([contactId], ct)
            ?? throw new KeyNotFoundException("Contact not found.");

        var dup = await db.Set<ContactEmail>()
            .AnyAsync(e => e.ContactId == contactId && e.Email == email, ct);
        if (dup) throw new InvalidOperationException($"Email '{email}' already exists for this contact.");

        // First email is always primary regardless of the request flag
        var isFirst = !await db.Set<ContactEmail>().AnyAsync(e => e.ContactId == contactId, ct);
        if (isFirst) isPrimary = true;

        if (isPrimary)
            await ClearPrimaryEmailAsync(contactId, ct);

        var ce = new ContactEmail
        {
            TenantId  = contact.TenantId,
            ContactId = contactId,
            Email     = email,
            Label     = label,
            IsPrimary = isPrimary,
        };
        db.Set<ContactEmail>().Add(ce);
        await db.SaveChangesAsync(ct);
        return ce;
    }

    public async Task<ContactEmail> UpdateEmailAsync(
        Guid contactId, Guid emailId, string email, string label, CancellationToken ct = default)
    {
        var ce = await db.Set<ContactEmail>()
            .FirstOrDefaultAsync(e => e.Id == emailId && e.ContactId == contactId, ct)
            ?? throw new KeyNotFoundException("Email not found.");

        // Ensure no duplicate if address is changing
        if (!string.Equals(ce.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var dup = await db.Set<ContactEmail>()
                .AnyAsync(e => e.ContactId == contactId && e.Email == email && e.Id != emailId, ct);
            if (dup) throw new InvalidOperationException($"Email '{email}' already exists for this contact.");
        }

        ce.Email     = email;
        ce.Label     = label;
        ce.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ce;
    }

    public async Task SetPrimaryEmailAsync(Guid contactId, Guid emailId, CancellationToken ct = default)
    {
        var ce = await db.Set<ContactEmail>()
            .FirstOrDefaultAsync(e => e.Id == emailId && e.ContactId == contactId, ct)
            ?? throw new KeyNotFoundException("Email not found.");

        await ClearPrimaryEmailAsync(contactId, ct);
        ce.IsPrimary = true;
        ce.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteEmailAsync(Guid contactId, Guid emailId, CancellationToken ct = default)
    {
        var ce = await db.Set<ContactEmail>()
            .FirstOrDefaultAsync(e => e.Id == emailId && e.ContactId == contactId, ct)
            ?? throw new KeyNotFoundException("Email not found.");

        db.Set<ContactEmail>().Remove(ce);
        await db.SaveChangesAsync(ct);

        // Promote oldest remaining email to primary if we removed the primary
        if (ce.IsPrimary)
        {
            var next = await db.Set<ContactEmail>()
                .Where(e => e.ContactId == contactId)
                .OrderBy(e => e.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (next is not null)
            {
                next.IsPrimary = true;
                next.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    // ── Phone management ──────────────────────────────────────────────────────

    public async Task<ContactPhone> AddPhoneAsync(
        Guid contactId, string phone, string label, bool isPrimary, CancellationToken ct = default)
    {
        var contact = await db.Set<Contact>().FindAsync([contactId], ct)
            ?? throw new KeyNotFoundException("Contact not found.");

        var dup = await db.Set<ContactPhone>()
            .AnyAsync(p => p.ContactId == contactId && p.Phone == phone, ct);
        if (dup) throw new InvalidOperationException($"Phone '{phone}' already exists for this contact.");

        var isFirst = !await db.Set<ContactPhone>().AnyAsync(p => p.ContactId == contactId, ct);
        if (isFirst) isPrimary = true;

        if (isPrimary)
            await ClearPrimaryPhoneAsync(contactId, ct);

        var cp = new ContactPhone
        {
            TenantId  = contact.TenantId,
            ContactId = contactId,
            Phone     = phone,
            Label     = label,
            IsPrimary = isPrimary,
        };
        db.Set<ContactPhone>().Add(cp);
        await db.SaveChangesAsync(ct);
        return cp;
    }

    public async Task<ContactPhone> UpdatePhoneAsync(
        Guid contactId, Guid phoneId, string phone, string label, CancellationToken ct = default)
    {
        var cp = await db.Set<ContactPhone>()
            .FirstOrDefaultAsync(p => p.Id == phoneId && p.ContactId == contactId, ct)
            ?? throw new KeyNotFoundException("Phone not found.");

        if (cp.Phone != phone)
        {
            var dup = await db.Set<ContactPhone>()
                .AnyAsync(p => p.ContactId == contactId && p.Phone == phone && p.Id != phoneId, ct);
            if (dup) throw new InvalidOperationException($"Phone '{phone}' already exists for this contact.");
        }

        cp.Phone     = phone;
        cp.Label     = label;
        cp.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return cp;
    }

    public async Task SetPrimaryPhoneAsync(Guid contactId, Guid phoneId, CancellationToken ct = default)
    {
        var cp = await db.Set<ContactPhone>()
            .FirstOrDefaultAsync(p => p.Id == phoneId && p.ContactId == contactId, ct)
            ?? throw new KeyNotFoundException("Phone not found.");

        await ClearPrimaryPhoneAsync(contactId, ct);
        cp.IsPrimary = true;
        cp.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeletePhoneAsync(Guid contactId, Guid phoneId, CancellationToken ct = default)
    {
        var cp = await db.Set<ContactPhone>()
            .FirstOrDefaultAsync(p => p.Id == phoneId && p.ContactId == contactId, ct)
            ?? throw new KeyNotFoundException("Phone not found.");

        db.Set<ContactPhone>().Remove(cp);
        await db.SaveChangesAsync(ct);

        if (cp.IsPrimary)
        {
            var next = await db.Set<ContactPhone>()
                .Where(p => p.ContactId == contactId)
                .OrderBy(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (next is not null)
            {
                next.IsPrimary = true;
                next.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task ClearPrimaryEmailAsync(Guid contactId, CancellationToken ct)
    {
        var primaries = await db.Set<ContactEmail>()
            .Where(e => e.ContactId == contactId && e.IsPrimary)
            .ToListAsync(ct);
        foreach (var e in primaries) { e.IsPrimary = false; e.UpdatedAt = DateTime.UtcNow; }
    }

    private async Task ClearPrimaryPhoneAsync(Guid contactId, CancellationToken ct)
    {
        var primaries = await db.Set<ContactPhone>()
            .Where(p => p.ContactId == contactId && p.IsPrimary)
            .ToListAsync(ct);
        foreach (var p in primaries) { p.IsPrimary = false; p.UpdatedAt = DateTime.UtcNow; }
    }

    private static void AttachEmails(Contact c, Guid tenantId, IEnumerable<EmailInput>? emails)
    {
        if (emails is null) return;
        var list      = emails.ToList();
        var hasPrimary = list.Any(e => e.IsPrimary);

        for (var i = 0; i < list.Count; i++)
        {
            var input = list[i];
            c.Emails.Add(new ContactEmail
            {
                TenantId  = tenantId,
                Email     = input.Email,
                Label     = input.Label,
                // If no entry is flagged primary, the first one becomes primary
                IsPrimary = hasPrimary ? input.IsPrimary : i == 0,
            });
        }
    }

    private static void AttachPhones(Contact c, Guid tenantId, IEnumerable<PhoneInput>? phones)
    {
        if (phones is null) return;
        var list      = phones.ToList();
        var hasPrimary = list.Any(p => p.IsPrimary);

        for (var i = 0; i < list.Count; i++)
        {
            var input = list[i];
            c.Phones.Add(new ContactPhone
            {
                TenantId  = tenantId,
                Phone     = input.Phone,
                Label     = input.Label,
                IsPrimary = hasPrimary ? input.IsPrimary : i == 0,
            });
        }
    }
}

// ── Lead service ──────────────────────────────────────────────────────────────

public class LeadService(DbContext db, ITenantContext tenant)
{
    public async Task<List<Contact>> GetLeadsAsync(
        string? search, string? tag, string? status, CancellationToken ct = default)
    {
        var q = db.Set<Contact>()
            .Include(c => c.Emails)
            .Include(c => c.Phones)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(c =>
                c.FirstName.ToLower().Contains(s) ||
                c.LastName.ToLower().Contains(s)  ||
                c.Company.ToLower().Contains(s)   ||
                c.Emails.Any(e => e.Email.ToLower().Contains(s)) ||
                c.Phones.Any(p => p.Phone.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(tag))
            q = q.Where(c => c.Tags.Contains(tag));

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(c => c.Status == status);

        return await q.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
    }

    public async Task<Contact> CreateAsync(CreateLeadRequest req, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId!.Value;
        var c = new Contact
        {
            TenantId       = tenantId,
            FirstName      = req.FirstName,
            LastName       = req.LastName,
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

        AttachEmails(c, tenantId, req.Emails);
        AttachPhones(c, tenantId, req.Phones);

        db.Set<Contact>().Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }

    public async Task<Contact> UpdateScoreAsync(Guid id, int delta, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync([id], ct) ?? throw new KeyNotFoundException();
        c.Score     = Math.Max(0, Math.Min(100, c.Score + delta));
        c.UpdatedAt = DateTime.UtcNow;
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

    private static void AttachEmails(Contact c, Guid tenantId, IEnumerable<EmailInput>? emails)
    {
        if (emails is null) return;
        var list       = emails.ToList();
        var hasPrimary = list.Any(e => e.IsPrimary);
        for (var i = 0; i < list.Count; i++)
        {
            var input = list[i];
            c.Emails.Add(new ContactEmail
            {
                TenantId  = tenantId,
                Email     = input.Email,
                Label     = input.Label,
                IsPrimary = hasPrimary ? input.IsPrimary : i == 0,
            });
        }
    }

    private static void AttachPhones(Contact c, Guid tenantId, IEnumerable<PhoneInput>? phones)
    {
        if (phones is null) return;
        var list       = phones.ToList();
        var hasPrimary = list.Any(p => p.IsPrimary);
        for (var i = 0; i < list.Count; i++)
        {
            var input = list[i];
            c.Phones.Add(new ContactPhone
            {
                TenantId  = tenantId,
                Phone     = input.Phone,
                Label     = input.Label,
                IsPrimary = hasPrimary ? input.IsPrimary : i == 0,
            });
        }
    }
}

// ── Pipeline service ──────────────────────────────────────────────────────────

public class PipelineService(DbContext db, ITenantContext tenant)
{
    public Task<List<Deal>> GetAllAsync(CancellationToken ct = default)
        => db.Set<Deal>().Include(d => d.Contact).ToListAsync(ct);

    public async Task<Deal> CreateAsync(
        Guid contactId, string title, decimal value, string currency, CancellationToken ct = default)
    {
        var d = new Deal
        {
            TenantId  = tenant.TenantId!.Value,
            ContactId = contactId,
            Title     = title,
            Value     = value,
            Currency  = currency,
        };
        db.Set<Deal>().Add(d);
        await db.SaveChangesAsync(ct);
        return d;
    }

    public async Task<Deal> MoveStageAsync(Guid dealId, string stage, CancellationToken ct = default)
    {
        var d = await db.Set<Deal>().FindAsync([dealId], ct) ?? throw new KeyNotFoundException();
        d.Stage     = stage;
        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return d;
    }
}
