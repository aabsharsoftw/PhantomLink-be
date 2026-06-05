namespace PhantomPulse.Foundation.Dtos.Responses;

public sealed record LoginResponse(
    string Token,
    string RefreshToken,
    Guid   UserId,
    string Email,
    string Name,
    string Role,
    Guid?  AgencyId,
    Guid?  SubAccountId);

public sealed record UserInfo(
    Guid   Id,
    string FirstName,
    string LastName,
    string Email,
    string Scope,
    Guid?  AgencyId,
    Guid?  SubAccountId);

public sealed record RoleInfo(
    Guid   Id,
    string Name,
    string Scope);

public sealed record UiFlags(
    bool DashboardEnabled,
    bool UsersEnabled,
    bool ContactsEnabled,
    bool DealsEnabled,
    bool ConversationsEnabled,
    bool FormsEnabled,
    bool WorkflowsEnabled,
    bool SettingsEnabled);

public sealed record MeResponse(
    UserInfo              User,
    RoleInfo?             Role,
    IReadOnlyList<string> Permissions,
    UiFlags               Flags);
