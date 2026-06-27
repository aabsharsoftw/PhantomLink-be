using System.Linq.Expressions;
using System.Text.Json;
using PhantomPulse.Crm.Entities;

namespace PhantomPulse.Crm.Services;

/// <summary>
/// Translates a SmartListRuleGroup into an EF-Core-compatible
/// Expression&lt;Func&lt;Contact, bool&gt;&gt; and applies it to an IQueryable.
/// All string comparisons are case-insensitive via .ToLower().
/// </summary>
public static class SmartListRuleEngine
{
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    // ── Public entry-points ────────────────────────────────────────────────────

    public static IQueryable<Contact> Apply(IQueryable<Contact> query, string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return query;
        SmartListRuleGroup? group;
        try { group = JsonSerializer.Deserialize<SmartListRuleGroup>(rulesJson, _opts); }
        catch { return query; }
        if (group is null || group.Conditions.Count == 0) return query;
        return query.Where(BuildGroupPredicate(group));
    }

    public static int Count(IQueryable<Contact> query, string? rulesJson)
        => Apply(query, rulesJson).Count();

    // ── Group ──────────────────────────────────────────────────────────────────

    public static Expression<Func<Contact, bool>> BuildGroupPredicate(SmartListRuleGroup group)
    {
        if (group.Conditions.Count == 0) return _ => true;

        var predicates = group.Conditions.Select(BuildNodePredicate).ToList();

        return predicates.Count == 1
            ? predicates[0]
            : group.Operator.Equals("or", StringComparison.OrdinalIgnoreCase)
                ? predicates.Aggregate(OrElse)
                : predicates.Aggregate(AndAlso);
    }

    private static Expression<Func<Contact, bool>> BuildNodePredicate(SmartListRuleNode node)
    {
        if (node.Group is not null) return BuildGroupPredicate(node.Group);
        if (node.Field is null || node.Operator is null) return _ => true;
        return BuildCondition(node.Field.ToLowerInvariant(), node.Operator.ToLowerInvariant(), node.Value);
    }

    // ── Condition dispatch ─────────────────────────────────────────────────────

    private static Expression<Func<Contact, bool>> BuildCondition(
        string field, string op, JsonElement? value)
    {
        var str = GetString(value);
        return field switch
        {
            "firstname"      => StringProp("FirstName",   op, str),
            "lastname"       => StringProp("LastName",    op, str),
            "company"        => StringProp("Company",     op, str),
            "title"          => StringProp("Title",       op, str),
            "source"         => StringProp("Source",      op, str),
            "status"         => StringProp("Status",      op, str),
            "notes"          => StringProp("Notes",       op, str),
            "ownername"      => StringProp("OwnerName",   op, str),
            "email"          => EmailPredicate(op, str, value),
            "phone"          => PhonePredicate(op, str, value),
            "score"          => ScorePredicate(op, str, value),
            "tags"           => TagsPredicate(op, str, value),
            "createdat"      => DateProp("CreatedAt",     op, str),
            "updatedat"      => DateProp("UpdatedAt",     op, str),
            "lastactivityat" => DateProp("LastActivityAt", op, str),
            "importbatchid"  => ImportBatchPredicate(str),
            _                => _ => true,
        };
    }

    // ── String properties ──────────────────────────────────────────────────────

    private static Expression<Func<Contact, bool>> StringProp(
        string propName, string op, string value)
    {
        var param = Expression.Parameter(typeof(Contact), "c");
        var prop  = Expression.Property(param, propName);

        // EF Core translates .ToLower() → lower() in SQL
        var toLower = Expression.Call(prop,
            typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
        var vLower = Expression.Constant(value.ToLowerInvariant());

        Expression body = op switch
        {
            RuleOp.Equals      => Expression.Equal(toLower, vLower),
            RuleOp.NotEquals   => Expression.NotEqual(toLower, vLower),
            RuleOp.Contains    => StringCall(toLower, "Contains",   vLower),
            RuleOp.NotContains => Expression.Not(StringCall(toLower, "Contains",   vLower)),
            RuleOp.StartsWith  => StringCall(toLower, "StartsWith", vLower),
            RuleOp.EndsWith    => StringCall(toLower, "EndsWith",   vLower),
            RuleOp.IsEmpty     => Expression.Equal(prop, Expression.Constant("")),
            RuleOp.IsNotEmpty  => Expression.NotEqual(prop, Expression.Constant("")),
            RuleOp.In          => BuildStringIn(toLower, value),
            RuleOp.NotIn       => Expression.Not(BuildStringIn(toLower, value)),
            _                  => Expression.Constant(true),
        };
        return Expression.Lambda<Func<Contact, bool>>(body, param);
    }

    private static Expression StringCall(Expression instance, string method, Expression arg)
        => Expression.Call(instance,
            typeof(string).GetMethod(method, [typeof(string)])!,
            arg);

    private static Expression BuildStringIn(Expression propLower, string csv)
    {
        var values = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0) return Expression.Constant(false);
        var exprs = values
            .Select(v => (Expression)Expression.Equal(propLower, Expression.Constant(v.ToLowerInvariant())))
            .ToList();
        return exprs.Aggregate(Expression.OrElse);
    }

    // ── Email (correlated subquery) ────────────────────────────────────────────

    private static Expression<Func<Contact, bool>> EmailPredicate(
        string op, string value, JsonElement? raw)
    {
        var vLower = value.ToLowerInvariant();
        return op switch
        {
            RuleOp.Equals      => c => c.Emails.Any(e => e.Email.ToLower() == vLower),
            RuleOp.Contains    => c => c.Emails.Any(e => e.Email.ToLower().Contains(vLower)),
            RuleOp.NotContains => c => !c.Emails.Any(e => e.Email.ToLower().Contains(vLower)),
            RuleOp.StartsWith  => c => c.Emails.Any(e => e.Email.ToLower().StartsWith(vLower)),
            RuleOp.EndsWith    => c => c.Emails.Any(e => e.Email.ToLower().EndsWith(vLower)),
            RuleOp.IsEmpty     => c => !c.Emails.Any(),
            RuleOp.IsNotEmpty  => c => c.Emails.Any(),
            _                  => _ => true,
        };
    }

    // ── Phone (correlated subquery) ────────────────────────────────────────────

    private static Expression<Func<Contact, bool>> PhonePredicate(
        string op, string value, JsonElement? raw)
    {
        return op switch
        {
            RuleOp.Equals      => c => c.Phones.Any(p => p.Phone == value),
            RuleOp.Contains    => c => c.Phones.Any(p => p.Phone.Contains(value)),
            RuleOp.NotContains => c => !c.Phones.Any(p => p.Phone.Contains(value)),
            RuleOp.StartsWith  => c => c.Phones.Any(p => p.Phone.StartsWith(value)),
            RuleOp.EndsWith    => c => c.Phones.Any(p => p.Phone.EndsWith(value)),
            RuleOp.IsEmpty     => c => !c.Phones.Any(),
            RuleOp.IsNotEmpty  => c => c.Phones.Any(),
            _                  => _ => true,
        };
    }

    // ── Score (numeric) ────────────────────────────────────────────────────────

    private static Expression<Func<Contact, bool>> ScorePredicate(
        string op, string value, JsonElement? raw)
    {
        if (op is RuleOp.Between)
        {
            var nums = ParseNumberArray(raw);
            if (nums.Length >= 2)
            {
                var lo = nums[0]; var hi = nums[1];
                return c => c.Score >= lo && c.Score <= hi;
            }
            return _ => true;
        }

        if (!int.TryParse(value, out var n)) return _ => true;
        return op switch
        {
            RuleOp.Equals      => c => c.Score == n,
            RuleOp.NotEquals   => c => c.Score != n,
            RuleOp.GreaterThan => c => c.Score > n,
            RuleOp.LessThan    => c => c.Score < n,
            _                  => _ => true,
        };
    }

    // ── Tags (PostgreSQL text[] — EF Core translates .Contains → = ANY()) ──────

    private static Expression<Func<Contact, bool>> TagsPredicate(
        string op, string value, JsonElement? raw)
    {
        if (op is RuleOp.IsEmpty)    return c => c.Tags.Length == 0;
        if (op is RuleOp.IsNotEmpty) return c => c.Tags.Length > 0;

        if (op is RuleOp.In or RuleOp.NotIn)
        {
            var tags = ParseStringArray(raw, value);
            if (tags.Length == 0) return _ => op == RuleOp.NotIn;
            Expression<Func<Contact, bool>> anyOf = c => tags.Any(t => c.Tags.Contains(t));
            return op is RuleOp.In ? anyOf : c => !tags.Any(t => c.Tags.Contains(t));
        }

        return op switch
        {
            RuleOp.Contains    => c => c.Tags.Contains(value),
            RuleOp.NotContains => c => !c.Tags.Contains(value),
            _                  => _ => true,
        };
    }

    // ── Date properties ────────────────────────────────────────────────────────

    private static Expression<Func<Contact, bool>> DateProp(
        string propName, string op, string value)
    {
        var param = Expression.Parameter(typeof(Contact), "c");
        var prop  = Expression.Property(param, propName);

        if (op is RuleOp.Between)
        {
            var parts = value.Split(',', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && DateTime.TryParse(parts[0], out var dLo)
                && DateTime.TryParse(parts[1], out var dHi))
            {
                var lo = Expression.Constant(DateTime.SpecifyKind(dLo, DateTimeKind.Utc));
                var hi = Expression.Constant(DateTime.SpecifyKind(dHi.AddDays(1), DateTimeKind.Utc));
                var body = Expression.And(
                    Expression.GreaterThanOrEqual(prop, lo),
                    Expression.LessThan(prop, hi));
                return Expression.Lambda<Func<Contact, bool>>(body, param);
            }
            return _ => true;
        }

        if (!DateTime.TryParse(value, out var dt)) return _ => true;
        var dtUtc = Expression.Constant(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

        Expression dateBody = op switch
        {
            RuleOp.After  => Expression.GreaterThan(prop, dtUtc),
            RuleOp.Before => Expression.LessThan(prop, dtUtc),
            RuleOp.Equals => Expression.And(
                Expression.GreaterThanOrEqual(prop, dtUtc),
                Expression.LessThan(prop, Expression.Constant(DateTime.SpecifyKind(dt.AddDays(1), DateTimeKind.Utc)))),
            _ => Expression.Constant(true),
        };
        return Expression.Lambda<Func<Contact, bool>>(dateBody, param);
    }

    // ── ImportBatchId ──────────────────────────────────────────────────────────

    private static Expression<Func<Contact, bool>> ImportBatchPredicate(string value)
    {
        if (!Guid.TryParse(value, out var id)) return _ => false;
        return c => c.ImportBatchId == id;
    }

    // ── Combining helpers ──────────────────────────────────────────────────────

    private static Expression<Func<Contact, bool>> AndAlso(
        Expression<Func<Contact, bool>> left,
        Expression<Func<Contact, bool>> right)
    {
        var param   = left.Parameters[0];
        var visitor = new ParameterReplacer(right.Parameters[0], param);
        return Expression.Lambda<Func<Contact, bool>>(
            Expression.AndAlso(left.Body, visitor.Visit(right.Body)), param);
    }

    private static Expression<Func<Contact, bool>> OrElse(
        Expression<Func<Contact, bool>> left,
        Expression<Func<Contact, bool>> right)
    {
        var param   = left.Parameters[0];
        var visitor = new ParameterReplacer(right.Parameters[0], param);
        return Expression.Lambda<Func<Contact, bool>>(
            Expression.OrElse(left.Body, visitor.Visit(right.Body)), param);
    }

    // ── JSON helpers ───────────────────────────────────────────────────────────

    private static string GetString(JsonElement? elem)
    {
        if (elem is null) return "";
        return elem.Value.ValueKind switch
        {
            JsonValueKind.String => elem.Value.GetString() ?? "",
            JsonValueKind.Number => elem.Value.ToString(),
            JsonValueKind.Array  => elem.Value.EnumerateArray()
                                       .FirstOrDefault().GetString() ?? "",
            _                    => "",
        };
    }

    private static string[] ParseStringArray(JsonElement? elem, string fallback = "")
    {
        if (elem?.ValueKind == JsonValueKind.Array)
            return elem.Value.EnumerateArray()
                      .Select(e => e.GetString() ?? "")
                      .Where(s => s.Length > 0)
                      .ToArray();

        return fallback.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int[] ParseNumberArray(JsonElement? elem)
    {
        if (elem?.ValueKind == JsonValueKind.Array)
        {
            var nums = new List<int>();
            foreach (var e in elem.Value.EnumerateArray())
                if (e.TryGetInt32(out var n)) nums.Add(n);
            return [.. nums];
        }
        return [];
    }

    // ── Parameter replacer ─────────────────────────────────────────────────────

    private sealed class ParameterReplacer(
        ParameterExpression oldParam,
        ParameterExpression newParam) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == oldParam ? newParam : base.VisitParameter(node);
    }
}
