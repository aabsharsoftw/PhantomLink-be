namespace PhantomPulse.Automation.Dtos.Requests;

public sealed record CreateWorkflowRequest(string Name, string Trigger, string Action, string Payload);

public sealed record TriggerRequest(string Key, Guid? ContactId, Dictionary<string, string>? Context);
