namespace PhantomPulse.Foundation.Authorization;

public static class PermissionKeys
{
    public static class Contacts
    {
        public const string View   = "contacts.view";
        public const string Create = "contacts.create";
        public const string Edit   = "contacts.edit";
        public const string Delete = "contacts.delete";
    }

    public static class Leads
    {
        public const string View   = "leads.view";
        public const string Manage = "leads.manage";
    }

    public static class Inbox
    {
        public const string View  = "inbox.view";
        public const string Reply = "inbox.reply";
    }

    public static class Workflows
    {
        public const string View    = "workflows.view";
        public const string Create  = "workflows.create";
        public const string Execute = "workflows.execute";
    }

    public static class Campaigns
    {
        public const string View   = "campaigns.view";
        public const string Create = "campaigns.create";
    }

    public static readonly IReadOnlyList<(string Key, string Description, string Module, string Action)> All =
    [
        (Contacts.View,    "View contacts",          "contacts",  "view"),
        (Contacts.Create,  "Create contacts",        "contacts",  "create"),
        (Contacts.Edit,    "Edit contacts",          "contacts",  "edit"),
        (Contacts.Delete,  "Delete contacts",        "contacts",  "delete"),
        (Leads.View,       "View leads pipeline",    "leads",     "view"),
        (Leads.Manage,     "Manage deals",           "leads",     "manage"),
        (Inbox.View,       "View inbox",             "inbox",     "view"),
        (Inbox.Reply,      "Reply to conversations", "inbox",     "reply"),
        (Workflows.View,   "View workflows",         "workflows", "view"),
        (Workflows.Create, "Create workflows",       "workflows", "create"),
        (Workflows.Execute,"Execute workflows",      "workflows", "execute"),
        (Campaigns.View,   "View campaigns",         "campaigns", "view"),
        (Campaigns.Create, "Create campaigns",       "campaigns", "create"),
    ];
}
