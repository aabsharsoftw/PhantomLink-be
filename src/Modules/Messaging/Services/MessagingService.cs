using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhantomPulse.Messaging.Entities;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;
using System.Linq;

namespace PhantomPulse.Messaging.Services;

public class MessagingService(DbContext db, ITenantContext tenant, IHttpClientFactory http, IConfiguration config) : IMessagingService
{
    private string PhoneNumberId => config["WhatsApp:PhoneNumberId"]!;
    private string AccessToken   => config["WhatsApp:AccessToken"]!;

    public async Task SendTextAsync(string to, string message, CancellationToken ct = default)
    {
        await PostAsync(new { messaging_product = "whatsapp", to, type = "text", text = new { body = message } }, ct);
        await PersistAsync(to, message, "outbound", ct);
    }

    public async Task SendTemplateAsync(string to, string templateName, string[] variables, CancellationToken ct = default)
    {
        await PostAsync(new { messaging_product = "whatsapp", to, type = "template", template = new { name = templateName, language = new { code = "en" } } }, ct);
        await PersistAsync(to, $"[template:{templateName}]", "outbound", ct);
    }

    public async Task HandleInboundAsync(string from, string body, CancellationToken ct = default) =>
        await PersistAsync(from, body, "inbound", ct);

    public Task<List<Conversation>> GetConversationsAsync(string? status, CancellationToken ct = default)
    {
        var q = db.Set<Conversation>().Include(c => c.Messages).AsQueryable();
        if (status is not null) q = q.Where(c => c.Status == status);
        return q.OrderByDescending(c => c.LastMessageAt).ToListAsync(ct);
    }

    private async Task PostAsync(object payload, CancellationToken ct)
    {
        var client = http.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        var json = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        (await client.PostAsync($"https://graph.facebook.com/v20.0/{PhoneNumberId}/messages", json, ct)).EnsureSuccessStatusCode();
    }

    private async Task PersistAsync(string phone, string body, string direction, CancellationToken ct)
    {
        var conv = await db.Set<Conversation>().FirstOrDefaultAsync(c => c.WaPhoneNumber == phone, ct);
        if (conv is null) { conv = new Conversation { TenantId = tenant.TenantId!.Value, WaPhoneNumber = phone }; db.Set<Conversation>().Add(conv); }
        conv.LastMessageAt = DateTime.UtcNow;
        db.Set<Message>().Add(new Message { TenantId = tenant.TenantId!.Value, ConversationId = conv.Id, Body = body, Direction = direction });
        await db.SaveChangesAsync(ct);
    }
}
