using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Foundation.Authorization;

/// <summary>
/// Fixed GUIDs for every permission in the catalog.
/// GUID format: {module_index:08x}-{action_index:04x}-0000-0000-000000000000
///
/// Module index  Action index
/// 01 dashboard  0001 view
/// 02 users      0002 create
/// 03 contacts   0003 edit
/// 04 convs      0004 delete
/// 05 telephony  0005 manage
/// 06 leadmgmt   0006 execute
/// 07 calendars  0007 reply
/// 08 marketing
/// 09 automation
/// 0a aiagents
/// 0b templates
/// 0c social
/// 0d reputation
/// 0e reporting
/// 0f settings
///
/// DO NOT change these values once deployed to any environment.
/// Adding new permissions: add new GUIDs only; never reassign existing ones.
/// </summary>
public static class PermissionSeedData
{
    private static readonly Dictionary<string, Guid> _ids = new()
    {
        [PermissionKeys.Dashboard.View]          = new("00000001-0001-0000-0000-000000000000"),

        [PermissionKeys.Users.View]              = new("00000002-0001-0000-0000-000000000000"),
        [PermissionKeys.Users.Create]            = new("00000002-0002-0000-0000-000000000000"),
        [PermissionKeys.Users.Edit]              = new("00000002-0003-0000-0000-000000000000"),
        [PermissionKeys.Users.Delete]            = new("00000002-0004-0000-0000-000000000000"),

        [PermissionKeys.Contacts.View]           = new("00000003-0001-0000-0000-000000000000"),
        [PermissionKeys.Contacts.Create]         = new("00000003-0002-0000-0000-000000000000"),
        [PermissionKeys.Contacts.Edit]           = new("00000003-0003-0000-0000-000000000000"),
        [PermissionKeys.Contacts.Delete]         = new("00000003-0004-0000-0000-000000000000"),

        [PermissionKeys.Conversations.View]      = new("00000004-0001-0000-0000-000000000000"),
        [PermissionKeys.Conversations.Reply]     = new("00000004-0007-0000-0000-000000000000"),

        [PermissionKeys.Telephony.View]          = new("00000005-0001-0000-0000-000000000000"),
        [PermissionKeys.Telephony.Create]        = new("00000005-0002-0000-0000-000000000000"),
        [PermissionKeys.Telephony.Edit]          = new("00000005-0003-0000-0000-000000000000"),
        [PermissionKeys.Telephony.Delete]        = new("00000005-0004-0000-0000-000000000000"),

        [PermissionKeys.LeadManagement.View]     = new("00000006-0001-0000-0000-000000000000"),
        [PermissionKeys.LeadManagement.Create]   = new("00000006-0002-0000-0000-000000000000"),
        [PermissionKeys.LeadManagement.Edit]     = new("00000006-0003-0000-0000-000000000000"),
        [PermissionKeys.LeadManagement.Delete]   = new("00000006-0004-0000-0000-000000000000"),

        [PermissionKeys.Calendars.View]          = new("00000007-0001-0000-0000-000000000000"),
        [PermissionKeys.Calendars.Create]        = new("00000007-0002-0000-0000-000000000000"),
        [PermissionKeys.Calendars.Edit]          = new("00000007-0003-0000-0000-000000000000"),
        [PermissionKeys.Calendars.Delete]        = new("00000007-0004-0000-0000-000000000000"),

        [PermissionKeys.Marketing.View]          = new("00000008-0001-0000-0000-000000000000"),
        [PermissionKeys.Marketing.Create]        = new("00000008-0002-0000-0000-000000000000"),
        [PermissionKeys.Marketing.Edit]          = new("00000008-0003-0000-0000-000000000000"),
        [PermissionKeys.Marketing.Delete]        = new("00000008-0004-0000-0000-000000000000"),

        [PermissionKeys.Automation.View]         = new("00000009-0001-0000-0000-000000000000"),
        [PermissionKeys.Automation.Create]       = new("00000009-0002-0000-0000-000000000000"),
        [PermissionKeys.Automation.Execute]      = new("00000009-0006-0000-0000-000000000000"),

        [PermissionKeys.AiAgents.View]           = new("0000000a-0001-0000-0000-000000000000"),
        [PermissionKeys.AiAgents.Create]         = new("0000000a-0002-0000-0000-000000000000"),
        [PermissionKeys.AiAgents.Edit]           = new("0000000a-0003-0000-0000-000000000000"),
        [PermissionKeys.AiAgents.Delete]         = new("0000000a-0004-0000-0000-000000000000"),

        [PermissionKeys.Templates.View]          = new("0000000b-0001-0000-0000-000000000000"),
        [PermissionKeys.Templates.Create]        = new("0000000b-0002-0000-0000-000000000000"),
        [PermissionKeys.Templates.Edit]          = new("0000000b-0003-0000-0000-000000000000"),
        [PermissionKeys.Templates.Delete]        = new("0000000b-0004-0000-0000-000000000000"),

        [PermissionKeys.Social.View]             = new("0000000c-0001-0000-0000-000000000000"),
        [PermissionKeys.Social.Create]           = new("0000000c-0002-0000-0000-000000000000"),
        [PermissionKeys.Social.Edit]             = new("0000000c-0003-0000-0000-000000000000"),
        [PermissionKeys.Social.Delete]           = new("0000000c-0004-0000-0000-000000000000"),

        [PermissionKeys.Reputation.View]         = new("0000000d-0001-0000-0000-000000000000"),
        [PermissionKeys.Reputation.Manage]       = new("0000000d-0005-0000-0000-000000000000"),

        [PermissionKeys.Reporting.View]          = new("0000000e-0001-0000-0000-000000000000"),

        [PermissionKeys.Settings.View]           = new("0000000f-0001-0000-0000-000000000000"),
        [PermissionKeys.Settings.Manage]         = new("0000000f-0005-0000-0000-000000000000"),
    };

    static PermissionSeedData()
    {
        // Fail fast at startup if a key in the catalog has no registered GUID.
        foreach (var (key, _, _, _) in PermissionKeys.All)
            if (!_ids.ContainsKey(key))
                throw new InvalidOperationException($"Missing seed GUID for permission '{key}'. Add it to PermissionSeedData._ids.");
    }

    /// <summary>Returns the fixed GUID for a permission key. Throws if the key is unknown.</summary>
    public static Guid GetId(string key) => _ids[key];

    /// <summary>All Permission objects with stable IDs — consumed by EF HasData() seeding.</summary>
    public static IReadOnlyList<Permission> All =>
    [
        ..PermissionKeys.All.Select(p => new Permission
        {
            Id          = _ids[p.Key],
            Key         = p.Key,
            Description = p.Description,
            Module      = p.Module,
            Action      = p.Action,
        })
    ];
}
