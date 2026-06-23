namespace PhantomPulse.Messaging.Dtos.Requests;

public sealed record SendMessageRequest(string To, string Message);

public record CreateTemplateRequest(
    string   Name,
    string   Channel,
    string   Category,
    string   Body,
    string[] Variables
);

public record UpdateTemplateRequest(
    string?   Name,
    string?   Status,
    string?   Body,
    string[]? Variables
);

public record CreateEmailTemplateRequest(
    string   Name,
    string   Subject,
    string   HtmlBody,
    string   TextBody,
    string   Category,
    string[] Variables
);

public record UpdateEmailTemplateRequest(
    string?   Name,
    string?   Subject,
    string?   HtmlBody,
    string?   TextBody,
    string?   Status,
    string[]? Variables
);
