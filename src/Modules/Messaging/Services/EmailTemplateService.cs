using Microsoft.EntityFrameworkCore;
using PhantomPulse.Messaging.Dtos.Requests;
using PhantomPulse.Messaging.Entities;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Messaging.Services;

public class EmailTemplateService(DbContext db, ITenantContext tenant)
{
    public async Task<List<EmailTemplate>> GetAllAsync(
        string? category, string? search, CancellationToken ct = default)
    {
        var q = db.Set<EmailTemplate>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(t => t.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(t => t.Name.ToLower().Contains(s) || t.Subject.ToLower().Contains(s) || t.HtmlBody.ToLower().Contains(s));
        }

        return await q.OrderByDescending(t => t.UpdatedAt).ToListAsync(ct);
    }

    public Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Set<EmailTemplate>().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<EmailTemplate> CreateAsync(CreateEmailTemplateRequest req, CancellationToken ct = default)
    {
        var t = new EmailTemplate
        {
            TenantId  = tenant.TenantId!.Value,
            Name      = req.Name.Trim().ToLower().Replace(' ', '_'),
            Subject   = req.Subject,
            HtmlBody  = req.HtmlBody,
            TextBody  = req.TextBody,
            Category  = req.Category,
            Status    = "Pending",
            Variables = req.Variables ?? [],
            Usage     = 0,
        };
        db.Set<EmailTemplate>().Add(t);
        await db.SaveChangesAsync(ct);
        return t;
    }

    public async Task<EmailTemplate> UpdateAsync(Guid id, UpdateEmailTemplateRequest req, CancellationToken ct = default)
    {
        var t = await db.Set<EmailTemplate>().FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Email template not found");

        if (req.Name is not null) t.Name = req.Name.Trim().ToLower().Replace(' ', '_');
        if (req.Subject is not null) t.Subject = req.Subject;
        if (req.HtmlBody is not null) t.HtmlBody = req.HtmlBody;
        if (req.TextBody is not null) t.TextBody = req.TextBody;
        if (req.Status is not null) t.Status = req.Status;
        if (req.Variables is not null) t.Variables = req.Variables;
        t.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return t;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var t = await db.Set<EmailTemplate>().FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Email template not found");
        db.Set<EmailTemplate>().Remove(t);
        await db.SaveChangesAsync(ct);
    }
}
