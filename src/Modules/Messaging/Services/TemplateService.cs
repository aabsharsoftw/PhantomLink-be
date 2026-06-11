using Microsoft.EntityFrameworkCore;
using PhantomPulse.Messaging.Dtos.Requests;
using PhantomPulse.Messaging.Entities;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Messaging.Services;

public class TemplateService(DbContext db, ITenantContext tenant)
{
    public async Task<List<MessageTemplate>> GetAllAsync(
        string? channel, string? category, string? search, CancellationToken ct = default)
    {
        var q = db.Set<MessageTemplate>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(channel))
            q = q.Where(t => t.Channel == channel);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(t => t.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(t => t.Name.ToLower().Contains(s) || t.Body.ToLower().Contains(s));
        }

        return await q.OrderByDescending(t => t.UpdatedAt).ToListAsync(ct);
    }

    public Task<MessageTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Set<MessageTemplate>().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<MessageTemplate> CreateAsync(CreateTemplateRequest req, CancellationToken ct = default)
    {
        var t = new MessageTemplate
        {
            TenantId  = tenant.TenantId!.Value,
            Name      = req.Name.Trim().ToLower().Replace(' ', '_'),
            Channel   = req.Channel,
            Category  = req.Category,
            Status    = "Pending",
            Body      = req.Body,
            Variables = req.Variables ?? [],
            Usage     = 0,
        };
        db.Set<MessageTemplate>().Add(t);
        await db.SaveChangesAsync(ct);
        return t;
    }

    public async Task<MessageTemplate> UpdateAsync(Guid id, UpdateTemplateRequest req, CancellationToken ct = default)
    {
        var t = await db.Set<MessageTemplate>().FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Template not found");

        if (req.Name      is not null) t.Name      = req.Name.Trim().ToLower().Replace(' ', '_');
        if (req.Status    is not null) t.Status    = req.Status;
        if (req.Body      is not null) t.Body      = req.Body;
        if (req.Variables is not null) t.Variables = req.Variables;
        t.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return t;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var t = await db.Set<MessageTemplate>().FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Template not found");
        db.Set<MessageTemplate>().Remove(t);
        await db.SaveChangesAsync(ct);
    }
}
