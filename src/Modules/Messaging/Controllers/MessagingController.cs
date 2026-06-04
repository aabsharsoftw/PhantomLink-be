using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhantomPulse.Messaging.Dtos.Requests;
using PhantomPulse.Messaging.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;
using System.Text.Json;

namespace PhantomPulse.Messaging.Controllers;

[ApiController]
[Route("messaging")]
public class MessagingController(MessagingService messaging, IConfiguration config) : ControllerBase
{
    [RequirePermission("inbox.reply")]
    [Authorize]
    [HttpPost("send")]
    public async Task<IActionResult> Send(SendMessageRequest req, CancellationToken ct)
    {
        await messaging.SendTextAsync(req.To, req.Message, ct);
        return Ok(ApiResponse<object>.Ok(new { req.To }, "Message sent"));
    }

    [HttpGet("webhook/whatsapp")]
    public IActionResult Verify([FromQuery(Name = "hub.mode")] string mode, [FromQuery(Name = "hub.verify_token")] string token, [FromQuery(Name = "hub.challenge")] string challenge)
    {
        if (mode == "subscribe" && token == config["WhatsApp:WebhookVerifyToken"])
            return Ok(ApiResponse<string>.Ok(challenge, "Webhook verified"));

        return StatusCode(StatusCodes.Status403Forbidden,
            ApiResponse<object>.Fail("Webhook verification failed", new ApiError("forbidden", "Invalid webhook token")));
    }

    [HttpPost("webhook/whatsapp")]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var msgs = doc.RootElement.GetProperty("entry")[0].GetProperty("changes")[0].GetProperty("value").GetProperty("messages");
            foreach (var msg in msgs.EnumerateArray())
                await messaging.HandleInboundAsync(msg.GetProperty("from").GetString()!, msg.GetProperty("text").GetProperty("body").GetString()!, ct);
        }
        catch
        {
        }

        return Ok(ApiResponse<object>.Ok(new { received = true }, "Webhook received"));
    }
}
