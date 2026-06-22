using Microsoft.EntityFrameworkCore;
using PhantomPulse.Crm.Entities;
using PhantomPulse.SharedKernel.Contracts;

namespace PhantomPulse.Infrastructure.Services;

/// <summary>
/// Seeds a realistic starter dataset for every new tenant created via signup:
/// sample contacts (with multi-email/phone), sample deals spread across pipeline
/// stages, and the five system tags pre-created for the new tenant.
/// Called synchronously from AuthService.SignupAsync after the user record is saved.
/// </summary>
public sealed class TenantProvisioningService(DbContext db) : ITenantProvisioner
{
    public async Task ProvisionAsync(
        Guid agencyId, Guid subAccountId, Guid ownerUserId, string ownerName,
        CancellationToken ct = default)
    {
        // System tags are provisioned first so contact Tags strings can reference them.
        var systemTags = BuildSystemTags(agencyId);
        db.Set<Tag>().AddRange(systemTags);
        await db.SaveChangesAsync(ct);

        var contacts = BuildSampleContacts(agencyId, ownerUserId, ownerName);
        db.Set<Contact>().AddRange(contacts);
        await db.SaveChangesAsync(ct);

        var deals = BuildSampleDeals(agencyId, contacts);
        db.Set<Deal>().AddRange(deals);
        await db.SaveChangesAsync(ct);
    }

    // ── System tag definitions ────────────────────────────────────────────────

    private static Tag[] BuildSystemTags(Guid tenantId) =>
    [
        new Tag { TenantId = tenantId, Name = "⭐ VIP",       Color = "#F59E0B", Description = "High-value, priority contacts",    IsSystem = true },
        new Tag { TenantId = tenantId, Name = "✅ Customer",  Color = "#10B981", Description = "Existing paying customers",         IsSystem = true },
        new Tag { TenantId = tenantId, Name = "🎯 Prospect",  Color = "#6366F1", Description = "Leads being actively qualified",    IsSystem = true },
        new Tag { TenantId = tenantId, Name = "🔥 Hot Lead",  Color = "#EF4444", Description = "High intent, needs quick follow-up", IsSystem = true },
        new Tag { TenantId = tenantId, Name = "📥 Imported",  Color = "#8B5CF6", Description = "Contacts added via bulk import",     IsSystem = true },
    ];

    // ── Sample contacts ───────────────────────────────────────────────────────

    private static Contact[] BuildSampleContacts(Guid tenantId, Guid ownerUserId, string ownerName) =>
    [
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Arjun",
            LastName       = "Sharma",
            Company        = "Sharma Enterprises",
            Title          = "CEO",
            Source         = "referral",
            Tags           = ["demo", "🔥 Hot Lead", "enterprise", "follow-up"],
            Score          = 84,
            Status         = "open",
            Notes          = "Referred by existing client. Interested in enterprise plan. Schedule demo this week.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-1),
            Emails         = [new ContactEmail { TenantId = tenantId, Email = "arjun.sharma@sharmaenterprises.com", Label = "work",   IsPrimary = true }],
            Phones         = [new ContactPhone { TenantId = tenantId, Phone = "+91 98765 43210",                   Label = "mobile", IsPrimary = true }],
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Priya",
            LastName       = "Mehta",
            Company        = "TechSolutions Pvt Ltd",
            Title          = "Marketing Director",
            Source         = "website",
            Tags           = ["demo", "warm-lead", "marketing", "follow-up"],
            Score          = 67,
            Status         = "open",
            Notes          = "Downloaded pricing guide. Requested a demo call. Responds best in mornings.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-3),
            Emails         = [new ContactEmail { TenantId = tenantId, Email = "priya.mehta@techsolutions.in", Label = "work",   IsPrimary = true }],
            Phones         = [new ContactPhone { TenantId = tenantId, Phone = "+91 87654 32109",              Label = "mobile", IsPrimary = true }],
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Rahul",
            LastName       = "Verma",
            Company        = "Verma & Co.",
            Title          = "Operations Manager",
            Source         = "cold-outreach",
            Tags           = ["demo", "enterprise", "upsell", "✅ Customer"],
            Score          = 91,
            Status         = "won",
            Notes          = "Closed Q1 deal (₹2.4L). Follow up in July for multi-seat upsell.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-14),
            Emails         = [new ContactEmail { TenantId = tenantId, Email = "rahul.verma@vermagroup.com", Label = "work",   IsPrimary = true }],
            Phones         = [new ContactPhone { TenantId = tenantId, Phone = "+91 76543 21098",            Label = "mobile", IsPrimary = true }],
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Neha",
            LastName       = "Kapoor",
            Company        = "Kapoor Retail Group",
            Title          = "Procurement Head",
            Source         = "cold-outreach",
            Tags           = ["demo", "re-engage"],
            Score          = 28,
            Status         = "churned",
            Notes          = "Chose competitor (pricing). Re-engage October when their contract renews.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-45),
            Emails         = [new ContactEmail { TenantId = tenantId, Email = "neha.kapoor@kapoorretail.com", Label = "work",   IsPrimary = true }],
            Phones         = [new ContactPhone { TenantId = tenantId, Phone = "+91 65432 10987",              Label = "mobile", IsPrimary = true }],
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Vikram",
            LastName       = "Singh",
            Company        = "Singh Logistics",
            Title          = "Director",
            Source         = "website",
            Tags           = ["demo", "new", "warm-lead", "🎯 Prospect"],
            Score          = 52,
            Status         = "open",
            Notes          = "Submitted contact form. Initial discovery call not yet scheduled.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow,
            Emails         = [new ContactEmail { TenantId = tenantId, Email = "vikram@singhlogistics.com", Label = "work",   IsPrimary = true }],
            Phones         = [new ContactPhone { TenantId = tenantId, Phone = "+91 54321 09876",           Label = "mobile", IsPrimary = true }],
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Aisha",
            LastName       = "Khan",
            Company        = "Khan Fashions LLC",
            Title          = "Managing Partner",
            Source         = "social",
            Tags           = ["demo", "🔥 Hot Lead", "retail", "uae"],
            Score          = 79,
            Status         = "open",
            Notes          = "Engaged via Instagram ad. Looking for WhatsApp CRM for their boutique chain.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddHours(-6),
            Emails         = [new ContactEmail { TenantId = tenantId, Email = "aisha.khan@khanfashions.ae", Label = "work",   IsPrimary = true }],
            Phones         = [new ContactPhone { TenantId = tenantId, Phone = "+971 52 345 6789",           Label = "mobile", IsPrimary = true }],
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Mohammed",
            LastName       = "Al-Rashid",
            Company        = "Al-Rashid Realty",
            Title          = "CEO",
            Source         = "referral",
            Tags           = ["demo", "real-estate", "high-value", "✅ Customer", "⭐ VIP"],
            Score          = 88,
            Status         = "won",
            Notes          = "Closed annual plan. Uses the platform for lead tracking across 3 offices.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-7),
            Emails         = [new ContactEmail { TenantId = tenantId, Email = "m.rashid@alrashidrealty.ae", Label = "work",   IsPrimary = true }],
            Phones         = [new ContactPhone { TenantId = tenantId, Phone = "+971 50 678 9012",           Label = "mobile", IsPrimary = true }],
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Sunita",
            LastName       = "Rao",
            Company        = "Rao Dental Clinic",
            Title          = "Clinic Owner",
            Source         = "import",
            Tags           = ["demo", "healthcare", "smb", "📥 Imported"],
            Score          = 45,
            Status         = "lost",
            Notes          = "Budget constraints. Opted for a free tool. May revisit after 6 months.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-22),
            Emails         = [new ContactEmail { TenantId = tenantId, Email = "sunita.rao@raoclinic.in", Label = "work",   IsPrimary = true }],
            Phones         = [new ContactPhone { TenantId = tenantId, Phone = "+91 93456 78901",         Label = "mobile", IsPrimary = true }],
        },
    ];

    // ── Sample deals ──────────────────────────────────────────────────────────

    private static Deal[] BuildSampleDeals(Guid tenantId, Contact[] contacts)
    {
        var arjun  = contacts[0];
        var priya  = contacts[1];
        var rahul  = contacts[2];
        var vikram = contacts[4];
        var aisha  = contacts[5];
        var rashid = contacts[6];

        return
        [
            new Deal
            {
                TenantId       = tenantId,
                ContactId      = arjun.Id,
                Title          = "Enterprise CRM Package — Sharma Enterprises",
                Value          = 240000m,
                Currency       = "INR",
                Stage          = "Proposal Sent",
                Priority       = "High",
                AssignedUserId = arjun.OwnerId,
            },
            new Deal
            {
                TenantId       = tenantId,
                ContactId      = priya.Id,
                Title          = "Marketing Suite — TechSolutions Pvt Ltd",
                Value          = 84000m,
                Currency       = "INR",
                Stage          = "Qualification",
                Priority       = "Medium",
                AssignedUserId = priya.OwnerId,
            },
            new Deal
            {
                TenantId       = tenantId,
                ContactId      = vikram.Id,
                Title          = "Starter Plan — Singh Logistics",
                Value          = 36000m,
                Currency       = "INR",
                Stage          = "Prospecting",
                Priority       = "Low",
                AssignedUserId = vikram.OwnerId,
            },
            new Deal
            {
                TenantId       = tenantId,
                ContactId      = aisha.Id,
                Title          = "WhatsApp CRM — Khan Fashions LLC",
                Value          = 15000m,
                Currency       = "AED",
                Stage          = "Negotiation",
                Priority       = "High",
                AssignedUserId = aisha.OwnerId,
            },
            new Deal
            {
                TenantId       = tenantId,
                ContactId      = rahul.Id,
                Title          = "Annual Plan — Verma & Co.",
                Value          = 240000m,
                Currency       = "INR",
                Stage          = "Closed Won",
                Priority       = "High",
                AssignedUserId = rahul.OwnerId,
            },
            new Deal
            {
                TenantId       = tenantId,
                ContactId      = rashid.Id,
                Title          = "Multi-Office License — Al-Rashid Realty",
                Value          = 18000m,
                Currency       = "AED",
                Stage          = "Closed Won",
                Priority       = "High",
                AssignedUserId = rashid.OwnerId,
            },
        ];
    }
}
