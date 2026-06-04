using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Messaging.Dtos.Responses;
using PhantomPulse.Messaging.Entities;
using PhantomPulse.Messaging.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Messaging.Controllers;

[Authorize]
[ApiController]
[Route("inbox")]
public class InboxController(MessagingService messaging) : ControllerBase
{
    [RequirePermission("inbox.view")]
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] PaginationQuery query, CancellationToken ct)
    {
        var rows = await messaging.GetConversationsAsync(status, ct);
        var shaped = rows.Select(MapConversation).ToList();
        var page = Pagination.Slice(shaped, query);
        return Ok(ApiResponse<PagedData<ConversationResponse>>.Ok(page, "Inbox fetched"));
    }

    private static ConversationResponse MapConversation(Conversation c) => new(
        c.Id,
        c.WaPhoneNumber,
        c.Status,
        c.LastMessageAt,
        c.Messages
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MessageResponse(m.Id, m.Body, m.Channel, m.Direction, m.Status, m.CreatedAt))
            .ToList());
}
