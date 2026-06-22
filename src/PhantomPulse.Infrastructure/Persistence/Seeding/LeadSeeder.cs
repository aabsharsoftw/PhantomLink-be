using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhantomPulse.Crm.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds realistic demo leads (Contacts with lead fields) scoped to the
/// PhantomCore sub-account. Runs in Development only.
/// </summary>
internal sealed class LeadSeeder(DbContext db, ILogger<LeadSeeder> logger) : IDataSeeder
{
    // Anchor check — if this lead exists, skip the entire batch
    private static readonly Guid AnchorLeadId = new("dddddddd-0001-0000-0000-000000000000");

    // Tenant: SubPhantomCoreId from DemoContactsSeeder
    private static readonly Guid TenantId = new("cccccccc-0010-0000-0000-000000000000");

    public int  Order      => 120;
    public bool IsDemoOnly => true;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Set<Contact>().IgnoreQueryFilters().AnyAsync(c => c.Id == AnchorLeadId, ct))
            return;

        var now   = DateTime.UtcNow;
        var leads = BuildLeads(now);

        db.Set<Contact>().AddRange(leads);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} demo leads", leads.Count);
    }

    private static List<Contact> BuildLeads(DateTime now) =>
    [
        Lead("dddddddd-0001-0000-0000-000000000000",
            "Sana",    "Iqbal",     "sana@levanta.io",          "+91 98103 22018",
            "Levanta",        "Head of Growth",
            score: 92, status: "open",
            tags: ["hot-lead", "mumbai"],
            owner: "Riya Acharya",
            source: "Website",
            notes: "Interested in annual plan. Has 3 sub-brands. Decision maker.",
            lastActivity: now.AddHours(-2)),

        Lead("dddddddd-0002-0000-0000-000000000000",
            "Rohit",   "Menon",     "rohit@arcwise.app",        "+91 99876 11240",
            "Arcwise",        "CTO",
            score: 88, status: "open",
            tags: ["enterprise"],
            owner: "Aman Dubey",
            source: "Referral",
            notes: "Enterprise pilot. Procurement team involved. High intent.",
            lastActivity: now.AddMinutes(-14)),

        Lead("dddddddd-0003-0000-0000-000000000000",
            "Priya",   "Kulkarni",  "priya@northwind.co",       "+91 98201 49832",
            "Northwind",      "Marketing Lead",
            score: 64, status: "open",
            tags: ["newsletter"],
            owner: "Riya Acharya",
            source: "Import",
            notes: "Subscribed to newsletter. Passive interest.",
            lastActivity: now.AddDays(-3)),

        Lead("dddddddd-0004-0000-0000-000000000000",
            "Vikram",  "Shah",      "v.shah@meridian.in",       "+91 98765 41200",
            "Meridian Labs",  "Founder",
            score: 81, status: "open",
            tags: ["hot-lead"],
            owner: "Nikhil Kumar",
            source: "API",
            notes: "Reviewing proposal. Legal sign-off expected next week.",
            lastActivity: now.AddDays(-1)),

        Lead("dddddddd-0005-0000-0000-000000000000",
            "Aditi",   "Rao",       "aditi@orbital.tech",       "+91 90120 84411",
            "Orbital",        "VP Product",
            score: 76, status: "open",
            tags: ["demo-booked"],
            owner: "Aman Dubey",
            source: "Website",
            notes: "Demo confirmed for next Friday at 11am. Team of 12 users.",
            lastActivity: now.AddHours(-1)),

        Lead("dddddddd-0006-0000-0000-000000000000",
            "Faraz",   "Khan",      "faraz@helio.co",           "+91 99008 12340",
            "Helio",          "CEO",
            score: 84, status: "open",
            tags: ["enterprise", "demo-booked"],
            owner: "Riya Acharya",
            source: "Referral",
            notes: "Rescheduling call to Friday 4pm. 200 seat deal.",
            lastActivity: now.AddHours(-2)),

        Lead("dddddddd-0007-0000-0000-000000000000",
            "Meera",   "Iyer",      "meera@dunes.studio",       "+91 89765 21034",
            "Dunes Studio",   "Operations Head",
            score: 58, status: "open",
            tags: ["agency"],
            owner: "Nikhil Kumar",
            source: "Form",
            notes: "Agency retainer inquiry. Opted out of SMS.",
            lastActivity: now.AddHours(-6)),

        Lead("dddddddd-0008-0000-0000-000000000000",
            "Karan",   "Bhatt",     "karan@flux.app",           "+91 99887 76651",
            "Flux",           "Growth Manager",
            score: 79, status: "won",
            tags: ["hot-lead", "trial"],
            owner: "Aman Dubey",
            source: "Campaign",
            notes: "Closed last week. Annual growth plan.",
            lastActivity: now.AddHours(-3)),

        Lead("dddddddd-0009-0000-0000-000000000000",
            "Lina",    "Pereira",   "lina@belvedere.es",        "+34 612 88 04 12",
            "Belvedere",      "Director",
            score: 71, status: "open",
            tags: ["intl", "enterprise"],
            owner: "Riya Acharya",
            source: "Website",
            notes: "International deal. Contract signing this week. €18K.",
            lastActivity: now.AddHours(-5)),

        Lead("dddddddd-0010-0000-0000-000000000000",
            "Sahil",   "Mehta",     "sahil@quay.io",            "+91 98000 14422",
            "Quay",           "Co-founder",
            score: 32, status: "churned",
            tags: ["churned"],
            owner: "Nikhil Kumar",
            source: "Import",
            notes: "Churned 30 days ago. Re-engagement attempted.",
            lastActivity: now.AddDays(-14)),

        Lead("dddddddd-0011-0000-0000-000000000000",
            "Zara",    "Ahmed",     "zara@nexushq.ae",          "+971 50 234 5678",
            "Nexus HQ",       "Head of Sales",
            score: 77, status: "open",
            tags: ["enterprise", "demo-booked"],
            owner: "Aman Dubey",
            source: "Event",
            notes: "Met at GITEX. Wants a platform demo for a 50-person team.",
            lastActivity: now.AddHours(-4)),

        Lead("dddddddd-0012-0000-0000-000000000000",
            "Ishaan",  "Verma",     "ishaan@stackblaze.io",     "+91 97310 88891",
            "Stackblaze",     "Engineering Lead",
            score: 55, status: "open",
            tags: ["trial"],
            owner: "Riya Acharya",
            source: "Website",
            notes: "Started 14-day trial. Light usage so far.",
            lastActivity: now.AddDays(-2)),

        Lead("dddddddd-0013-0000-0000-000000000000",
            "Nadia",   "Al-Rashid", "nadia@crescenttech.ae",    "+971 55 999 1234",
            "Crescent Tech",  "VP Marketing",
            score: 68, status: "open",
            tags: ["intl", "newsletter"],
            owner: "Nikhil Kumar",
            source: "Campaign",
            notes: "Responded to WhatsApp campaign. Wants pricing deck.",
            lastActivity: now.AddDays(-1)),

        Lead("dddddddd-0014-0000-0000-000000000000",
            "Tanvir",  "Hossain",   "tanvir@clearpath.io",      "+880 1712 345678",
            "Clearpath",      "CEO",
            score: 45, status: "lost",
            tags: ["intl"],
            owner: "Aman Dubey",
            source: "Referral",
            notes: "Went with a competitor. Follow up in Q3.",
            lastActivity: now.AddDays(-10)),

        Lead("dddddddd-0015-0000-0000-000000000000",
            "Pooja",   "Nair",      "pooja@brightleaf.co",      "+91 98450 77221",
            "Brightleaf",     "Product Manager",
            score: 83, status: "open",
            tags: ["hot-lead", "demo-booked"],
            owner: "Riya Acharya",
            source: "Form",
            notes: "Inbound from pricing page. High intent. Demo next Tuesday.",
            lastActivity: now.AddMinutes(-30)),

        Lead("dddddddd-0016-0000-0000-000000000000",
            "Carlos",  "Mendez",    "carlos@opusworks.mx",      "+52 55 1234 5678",
            "OpusWorks",      "CTO",
            score: 62, status: "open",
            tags: ["intl", "enterprise"],
            owner: "Nikhil Kumar",
            source: "API",
            notes: "Latin America expansion. Evaluating 3 vendors.",
            lastActivity: now.AddDays(-2)),

        Lead("dddddddd-0017-0000-0000-000000000000",
            "Anjali",  "Singh",     "anjali@velociti.in",       "+91 99000 33445",
            "Veloci.ti",      "Marketing Head",
            score: 70, status: "won",
            tags: ["trial", "newsletter"],
            owner: "Aman Dubey",
            source: "Campaign",
            notes: "Converted from trial. Monthly plan, upgrade expected in 60 days.",
            lastActivity: now.AddHours(-8)),

        Lead("dddddddd-0018-0000-0000-000000000000",
            "Faisal",  "Al-Mansoori", "faisal@goldenvista.ae",  "+971 56 876 5432",
            "Golden Vista RE","Director",
            score: 89, status: "open",
            tags: ["hot-lead", "enterprise", "intl"],
            owner: "Riya Acharya",
            source: "Referral",
            notes: "Real estate conglomerate. 500+ agents. High-value opportunity.",
            lastActivity: now.AddHours(-1)),
    ];

    private static Contact Lead(
        string id, string firstName, string lastName,
        string email, string phone,
        string company, string title,
        int score, string status,
        string[] tags, string owner, string source,
        string notes, DateTime lastActivity)
    => new()
    {
        Id             = Guid.Parse(id),
        TenantId       = TenantId,
        FirstName      = firstName,
        LastName       = lastName,
        Company        = company,
        Title          = title,
        Score          = score,
        Status         = status,
        Tags           = tags,
        OwnerName      = owner,
        Source         = source,
        Notes          = notes,
        LastActivityAt = lastActivity,
        CreatedAt      = lastActivity.AddDays(-new Random(id.GetHashCode()).Next(1, 30)),
        UpdatedAt      = lastActivity,
        Emails = string.IsNullOrWhiteSpace(email) ? [] :
        [
            new ContactEmail { TenantId = TenantId, Email = email, Label = "work", IsPrimary = true,
                CreatedAt = lastActivity, UpdatedAt = lastActivity }
        ],
        Phones = string.IsNullOrWhiteSpace(phone) ? [] :
        [
            new ContactPhone { TenantId = TenantId, Phone = phone, Label = "mobile", IsPrimary = true,
                CreatedAt = lastActivity, UpdatedAt = lastActivity }
        ],
    };
}
