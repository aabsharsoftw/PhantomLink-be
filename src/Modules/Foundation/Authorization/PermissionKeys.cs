namespace PhantomPulse.Foundation.Authorization;

/// <summary>
/// Authoritative list of all permission key constants.
/// Each key follows the pattern: {module}.{action}
/// Add new entries here AND in PermissionSeedData when introducing new permissions.
/// </summary>
public static class PermissionKeys
{
    public static class Dashboard
    {
        public const string View = "dashboard.view";
    }

    public static class Users
    {
        public const string View   = "users.view";
        public const string Create = "users.create";
        public const string Edit   = "users.edit";
        public const string Delete = "users.delete";
    }

    public static class Contacts
    {
        public const string View   = "contacts.view";
        public const string Create = "contacts.create";
        public const string Edit   = "contacts.edit";
        public const string Delete = "contacts.delete";
    }

    public static class Conversations
    {
        public const string View  = "conversations.view";
        public const string Reply = "conversations.reply";
    }

    public static class Telephony
    {
        public const string View   = "telephony.view";
        public const string Create = "telephony.create";
        public const string Edit   = "telephony.edit";
        public const string Delete = "telephony.delete";
    }

    public static class LeadManagement
    {
        public const string View   = "leadmanagement.view";
        public const string Create = "leadmanagement.create";
        public const string Edit   = "leadmanagement.edit";
        public const string Delete = "leadmanagement.delete";
    }

    public static class Calendars
    {
        public const string View   = "calendars.view";
        public const string Create = "calendars.create";
        public const string Edit   = "calendars.edit";
        public const string Delete = "calendars.delete";
    }

    public static class Marketing
    {
        public const string View   = "marketing.view";
        public const string Create = "marketing.create";
        public const string Edit   = "marketing.edit";
        public const string Delete = "marketing.delete";
    }

    public static class Automation
    {
        public const string View    = "automation.view";
        public const string Create  = "automation.create";
        public const string Execute = "automation.execute";
    }

    public static class AiAgents
    {
        public const string View   = "aiagents.view";
        public const string Create = "aiagents.create";
        public const string Edit   = "aiagents.edit";
        public const string Delete = "aiagents.delete";
    }

    public static class Templates
    {
        public const string View   = "templates.view";
        public const string Create = "templates.create";
        public const string Edit   = "templates.edit";
        public const string Delete = "templates.delete";
    }

    public static class Social
    {
        public const string View   = "social.view";
        public const string Create = "social.create";
        public const string Edit   = "social.edit";
        public const string Delete = "social.delete";
    }

    public static class Reputation
    {
        public const string View   = "reputation.view";
        public const string Manage = "reputation.manage";
    }

    public static class Reporting
    {
        public const string View = "reporting.view";
    }

    public static class Settings
    {
        public const string View   = "settings.view";
        public const string Manage = "settings.manage";
    }

    /// <summary>
    /// Ordered catalog consumed by migrations (HasData) and authorization policy registration.
    /// DO NOT reorder — only append. Changing a key requires a data migration.
    /// </summary>
    public static readonly IReadOnlyList<(string Key, string Description, string Module, string Action)> All =
    [
        (Dashboard.View,          "View dashboard",               "dashboard",      "view"),

        (Users.View,              "View users",                   "users",          "view"),
        (Users.Create,            "Create users",                 "users",          "create"),
        (Users.Edit,              "Edit users",                   "users",          "edit"),
        (Users.Delete,            "Delete users",                 "users",          "delete"),

        (Contacts.View,           "View contacts",                "contacts",       "view"),
        (Contacts.Create,         "Create contacts",              "contacts",       "create"),
        (Contacts.Edit,           "Edit contacts",                "contacts",       "edit"),
        (Contacts.Delete,         "Delete contacts",              "contacts",       "delete"),

        (Conversations.View,      "View conversations",           "conversations",  "view"),
        (Conversations.Reply,     "Reply to conversations",       "conversations",  "reply"),

        (Telephony.View,          "View telephony",               "telephony",      "view"),
        (Telephony.Create,        "Make calls",                   "telephony",      "create"),
        (Telephony.Edit,          "Edit telephony settings",      "telephony",      "edit"),
        (Telephony.Delete,        "Delete call records",          "telephony",      "delete"),

        (LeadManagement.View,     "View lead pipeline",           "leadmanagement", "view"),
        (LeadManagement.Create,   "Create leads/deals",           "leadmanagement", "create"),
        (LeadManagement.Edit,     "Edit leads/deals",             "leadmanagement", "edit"),
        (LeadManagement.Delete,   "Delete leads/deals",           "leadmanagement", "delete"),

        (Calendars.View,          "View calendars",               "calendars",      "view"),
        (Calendars.Create,        "Create calendar events",       "calendars",      "create"),
        (Calendars.Edit,          "Edit calendar events",         "calendars",      "edit"),
        (Calendars.Delete,        "Delete calendar events",       "calendars",      "delete"),

        (Marketing.View,          "View marketing campaigns",     "marketing",      "view"),
        (Marketing.Create,        "Create campaigns",             "marketing",      "create"),
        (Marketing.Edit,          "Edit campaigns",               "marketing",      "edit"),
        (Marketing.Delete,        "Delete campaigns",             "marketing",      "delete"),

        (Automation.View,         "View automations",             "automation",     "view"),
        (Automation.Create,       "Create automations",           "automation",     "create"),
        (Automation.Execute,      "Execute automations",          "automation",     "execute"),

        (AiAgents.View,           "View AI agents",               "aiagents",       "view"),
        (AiAgents.Create,         "Create AI agents",             "aiagents",       "create"),
        (AiAgents.Edit,           "Edit AI agents",               "aiagents",       "edit"),
        (AiAgents.Delete,         "Delete AI agents",             "aiagents",       "delete"),

        (Templates.View,          "View templates",               "templates",      "view"),
        (Templates.Create,        "Create templates",             "templates",      "create"),
        (Templates.Edit,          "Edit templates",               "templates",      "edit"),
        (Templates.Delete,        "Delete templates",             "templates",      "delete"),

        (Social.View,             "View social planner",          "social",         "view"),
        (Social.Create,           "Create social posts",          "social",         "create"),
        (Social.Edit,             "Edit social posts",            "social",         "edit"),
        (Social.Delete,           "Delete social posts",          "social",         "delete"),

        (Reputation.View,         "View reputation",              "reputation",     "view"),
        (Reputation.Manage,       "Manage reviews & reputation",  "reputation",     "manage"),

        (Reporting.View,          "View reports",                 "reporting",      "view"),

        (Settings.View,           "View settings",                "settings",       "view"),
        (Settings.Manage,         "Manage settings",              "settings",       "manage"),
    ];
}
