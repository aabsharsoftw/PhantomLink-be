using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using PhantomPulse.Automation.Entities;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;

namespace PhantomPulse.Automation.Services;

public class AutomationService(DbContext db, ITenantContext tenant) : IAutomationTrigger
{
    public Task<List<Workflow>> GetAllAsync(CancellationToken ct = default) => db.Set<Workflow>().ToListAsync(ct);

    public async Task<Workflow> CreateAsync(string name, string trigger, string action, string payload, CancellationToken ct = default)
    {
        var w = new Workflow { TenantId = tenant.TenantId!.Value, Name = name, Trigger = trigger, Action = action, Payload = payload };
        db.Set<Workflow>().Add(w);
        await db.SaveChangesAsync(ct);
        return w;
    }

    public async Task FireAsync(string triggerKey, Guid? contactId, Dictionary<string, string>? context = null, CancellationToken ct = default)
    {
        var workflows = await db.Set<Workflow>().Where(w => w.IsActive && w.Trigger == triggerKey).ToListAsync(ct);
        foreach (var w in workflows)
            BackgroundJob.Enqueue<WorkflowExecutionJob>(x => x.ExecuteAsync(w.Id, contactId, context));
    }
}

public class WorkflowExecutionJob(DbContext db, IMessagingService messaging, IContactService contacts)
{
    public async Task ExecuteAsync(Guid workflowId, Guid? contactId, Dictionary<string, string>? ctx)
    {
        var w = await db.Set<Workflow>().FindAsync(new object[] { workflowId }) ?? throw new InvalidOperationException();
        var opts = JsonSerializer.Deserialize<Dictionary<string, string>>(w.Payload) ?? new Dictionary<string, string>();
        switch (w.Action)
        {
            case "send_whatsapp":
                var phone = ctx?.GetValueOrDefault("phone") ?? opts.GetValueOrDefault("phone") ?? "";
                await messaging.SendTextAsync(phone, Resolve(opts.GetValueOrDefault("message") ?? "", ctx));
                break;
            case "send_template":
                await messaging.SendTemplateAsync(ctx?.GetValueOrDefault("phone") ?? "", opts.GetValueOrDefault("template") ?? "", Array.Empty<string>());
                break;
            case "update_field":
                if (contactId.HasValue)
                    await contacts.UpdateFieldAsync(contactId.Value, opts["field"], Resolve(opts["value"], ctx));
                break;
            case "webhook":
                {
                    using var http = new HttpClient();
                    await http.PostAsJsonAsync(opts["url"], new { workflowId, contactId, context = ctx });
                }
                break;
        }
    }

    private static string Resolve(string t, Dictionary<string, string>? ctx)
    {
        if (ctx is null) return t;
        foreach (var (k, v) in ctx) t = t.Replace($"{{{{{k}}}}}", v);
        return t;
    }
}
