using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Foundation.Authorization;

/// <summary>
/// Defines the default permission set for each system role type.
/// Used when creating a new role during agency/sub-account provisioning.
/// </summary>
public static class RolePermissionMatrix
{
    private static readonly IReadOnlySet<string> All =
        new HashSet<string>(PermissionKeys.All.Select(p => p.Key));

    // ── Agency-level roles ────────────────────────────────────────────────────

    // Full platform and agency access.
    private static readonly IReadOnlySet<string> AgencyOwnerKeys = All;

    // Same as owner but cannot delete users (prevents accidental removal).
    private static readonly IReadOnlySet<string> AgencyAdminKeys = new HashSet<string>(
        All.Where(k => k != PermissionKeys.Users.Delete));

    // ── Sub-account-level roles ───────────────────────────────────────────────

    // Full control within their sub-account (tenant filter limits the scope).
    private static readonly IReadOnlySet<string> AccountAdminKeys = All;

    // Operational lead — can manage content and data, limited admin surface.
    private static readonly IReadOnlySet<string> ManagerKeys = new HashSet<string>([
        PermissionKeys.Dashboard.View,

        PermissionKeys.Contacts.View,
        PermissionKeys.Contacts.Create,
        PermissionKeys.Contacts.Edit,
        PermissionKeys.Contacts.Delete,

        PermissionKeys.Conversations.View,
        PermissionKeys.Conversations.Reply,

        PermissionKeys.Telephony.View,
        PermissionKeys.Telephony.Create,
        PermissionKeys.Telephony.Edit,

        PermissionKeys.LeadManagement.View,
        PermissionKeys.LeadManagement.Create,
        PermissionKeys.LeadManagement.Edit,
        PermissionKeys.LeadManagement.Delete,

        PermissionKeys.Calendars.View,
        PermissionKeys.Calendars.Create,
        PermissionKeys.Calendars.Edit,

        PermissionKeys.Marketing.View,
        PermissionKeys.Marketing.Create,
        PermissionKeys.Marketing.Edit,

        PermissionKeys.Automation.View,
        PermissionKeys.Automation.Create,
        PermissionKeys.Automation.Execute,

        PermissionKeys.AiAgents.View,
        PermissionKeys.AiAgents.Create,

        PermissionKeys.Templates.View,
        PermissionKeys.Templates.Create,
        PermissionKeys.Templates.Edit,

        PermissionKeys.Social.View,
        PermissionKeys.Social.Create,
        PermissionKeys.Social.Edit,

        PermissionKeys.Reputation.View,
        PermissionKeys.Reputation.Manage,

        PermissionKeys.Reporting.View,

        PermissionKeys.Settings.View,
    ]);

    // Standard day-to-day user — read + own-work access, no destructive ops.
    private static readonly IReadOnlySet<string> UserKeys = new HashSet<string>([
        PermissionKeys.Dashboard.View,

        PermissionKeys.Contacts.View,
        PermissionKeys.Contacts.Create,
        PermissionKeys.Contacts.Edit,

        PermissionKeys.Conversations.View,
        PermissionKeys.Conversations.Reply,

        PermissionKeys.Telephony.View,

        PermissionKeys.LeadManagement.View,
        PermissionKeys.LeadManagement.Create,
        PermissionKeys.LeadManagement.Edit,

        PermissionKeys.Calendars.View,
        PermissionKeys.Calendars.Create,

        PermissionKeys.Marketing.View,

        PermissionKeys.Automation.View,
        PermissionKeys.Automation.Execute,

        PermissionKeys.AiAgents.View,

        PermissionKeys.Templates.View,

        PermissionKeys.Social.View,

        PermissionKeys.Reputation.View,

        PermissionKeys.Reporting.View,
    ]);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the permission key set for a given system role type.</summary>
    public static IReadOnlySet<string> GetPermissions(SystemRoleType role) => role switch
    {
        SystemRoleType.PlatformAdmin => All,
        SystemRoleType.AgencyOwner   => AgencyOwnerKeys,
        SystemRoleType.AgencyAdmin   => AgencyAdminKeys,
        SystemRoleType.AccountAdmin  => AccountAdminKeys,
        SystemRoleType.Manager       => ManagerKeys,
        SystemRoleType.User          => UserKeys,
        _                            => UserKeys,
    };

    /// <summary>
    /// Returns the fixed permission GUIDs for a given system role type.
    /// Avoids a DB round-trip when provisioning new roles.
    /// </summary>
    public static IReadOnlySet<Guid> GetPermissionIds(SystemRoleType role) =>
        new HashSet<Guid>(GetPermissions(role).Select(PermissionSeedData.GetId));
}
