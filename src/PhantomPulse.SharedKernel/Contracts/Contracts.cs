namespace PhantomPulse.SharedKernel.Contracts;

public interface IMessagingService
{
    Task SendTextAsync(string waPhoneNumber, string message, CancellationToken ct = default);
    Task SendTemplateAsync(string waPhoneNumber, string templateName, string[] variables, CancellationToken ct = default);
}

public interface IContactService
{
    Task<ContactDto?> GetByPhoneAsync(string phone, CancellationToken ct = default);
    Task UpdateFieldAsync(Guid contactId, string field, string value, CancellationToken ct = default);
}

public record ContactDto(Guid Id, string Name, string Phone, string Email, Guid TenantId);

public interface IAutomationTrigger
{
    Task FireAsync(string triggerKey, Guid? contactId, Dictionary<string, string>? context = null, CancellationToken ct = default);
}

/// <summary>
/// Seeds the initial CRM data (sample contacts, leads, deals) for a freshly created tenant.
/// Implemented in Infrastructure so it can reach CRM entities without a Foundation→CRM dependency.
/// </summary>
public interface ITenantProvisioner
{
    Task ProvisionAsync(Guid agencyId, Guid subAccountId, Guid ownerUserId, string ownerName, CancellationToken ct = default);
}
