namespace PhantomPulse.Automation.Dtos.Responses;

public sealed record WorkflowResponse(Guid Id, string Name, string Trigger, string Action, string Payload, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
