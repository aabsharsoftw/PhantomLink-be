using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Foundation.Authorization;

/// <summary>
/// Runtime source of truth for the permission catalog and role→permission mappings.
/// Implemented by <c>JsonRolePermissionProvider</c> in the Infrastructure layer.
/// </summary>
public interface IRolePermissionProvider
{
    /// <summary>Returns all permission keys defined in the catalog.</summary>
    IReadOnlyList<string> GetAllPermissionKeys();

    /// <summary>Returns the permission keys assigned to a system role.</summary>
    IReadOnlyList<string> GetPermissionKeys(SystemRoleType roleType);
}
