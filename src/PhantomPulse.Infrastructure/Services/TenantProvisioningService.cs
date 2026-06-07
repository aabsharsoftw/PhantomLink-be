using Microsoft.EntityFrameworkCore;
using PhantomPulse.Crm.Entities;
using PhantomPulse.SharedKernel.Contracts;

namespace PhantomPulse.Infrastructure.Services;

/// <summary>
/// Seeds a realistic starter dataset for every new tenant created via signup:
/// sample contacts (across open / won / lost / churned statuses), sample deals
/// spread across pipeline stages, and common CRM tags pre-applied.
/// Called synchronously from AuthService.SignupAsync after the user record is saved.
/// </summary>
public sealed class TenantProvisioningService(DbContext db) : ITenantProvisioner
{
    public async Task ProvisionAsync(
        Guid agencyId, Guid subAccountId, Guid ownerUserId, string ownerName,
        CancellationToken ct = default)
    {
        // The new user is agency-scoped (TenantId = agencyId). The EF global filter
        // returns entities where TenantId == caller.TenantId, so sample data must
        // share the same TenantId or it will be invisible to the new owner.
        var contacts = BuildSampleContacts(agencyId, ownerUserId, ownerName);
        db.Set<Contact>().AddRange(contacts);
        await db.SaveChangesAsync(ct);

        var deals = BuildSampleDeals(agencyId, contacts);
        db.Set<Deal>().AddRange(deals);
        await db.SaveChangesAsync(ct);
    }

    private static Contact[] BuildSampleContacts(Guid tenantId, Guid ownerUserId, string ownerName) =>
    [
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Arjun",
            LastName       = "Sharma",
            Email          = "arjun.sharma@sharmaenterprises.com",
            Phone          = "+91 98765 43210",
            Company        = "Sharma Enterprises",
            Title          = "CEO",
            Source         = "referral",
            Tags           = ["demo", "hot-lead", "enterprise", "follow-up"],
            Score          = 84,
            Status         = "open",
            Notes          = "Referred by existing client. Interested in enterprise plan. Schedule demo this week.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-1),
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Priya",
            LastName       = "Mehta",
            Email          = "priya.mehta@techsolutions.in",
            Phone          = "+91 87654 32109",
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
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Rahul",
            LastName       = "Verma",
            Email          = "rahul.verma@vermagroup.com",
            Phone          = "+91 76543 21098",
            Company        = "Verma & Co.",
            Title          = "Operations Manager",
            Source         = "cold-outreach",
            Tags           = ["demo", "enterprise", "upsell"],
            Score          = 91,
            Status         = "won",
            Notes          = "Closed Q1 deal (₹2.4L). Follow up in July for multi-seat upsell.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-14),
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Neha",
            LastName       = "Kapoor",
            Email          = "neha.kapoor@kapoorretail.com",
            Phone          = "+91 65432 10987",
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
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Vikram",
            LastName       = "Singh",
            Email          = "vikram@singhlogistics.com",
            Phone          = "+91 54321 09876",
            Company        = "Singh Logistics",
            Title          = "Director",
            Source         = "website",
            Tags           = ["demo", "new", "warm-lead"],
            Score          = 52,
            Status         = "open",
            Notes          = "Submitted contact form. Initial discovery call not yet scheduled.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow,
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Aisha",
            LastName       = "Khan",
            Email          = "aisha.khan@khanfashions.ae",
            Phone          = "+971 52 345 6789",
            Company        = "Khan Fashions LLC",
            Title          = "Managing Partner",
            Source         = "social",
            Tags           = ["demo", "hot-lead", "retail", "uae"],
            Score          = 79,
            Status         = "open",
            Notes          = "Engaged via Instagram ad. Looking for WhatsApp CRM for their boutique chain.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddHours(-6),
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Mohammed",
            LastName       = "Al-Rashid",
            Email          = "m.rashid@alrashidrealty.ae",
            Phone          = "+971 50 678 9012",
            Company        = "Al-Rashid Realty",
            Title          = "CEO",
            Source         = "referral",
            Tags           = ["demo", "real-estate", "high-value"],
            Score          = 88,
            Status         = "won",
            Notes          = "Closed annual plan. Uses the platform for lead tracking across 3 offices.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-7),
        },
        new Contact
        {
            TenantId       = tenantId,
            FirstName      = "Sunita",
            LastName       = "Rao",
            Email          = "sunita.rao@raoclinic.in",
            Phone          = "+91 93456 78901",
            Company        = "Rao Dental Clinic",
            Title          = "Clinic Owner",
            Source         = "import",
            Tags           = ["demo", "healthcare", "smb"],
            Score          = 45,
            Status         = "lost",
            Notes          = "Budget constraints. Opted for a free tool. May revisit after 6 months.",
            OwnerId        = ownerUserId,
            OwnerName      = ownerName,
            LastActivityAt = DateTime.UtcNow.AddDays(-22),
        },
    ];

    private static Deal[] BuildSampleDeals(Guid tenantId, Contact[] contacts)
    {
        // Index contacts by company for readability
        var arjun   = contacts[0]; // open, hot-lead
        var priya   = contacts[1]; // open, warm
        var rahul   = contacts[2]; // won
        var vikram  = contacts[4]; // open, new
        var aisha   = contacts[5]; // open, hot uae
        var rashid  = contacts[6]; // won

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
