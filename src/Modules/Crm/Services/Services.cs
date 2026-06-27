using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhantomPulse.Crm.Dtos.Requests;
using PhantomPulse.Crm.Dtos.Responses;
using PhantomPulse.Crm.Entities;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;
using Microsoft.AspNetCore.Http;

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
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set. Ensure the request carries a valid sub-account token.");
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

    public async Task RemoveTagAsync(Guid contactId, string tag, CancellationToken ct = default)
    {
        var c = await db.Set<Contact>().FindAsync([contactId], ct) ?? throw new KeyNotFoundException();
        c.Tags = c.Tags.Where(t => t != tag).ToArray();
        await db.SaveChangesAsync(ct);
    }

    // ── Notes ─────────────────────────────────────────────────────────────────

    public Task<List<ContactNote>> GetNotesAsync(Guid contactId, CancellationToken ct = default)
        => db.Set<ContactNote>()
             .Where(n => n.ContactId == contactId)
             .OrderByDescending(n => n.CreatedAt)
             .ToListAsync(ct);

    public async Task<ContactNote> AddNoteAsync(Guid contactId, string body, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set. Ensure the request carries a valid sub-account token.");
        var note = new ContactNote { TenantId = tenantId, ContactId = contactId, Body = body };
        db.Set<ContactNote>().Add(note);
        await db.SaveChangesAsync(ct);
        return note;
    }

    public async Task DeleteNoteAsync(Guid noteId, CancellationToken ct = default)
    {
        var n = await db.Set<ContactNote>().FindAsync([noteId], ct) ?? throw new KeyNotFoundException();
        db.Set<ContactNote>().Remove(n);
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
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set. Ensure the request carries a valid sub-account token.");
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

        if (req.SmartListIds is { Count: > 0 })
        {
            foreach (var slId in req.SmartListIds)
            {
                db.Set<ContactSmartListMember>().Add(new ContactSmartListMember
                {
                    ContactId   = c.Id,
                    SmartListId = slId,
                    TenantId    = tenantId,
                });
            }
            await db.SaveChangesAsync(ct);
        }

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

    // ── CSV Import ────────────────────────────────────────────────────────────

    public async Task<ImportResultWithSmartList> ImportAsync(
        IFormFile file,
        Dictionary<string, string> mapping,
        string channel,
        bool createSmartList = false,
        string smartListName = "",
        CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set. Ensure the request carries a valid sub-account token.");

        // Pre-create the batch so its ID is available to stamp on each contact
        var batch = new ImportBatch
        {
            TenantId = tenantId,
            FileName = file.FileName,
            Channel  = channel,
        };
        db.Set<ImportBatch>().Add(batch);

        using var stream = file.OpenReadStream();
        var rows = ParseCsv(stream);
        if (rows.Count < 2)
        {
            await db.SaveChangesAsync(ct);
            return new ImportResultWithSmartList(batch.Id, 0, 0, 0, 0, [], null);
        }

        var headers  = rows[0];
        var dataRows = rows.Skip(1).Where(r => r.Any(f => !string.IsNullOrWhiteSpace(f))).ToList();

        // Build lookup: csvHeader → column index
        var headerIndex = headers
            .Select((h, i) => (h.Trim(), i))
            .Where(x => !string.IsNullOrWhiteSpace(x.Item1))
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.First().i);

        // Load existing emails + phones for duplicate detection (tenant-scoped via query filter)
        var existingEmails = (await db.Set<ContactEmail>().Select(e => e.Email.ToLower()).ToListAsync(ct)).ToHashSet();
        var existingPhones = (await db.Set<ContactPhone>().Select(p => p.Phone).ToListAsync(ct)).ToHashSet();

        var imported = 0;
        var skipped  = 0;
        var failed   = 0;
        var errors   = new List<ImportRowError>();

        for (var rowIdx = 0; rowIdx < dataRows.Count; rowIdx++)
        {
            var row     = dataRows[rowIdx];
            var lineNum = rowIdx + 2; // 1-based, row 1 is header

            string Get(string field)
            {
                var csvCol = mapping.FirstOrDefault(m => m.Value == field).Key;
                if (csvCol is null || !headerIndex.TryGetValue(csvCol, out var idx) || idx >= row.Length)
                    return "";
                return row[idx].Trim();
            }

            var firstName = Get("firstName");
            var lastName  = Get("lastName");
            var email     = Get("email");
            var phone     = Get("phone");
            var company   = Get("company");
            var title     = Get("title");
            var source    = Get("source");
            var notes     = Get("notes");
            var ownerName = Get("ownerName");

            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
            {
                errors.Add(new ImportRowError(lineNum, "Name", "First name or last name is required"));
                failed++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            {
                errors.Add(new ImportRowError(lineNum, "Contact", "At least one of email or phone is required"));
                failed++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            {
                errors.Add(new ImportRowError(lineNum, "Email", $"'{email}' is not a valid email address"));
                failed++;
                continue;
            }

            // Duplicate detection
            var isDuplicate = (!string.IsNullOrWhiteSpace(email) && existingEmails.Contains(email.ToLower()))
                           || (!string.IsNullOrWhiteSpace(phone) && existingPhones.Contains(phone));
            if (isDuplicate) { skipped++; continue; }

            var c = new Contact
            {
                TenantId       = tenantId,
                FirstName      = firstName,
                LastName       = lastName,
                Company        = company,
                Title          = title,
                Source         = string.IsNullOrWhiteSpace(source) ? "import" : source.ToLower(),
                Notes          = notes,
                OwnerName      = ownerName,
                Score          = 50,
                Status         = "open",
                LastActivityAt = DateTime.UtcNow,
                ImportBatchId  = batch.Id,
            };

            if (!string.IsNullOrWhiteSpace(email))
            {
                c.Emails.Add(new ContactEmail { TenantId = tenantId, Email = email, Label = "work", IsPrimary = true });
                existingEmails.Add(email.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                c.Phones.Add(new ContactPhone { TenantId = tenantId, Phone = phone, Label = "mobile", IsPrimary = true });
                existingPhones.Add(phone);
            }

            db.Set<Contact>().Add(c);
            imported++;
        }

        batch.Total      = dataRows.Count;
        batch.Imported   = imported;
        batch.Skipped    = skipped;
        batch.Failed     = failed;
        batch.ErrorsJson = JsonSerializer.Serialize(errors);

        Guid? smartListId = null;
        if (createSmartList && imported > 0)
        {
            var listName = string.IsNullOrWhiteSpace(smartListName)
                ? $"Import {DateTime.UtcNow:yyyy-MM-dd}"
                : smartListName;

            var rulesJson = JsonSerializer.Serialize(new
            {
                @operator  = "and",
                conditions = new[]
                {
                    new { field = "importBatchId", @operator = "equals", value = batch.Id.ToString() },
                },
            });

            var sl = new SmartList
            {
                TenantId    = tenantId,
                Name        = listName,
                Color       = "#6366F1",
                Description = $"Contacts from import on {DateTime.UtcNow:yyyy-MM-dd}",
                RulesJson   = rulesJson,
                SortOrder   = 0,
            };
            db.Set<SmartList>().Add(sl);
            smartListId = sl.Id;
        }

        await db.SaveChangesAsync(ct);
        return new ImportResultWithSmartList(batch.Id, dataRows.Count, imported, skipped, failed, errors, smartListId);
    }

    // ── Import History ────────────────────────────────────────────────────────

    public Task<List<ImportBatch>> GetImportHistoryAsync(CancellationToken ct = default)
        => db.Set<ImportBatch>()
             .OrderByDescending(b => b.CreatedAt)
             .ToListAsync(ct);

    public async Task RevertImportAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await db.Set<ImportBatch>().FindAsync([batchId], ct)
            ?? throw new KeyNotFoundException("Import batch not found.");

        if (batch.Status == "reverted")
            throw new InvalidOperationException("This import has already been reverted.");

        var contacts = await db.Set<Contact>()
            .Where(c => c.ImportBatchId == batchId)
            .ToListAsync(ct);

        foreach (var c in contacts)
        {
            c.IsDeleted  = true;
            c.DeletedAt  = DateTime.UtcNow;
        }

        batch.Status    = "reverted";
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static bool IsValidEmail(string email) =>
        System.Text.RegularExpressions.Regex.IsMatch(email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static List<string[]> ParseCsv(Stream stream)
    {
        var rows = new List<string[]>();
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            rows.Add(SplitCsvLine(line));
        }
        return rows;
    }

    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var i      = 0;
        while (i < line.Length)
        {
            if (line[i] == '"')
            {
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i += 2; }
                    else if (line[i] == '"') { i++; break; }
                    else { sb.Append(line[i++]); }
                }
                fields.Add(sb.ToString());
                if (i < line.Length && line[i] == ',') i++;
            }
            else
            {
                var end = line.IndexOf(',', i);
                if (end < 0) { fields.Add(line[i..].Trim()); break; }
                fields.Add(line[i..end].Trim());
                i = end + 1;
            }
        }
        return [.. fields];
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

// ── Tag service ───────────────────────────────────────────────────────────────

public record TagWithCount(Tag Tag, int ContactCount);

public class TagService(DbContext db, ITenantContext tenant)
{
    public async Task<List<TagWithCount>> GetAllAsync(string? search, CancellationToken ct = default)
    {
        var q = db.Set<Tag>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(t => t.Name.ToLower().Contains(s) || t.Description.ToLower().Contains(s));
        }

        var tagList = await q
            .OrderBy(t => !t.IsSystem)  // system tags first
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        // Single query: pull all contact tag arrays, count in memory
        var contactTagArrays = await db.Set<Contact>().Select(c => c.Tags).ToListAsync(ct);
        var tagCounts = contactTagArrays
            .SelectMany(arr => arr)
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return tagList
            .Select(t => new TagWithCount(t, tagCounts.GetValueOrDefault(t.Name, 0)))
            .ToList();
    }

    public Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Set<Tag>().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Tag> CreateAsync(
        string name, string color, string description, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set. Ensure the request carries a valid sub-account token.");

        var dup = await db.Set<Tag>().AnyAsync(t => t.Name.ToLower() == name.ToLower(), ct);
        if (dup) throw new InvalidOperationException($"Tag '{name}' already exists.");

        var tag = new Tag
        {
            TenantId    = tenantId,
            Name        = name,
            Color       = color,
            Description = description,
            IsSystem    = false,
        };
        db.Set<Tag>().Add(tag);
        await db.SaveChangesAsync(ct);
        return tag;
    }

    public async Task<Tag> UpdateAsync(
        Guid id, string name, string color, string description, CancellationToken ct = default)
    {
        var tag = await db.Set<Tag>().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Tag not found.");

        if (!string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var dup = await db.Set<Tag>()
                .AnyAsync(t => t.Name.ToLower() == name.ToLower() && t.Id != id, ct);
            if (dup) throw new InvalidOperationException($"Tag '{name}' already exists.");
        }

        tag.Name        = name;
        tag.Color       = color;
        tag.Description = description;
        tag.UpdatedAt   = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return tag;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await db.Set<Tag>().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Tag not found.");

        if (tag.IsSystem) throw new InvalidOperationException("System tags cannot be deleted.");

        db.Set<Tag>().Remove(tag);
        await db.SaveChangesAsync(ct);
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
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set. Ensure the request carries a valid sub-account token.");
        var d = new Deal
        {
            TenantId  = tenantId,
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

// ── Smart List service ────────────────────────────────────────────────────────

public class SmartListService(DbContext db, ITenantContext tenant)
{
    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<(SmartList List, int Count)>> GetAllAsync(CancellationToken ct = default)
    {
        var lists = await db.Set<SmartList>()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        var baseQuery = db.Set<Contact>()
            .Include(c => c.Emails)
            .Include(c => c.Phones);

        var result = new List<(SmartList, int)>();
        foreach (var sl in lists)
        {
            var ruleIds  = SmartListRuleEngine.Apply(baseQuery.AsQueryable(), sl.RulesJson).Select(c => c.Id);
            var memberIds = db.Set<ContactSmartListMember>()
                .Where(m => m.SmartListId == sl.Id)
                .Select(m => m.ContactId);
            var count = await ruleIds.Union(memberIds).CountAsync(ct);
            result.Add((sl, count));
        }
        return result;
    }

    public Task<SmartList?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Set<SmartList>().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<PagedData<Contact>> GetContactsAsync(
        Guid id, string? search, PaginationQuery pagination, CancellationToken ct = default)
    {
        var sl = await db.Set<SmartList>().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new KeyNotFoundException("Smart list not found.");

        var ruleMatchedIds = SmartListRuleEngine.Apply(
                db.Set<Contact>().AsQueryable(), sl.RulesJson)
            .Select(c => c.Id);

        var manualMemberIds = db.Set<ContactSmartListMember>()
            .Where(m => m.SmartListId == id)
            .Select(m => m.ContactId);

        var allIds = ruleMatchedIds.Union(manualMemberIds);

        var q = db.Set<Contact>()
            .Include(c => c.Emails)
            .Include(c => c.Phones)
            .Where(c => allIds.Contains(c.Id));

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

        q = q.OrderByDescending(c => c.CreatedAt);

        var total   = await q.CountAsync(ct);
        var page    = Math.Max(1, pagination.Page);
        var size    = Math.Clamp(pagination.PageSize, 1, 200);
        var items   = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        return new PagedData<Contact>
        {
            Items      = items,
            Page       = page,
            PageSize   = size,
            TotalCount = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size),
        };
    }

    public async Task<int> PreviewCountAsync(string rulesJson, CancellationToken ct = default)
    {
        var q = db.Set<Contact>()
            .Include(c => c.Emails)
            .Include(c => c.Phones)
            .AsQueryable();
        return await SmartListRuleEngine.Apply(q, rulesJson).CountAsync(ct);
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task<SmartList> CreateAsync(
        string name, string color, string description, string rulesJson,
        CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set.");

        var maxOrder = await db.Set<SmartList>()
            .OrderByDescending(s => s.SortOrder)
            .Select(s => (int?)s.SortOrder)
            .FirstOrDefaultAsync(ct) ?? -1;

        var sl = new SmartList
        {
            TenantId    = tenantId,
            Name        = name,
            Color       = color,
            Description = description,
            RulesJson   = rulesJson,
            SortOrder   = maxOrder + 1,
        };
        db.Set<SmartList>().Add(sl);
        await db.SaveChangesAsync(ct);
        return sl;
    }

    public async Task<SmartList> UpdateAsync(
        Guid id, string name, string color, string description, string rulesJson,
        CancellationToken ct = default)
    {
        var sl = await db.Set<SmartList>().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new KeyNotFoundException("Smart list not found.");

        sl.Name        = name;
        sl.Color       = color;
        sl.Description = description;
        sl.RulesJson   = rulesJson;
        sl.UpdatedAt   = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return sl;
    }

    public async Task<SmartList> RenameAsync(Guid id, string name, CancellationToken ct = default)
    {
        var sl = await db.Set<SmartList>().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new KeyNotFoundException("Smart list not found.");

        if (sl.IsSystem) throw new InvalidOperationException("System lists cannot be renamed.");
        sl.Name      = name;
        sl.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return sl;
    }

    public async Task<SmartList> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set.");

        var src = await db.Set<SmartList>().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new KeyNotFoundException("Smart list not found.");

        var maxOrder = await db.Set<SmartList>()
            .OrderByDescending(s => s.SortOrder)
            .Select(s => (int?)s.SortOrder)
            .FirstOrDefaultAsync(ct) ?? -1;

        var copy = new SmartList
        {
            TenantId    = tenantId,
            Name        = $"{src.Name} (copy)",
            Color       = src.Color,
            Description = src.Description,
            RulesJson   = src.RulesJson,
            SortOrder   = maxOrder + 1,
        };
        db.Set<SmartList>().Add(copy);
        await db.SaveChangesAsync(ct);
        return copy;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sl = await db.Set<SmartList>().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new KeyNotFoundException("Smart list not found.");

        if (sl.IsSystem) throw new InvalidOperationException("System lists cannot be deleted.");
        db.Set<SmartList>().Remove(sl);
        await db.SaveChangesAsync(ct);
    }

    // ── Manual membership ─────────────────────────────────────────────────────

    public async Task<List<SmartList>> GetSmartListsForContactAsync(Guid contactId, CancellationToken ct = default)
    {
        var memberListIds = await db.Set<ContactSmartListMember>()
            .Where(m => m.ContactId == contactId)
            .Select(m => m.SmartListId)
            .ToListAsync(ct);

        return await db.Set<SmartList>()
            .Where(s => memberListIds.Contains(s.Id))
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .ToListAsync(ct);
    }

    public async Task AddMemberAsync(Guid smartListId, Guid contactId, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set.");

        var exists = await db.Set<ContactSmartListMember>()
            .AnyAsync(m => m.SmartListId == smartListId && m.ContactId == contactId, ct);
        if (exists) return;

        db.Set<ContactSmartListMember>().Add(new ContactSmartListMember
        {
            ContactId   = contactId,
            SmartListId = smartListId,
            TenantId    = tenantId,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid smartListId, Guid contactId, CancellationToken ct = default)
    {
        var entry = await db.Set<ContactSmartListMember>()
            .FirstOrDefaultAsync(m => m.SmartListId == smartListId && m.ContactId == contactId, ct);
        if (entry is null) return;
        db.Set<ContactSmartListMember>().Remove(entry);
        await db.SaveChangesAsync(ct);
    }
}
