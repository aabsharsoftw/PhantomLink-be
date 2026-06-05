using System.Text.Json;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Seeding;

/// <summary>
/// Loads the permission catalog and role→permission mappings from the embedded
/// <c>roles_and_permissions.json</c> file. Registered as a singleton.
/// </summary>
public sealed class JsonRolePermissionProvider : IRolePermissionProvider
{
    private readonly IReadOnlyList<string> _allPermissions;
    private readonly IReadOnlyDictionary<SystemRoleType, IReadOnlyList<string>> _matrix;

    public JsonRolePermissionProvider()
    {
        var assembly = typeof(JsonRolePermissionProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "PhantomPulse.Infrastructure.Persistence.Seeding.roles_and_permissions.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'roles_and_permissions.json' not found. " +
                "Ensure it is marked as EmbeddedResource in the Infrastructure project.");

        var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        _allPermissions = root.GetProperty("permissions")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        var matrix = new Dictionary<SystemRoleType, IReadOnlyList<string>>();
        var rolePerms = root.GetProperty("rolePermissions");

        foreach (SystemRoleType roleType in Enum.GetValues<SystemRoleType>())
        {
            if (rolePerms.TryGetProperty(roleType.ToString(), out var permsEl))
            {
                matrix[roleType] = permsEl.EnumerateArray()
                    .Select(e => e.GetString()!)
                    .ToList();
            }
        }

        _matrix = matrix;
    }

    public IReadOnlyList<string> GetAllPermissionKeys() => _allPermissions;

    public IReadOnlyList<string> GetPermissionKeys(SystemRoleType roleType) =>
        _matrix.TryGetValue(roleType, out var keys) ? keys : [];
}
