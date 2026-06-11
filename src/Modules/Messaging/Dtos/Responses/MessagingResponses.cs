namespace PhantomPulse.Messaging.Dtos.Responses;

public sealed record MessageResponse(Guid Id, string Body, string Channel, string Direction, string Status, DateTime CreatedAt);

public sealed record ConversationResponse(Guid Id, string WaPhoneNumber, string Status, DateTime LastMessageAt, IReadOnlyList<MessageResponse> Messages);

public record TemplateResponse(
    Guid     Id,
    string   Name,
    string   Channel,
    string   Category,
    string   Status,
    string   Body,
    string[] Variables,
    int      Usage,
    DateTime UpdatedAt
);
