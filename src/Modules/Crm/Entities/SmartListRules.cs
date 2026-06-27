using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhantomPulse.Crm.Entities;

// ── Rule tree ─────────────────────────────────────────────────────────────────
// operator: "and" | "or"
// A node is either a leaf condition (field+operator+value) or a nested group.

public class SmartListRuleGroup
{
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "and";

    [JsonPropertyName("conditions")]
    public List<SmartListRuleNode> Conditions { get; set; } = [];
}

public class SmartListRuleNode
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }

    [JsonPropertyName("group")]
    public SmartListRuleGroup? Group { get; set; }
}

// ── Operator constants ─────────────────────────────────────────────────────────

public static class RuleOp
{
    public const string Equals      = "equals";
    public const string NotEquals   = "not_equals";
    public const string Contains    = "contains";
    public const string NotContains = "not_contains";
    public const string StartsWith  = "starts_with";
    public const string EndsWith    = "ends_with";
    public const string IsEmpty     = "is_empty";
    public const string IsNotEmpty  = "is_not_empty";
    public const string GreaterThan = "greater_than";
    public const string LessThan    = "less_than";
    public const string Between     = "between";
    public const string In          = "in";
    public const string NotIn       = "not_in";
    public const string After       = "after";
    public const string Before      = "before";
}
