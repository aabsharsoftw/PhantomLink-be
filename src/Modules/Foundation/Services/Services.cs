using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Foundation.Dtos.Requests;
using PhantomPulse.Foundation.Dtos.Responses;
using PhantomPulse.Foundation.Entities;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PhantomPulse.Foundation.Services;

public class AuthService(DbContext db, IConfiguration config, IRolePermissionProvider permissionProvider, RbacService rbac, ITenantProvisioner provisioner)
{
    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await db.Set<User>().IgnoreQueryFilters()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        var rt = BuildRefreshToken(user.Id);
        db.Set<RefreshToken>().Add(rt);
        await db.SaveChangesAsync(ct);

        var name = $"{user.FirstName} {user.LastName}".Trim();
        return new AuthResult(GenerateToken(user), rt.Token, user.Id, user.Email, name,
            user.Role?.Name ?? "", user.AgencyId, user.SubAccountId);
    }

    public async Task<AuthResult> SignupAsync(string name, string email, string password, CancellationToken ct = default)
    {
        var exists = await db.Set<User>().IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct);
        if (exists) throw new ArgumentException("An account with this email already exists.");

        var agencyId = Guid.NewGuid();
        var subAccountId = Guid.NewGuid();
        var parts = name.Trim().Split(' ', 2);
        var emailPrefix = email.Split('@')[0].ToLowerInvariant().Replace(".", "-");

        var agency = new Agency
        {
            Id = agencyId,
            Name = $"{parts[0]}'s Agency",
            Slug = $"{emailPrefix}-agency-{agencyId.ToString("N")[..6]}",
            IsActive = true,
        };
        db.Set<Agency>().Add(agency);

        var subAccount = new SubAccount
        {
            Id = subAccountId,
            AgencyId = agencyId,
            Name = $"{parts[0]}'s Account",
            Slug = $"{emailPrefix}-{subAccountId.ToString("N")[..6]}",
            IsActive = true,
        };
        db.Set<SubAccount>().Add(subAccount);

        var ownerRole = new Role
        {
            TenantId = agencyId,
            Name = "Agency Owner",
            Description = "Full access to the agency and all sub-accounts",
            IsSystem = true,
            Scope = RoleScope.Agency,
            SystemRoleType = SystemRoleType.AgencyOwner,
        };
        db.Set<Role>().Add(ownerRole);

        var user = new User
        {
            TenantId = agencyId,
            AgencyId = agencyId,
            SubAccountId = null,
            Scope = UserScope.Agency,
            FirstName = parts[0],
            LastName = parts.Length > 1 ? parts[1] : "",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RoleId = ownerRole.Id,
        };
        db.Set<User>().Add(user);
        await db.SaveChangesAsync(ct);

        var ownerKeys = permissionProvider.GetPermissionKeys(SystemRoleType.AgencyOwner);
        var permIds = await db.Set<Permission>()
            .Where(p => ownerKeys.Contains(p.Key))
            .Select(p => p.Id)
            .ToListAsync(ct);
        db.Set<RolePermission>().AddRange(permIds.Select(id => new RolePermission
        {
            RoleId = ownerRole.Id,
            PermissionId = id,
        }));

        var rt = BuildRefreshToken(user.Id);
        db.Set<RefreshToken>().Add(rt);
        await db.SaveChangesAsync(ct);

        await SubAccountProvisioner.EnsureRolesAsync(db, subAccountId, permissionProvider, ct);
        await provisioner.ProvisionAsync(agencyId, subAccountId, user.Id, name.Trim(), ct);

        return new AuthResult(GenerateToken(user, ownerRole.Name), rt.Token, user.Id, user.Email,
            name.Trim(), ownerRole.Name, agencyId, null);
    }

    public async Task<AuthResult> RefreshAsync(string refreshTokenValue, CancellationToken ct = default)
    {
        var stored = await db.Set<RefreshToken>().IgnoreQueryFilters()
            .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token");

        if (stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired or revoked");

        stored.IsRevoked = true;
        stored.RevokedAt = DateTime.UtcNow;

        var newRt = BuildRefreshToken(stored.User.Id);
        stored.ReplacedByToken = newRt.Token;
        db.Set<RefreshToken>().Add(newRt);
        await db.SaveChangesAsync(ct);

        var u = stored.User;
        var roleName = u.Role?.Name ?? "";
        var displayName = $"{u.FirstName} {u.LastName}".Trim();
        return new AuthResult(GenerateToken(u, roleName), newRt.Token, u.Id, u.Email,
            displayName, roleName, u.AgencyId, u.SubAccountId);
    }

    public async Task LogoutAsync(string refreshTokenValue, CancellationToken ct = default)
    {
        var token = await db.Set<RefreshToken>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue && !rt.IsRevoked, ct);

        if (token is null) return;

        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<MeData> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Set<User>().IgnoreQueryFilters()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
            ?? throw new KeyNotFoundException("User not found");

        var permissions = await rbac.GetPermissionsAsync(user.Id, user.TenantId, ct);
        return new MeData(user, user.Role, permissions);
    }

    private string GenerateToken(User user, string? roleName = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Auth:Secret"]!));
        var scopeValue = user.Scope switch
        {
            UserScope.Platform => "platform",
            UserScope.Agency   => "agency",
            _                  => "sub_account",
        };
        var claims = new List<Claim>
        {
            new("sub",       user.Id.ToString()),
            new("email",     user.Email),
            new("role",      roleName ?? user.Role?.Name ?? ""),
            new("tenant_id", user.TenantId.ToString()),
            new("scope",     scopeValue),
        };

        if (user.AgencyId.HasValue)
            claims.Add(new Claim("agency_id", user.AgencyId.Value.ToString()));

        if (user.SubAccountId.HasValue)
            claims.Add(new Claim("sub_account_id", user.SubAccountId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: config["Auth:Issuer"],
            audience: config["Auth:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static RefreshToken BuildRefreshToken(Guid userId) => new()
    {
        UserId = userId,
        Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        ExpiresAt = DateTime.UtcNow.AddDays(30),
    };
}

public record AuthResult(
    string Token,
    string RefreshToken,
    Guid   UserId,
    string Email,
    string Name,
    string Role,
    Guid?  AgencyId,
    Guid?  SubAccountId);

public record MeData(
    User              User,
    Role?             Role,
    IReadOnlyList<string> Permissions);

// ─── User Management ──────────────────────────────────────────────────────────

public class UserService(DbContext db, ICurrentUser caller, RbacService rbac)
{
    // Base query applying caller-scope tenant isolation
    private IQueryable<User> ScopedUsers() =>
        (caller.Scope switch
        {
            UserScope.Platform   => db.Set<User>().IgnoreQueryFilters(),
            UserScope.Agency     => db.Set<User>().IgnoreQueryFilters()
                                      .Where(u => u.AgencyId == caller.AgencyId),
            _                    => db.Set<User>().IgnoreQueryFilters()
                                      .Where(u => u.SubAccountId == caller.SubAccountId),
        })
        .Include(u => u.Role)
        .Include(u => u.Agency)
        .Include(u => u.SubAccount)
        .Where(u => !u.IsDeleted);

    public async Task<PagedData<UserResponse>> ListAsync(UserListQuery q, CancellationToken ct)
    {
        var query = ScopedUsers();

        if (q.Scope.HasValue)
            query = query.Where(u => u.Scope == q.Scope.Value);

        if (q.SubAccountId.HasValue && caller.Scope != UserScope.SubAccount)
            query = query.Where(u => u.SubAccountId == q.SubAccountId.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s)  ||
                u.Email.ToLower().Contains(s));
        }

        var total    = await query.CountAsync(ct);
        var pageNum  = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

        var users = await query
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedData<UserResponse>
        {
            Items      = users.Select(MapUser).ToList(),
            Page       = pageNum,
            PageSize   = pageSize,
            TotalCount = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
        };
    }

    public async Task<UserResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var user = await ScopedUsers().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new KeyNotFoundException("User not found");
        return MapUser(user);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (req.Scope == UserScope.Platform)
            throw new ArgumentException("Cannot create Platform-scoped users via this endpoint");

        // Determine target agency and sub-account from caller scope
        Guid targetAgencyId;
        Guid? targetSubAccountId;

        switch (caller.Scope)
        {
            case UserScope.Platform:
                targetAgencyId     = req.AgencyId ?? throw new ArgumentException("agencyId is required for platform callers");
                targetSubAccountId = req.Scope == UserScope.SubAccount
                    ? req.SubAccountId ?? throw new ArgumentException("subAccountId is required for SubAccount scope")
                    : null;
                break;

            case UserScope.Agency:
                targetAgencyId = caller.AgencyId!.Value;
                targetSubAccountId = req.Scope == UserScope.SubAccount
                    ? req.SubAccountId ?? throw new ArgumentException("subAccountId is required for SubAccount scope")
                    : null;
                break;

            default: // SubAccount
                if (req.Scope == UserScope.Agency)
                    throw new UnauthorizedAccessException("Sub-account callers cannot create agency-level users");
                targetAgencyId     = caller.AgencyId!.Value;
                targetSubAccountId = caller.SubAccountId!.Value;
                break;
        }

        if (targetSubAccountId.HasValue)
        {
            var subExists = await db.Set<SubAccount>()
                .AnyAsync(sa => sa.Id == targetSubAccountId.Value && sa.AgencyId == targetAgencyId, ct);
            if (!subExists)
                throw new ArgumentException("Sub-account not found or does not belong to your agency");

            // System roles are provisioned once during sub-account creation (SignupAsync / SubAccountService).
            // If they are missing here it means the sub-account was never properly provisioned.
            var rolesProvisioned = await db.Set<Role>().IgnoreQueryFilters()
                .AnyAsync(r => r.TenantId == targetSubAccountId.Value && r.IsSystem && !r.IsDeleted, ct);
            if (!rolesProvisioned)
                throw new InvalidOperationException(
                    $"Sub-account {targetSubAccountId.Value} has no system roles. Ensure the sub-account was created through the provisioning flow.");
        }

        var emailTaken = await db.Set<User>().IgnoreQueryFilters()
            .AnyAsync(u => u.Email == req.Email.Trim().ToLowerInvariant()
                        && u.AgencyId == targetAgencyId
                        && !u.IsDeleted, ct);
        if (emailTaken)
            throw new ArgumentException("A user with this email already exists in your agency");

        var role = await ResolveRoleAsync(req.RoleId, req.Scope, targetAgencyId, targetSubAccountId, ct);

        var tenantId = req.Scope == UserScope.SubAccount ? targetSubAccountId!.Value : targetAgencyId;
        var user = new User
        {
            TenantId      = tenantId,
            AgencyId      = targetAgencyId,
            SubAccountId  = targetSubAccountId,
            Scope         = req.Scope,
            FirstName     = req.FirstName.Trim(),
            LastName      = req.LastName.Trim(),
            Email         = req.Email.Trim().ToLowerInvariant(),
            Phone         = req.Phone?.Trim(),
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword(req.Password),
            RoleId        = role.Id,
            IsActive      = true,
            CreatedBy     = caller.UserId,
        };
        db.Set<User>().Add(user);
        await db.SaveChangesAsync(ct);

        user.Role = role;
        return MapUser(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct)
    {
        var user = await ScopedUsers().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new KeyNotFoundException("User not found");

        if (req.FirstName is not null) user.FirstName = req.FirstName.Trim();
        if (req.LastName  is not null) user.LastName  = req.LastName.Trim();
        if (req.Phone     is not null) user.Phone     = req.Phone.Trim();

        if (req.IsActive is false && user.IsActive)
        {
            // Revoke tokens on deactivation so active sessions are terminated
            var tokens = await db.Set<RefreshToken>()
                .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
                .ToListAsync(ct);
            foreach (var t in tokens) { t.IsRevoked = true; t.RevokedAt = DateTime.UtcNow; }
        }
        if (req.IsActive is not null) user.IsActive = req.IsActive.Value;

        if (req.RoleId is not null)
        {
            var role = await ResolveRoleAsync(req.RoleId.Value, user.Scope, user.AgencyId, user.SubAccountId, ct);
            user.RoleId = role.Id;
            user.Role   = role;
            await rbac.InvalidateUserCacheAsync(user.Id, user.TenantId, ct);
        }

        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = caller.UserId;
        await db.SaveChangesAsync(ct);
        return MapUser(user);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        if (id == caller.UserId)
            throw new InvalidOperationException("You cannot delete your own account");

        var user = await ScopedUsers().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new KeyNotFoundException("User not found");

        if (user.Role?.SystemRoleType == SystemRoleType.AgencyOwner)
        {
            var ownerCount = await db.Set<User>().IgnoreQueryFilters()
                .Include(u => u.Role)
                .CountAsync(u => u.AgencyId == user.AgencyId
                              && !u.IsDeleted
                              && u.Role!.SystemRoleType == SystemRoleType.AgencyOwner, ct);
            if (ownerCount <= 1)
                throw new InvalidOperationException("Cannot delete the last agency owner");
        }

        var tokens = await db.Set<RefreshToken>()
            .Where(rt => rt.UserId == id && !rt.IsRevoked)
            .ToListAsync(ct);
        foreach (var t in tokens) { t.IsRevoked = true; t.RevokedAt = DateTime.UtcNow; }

        user.IsDeleted  = true;
        user.DeletedAt  = DateTime.UtcNow;
        user.DeletedBy  = caller.UserId;
        await db.SaveChangesAsync(ct);

        await rbac.InvalidateUserCacheAsync(id, user.TenantId, ct);
    }

    // Validates that a role exists, belongs to the right tenant, and has the right scope.
    private async Task<Role> ResolveRoleAsync(
        Guid roleId, UserScope userScope, Guid? agencyId, Guid? subAccountId, CancellationToken ct)
    {
        var role = await db.Set<Role>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == roleId && !r.IsDeleted, ct)
            ?? throw new ArgumentException("Role not found");

        var expectedScope = userScope == UserScope.Agency ? RoleScope.Agency : RoleScope.SubAccount;
        if (role.Scope != expectedScope)
            throw new ArgumentException(
                $"A {userScope} user must have a {expectedScope}-scoped role, but role '{role.Name}' is {role.Scope}-scoped");

        var expectedTenantId = userScope == UserScope.SubAccount ? subAccountId!.Value : agencyId!.Value;
        if (caller.Scope != UserScope.Platform && role.TenantId != expectedTenantId)
            throw new ArgumentException("Role does not belong to the target tenant");

        return role;
    }

    private static UserResponse MapUser(User u) => new(
        u.Id,
        u.FirstName,
        u.LastName,
        u.Email,
        u.Phone,
        u.Scope.ToString(),
        u.IsActive,
        u.AgencyId,
        u.SubAccountId,
        u.Agency?.Name,
        u.SubAccount?.Name,
        u.Role is null ? null : new UserRoleDto(
            u.Role.Id,
            u.Role.Name,
            u.Role.Scope.ToString(),
            u.Role.SystemRoleType?.ToString()),
        u.CreatedAt,
        u.UpdatedAt);
}

// ─── Sub-Account Provisioner ─────────────────────────────────────────────────

public static class SubAccountProvisioner
{
    public static async Task EnsureRolesAsync(
        DbContext db, Guid subAccountId,
        IRolePermissionProvider permissionProvider, CancellationToken ct)
    {
        var hasRoles = await db.Set<Role>().IgnoreQueryFilters()
            .AnyAsync(r => r.TenantId == subAccountId && r.IsSystem && !r.IsDeleted, ct);
        if (hasRoles) return;

        var definitions = new[]
        {
            (SystemRoleType.AccountAdmin, "Account Admin", "Full access within this account"),
            (SystemRoleType.Manager,      "Manager",       "Operational access within this account"),
            (SystemRoleType.User,         "User",          "Standard day-to-day access"),
        };

        // Batch-resolve all permission IDs for the three sub-account role types in one query
        var allKeys = definitions
            .SelectMany(d => permissionProvider.GetPermissionKeys(d.Item1))
            .Distinct()
            .ToList();

        var permissionsByKey = await db.Set<Permission>()
            .Where(p => allKeys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, p => p.Id, ct);

        foreach (var (type, name, desc) in definitions)
        {
            var role = new Role
            {
                TenantId       = subAccountId,
                Name           = name,
                Description    = desc,
                IsSystem       = true,
                Scope          = RoleScope.SubAccount,
                SystemRoleType = type,
            };
            db.Set<Role>().Add(role);

            var keys = permissionProvider.GetPermissionKeys(type);
            db.Set<RolePermission>().AddRange(
                keys.Where(k => permissionsByKey.ContainsKey(k))
                    .Select(k => new RolePermission { RoleId = role.Id, PermissionId = permissionsByKey[k] }));
        }
        await db.SaveChangesAsync(ct);
    }
}

// ─── Roles & Permissions ──────────────────────────────────────────────────────

public class RolesService(DbContext db, ICurrentUser caller, RbacService rbac)
{
    public async Task<List<RoleDto>> ListAsync(CancellationToken ct)
    {
        IQueryable<Role> query = caller.Scope switch
        {
            UserScope.Platform => db.Set<Role>().IgnoreQueryFilters()
                                    .Where(r => r.TenantId == Guid.Empty),

            // Agency owners/admins need to assign both agency-scoped roles AND
            // sub-account-scoped roles (which live under subAccountId, not agencyId).
            UserScope.Agency => db.Set<Role>().IgnoreQueryFilters()
                                    .Where(r => r.TenantId == caller.AgencyId!.Value ||
                                                db.Set<SubAccount>().Any(sa =>
                                                    sa.Id == r.TenantId &&
                                                    sa.AgencyId == caller.AgencyId!.Value &&
                                                    sa.IsActive)),

            _ => db.Set<Role>().IgnoreQueryFilters()
                    .Where(r => r.TenantId == caller.SubAccountId!.Value),
        };

        var roles = await query
            .Where(r => !r.IsDeleted)
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return roles.Select(MapRole).ToList();
    }

    public async Task<List<PermissionDto>> ListAllPermissionsAsync(CancellationToken ct)
    {
        var permissions = await db.Set<Permission>()
            .OrderBy(p => p.Module).ThenBy(p => p.Action)
            .ToListAsync(ct);

        return permissions
            .Select(p => new PermissionDto(p.Id, p.Key, p.Description, p.Module, p.Action))
            .ToList();
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest req, CancellationToken ct)
    {
        var tenantId = ResolveTenantId(req.Scope, req.SubAccountId);

        var role = new Role
        {
            TenantId    = tenantId,
            Name        = req.Name.Trim(),
            Description = req.Description?.Trim() ?? "",
            IsSystem    = false,
            Scope       = req.Scope,
            CreatedBy   = caller.UserId,
        };
        db.Set<Role>().Add(role);

        if (req.PermissionKeys is { Count: > 0 })
        {
            var permIds = await db.Set<Permission>()
                .Where(p => req.PermissionKeys.Contains(p.Key))
                .Select(p => p.Id)
                .ToListAsync(ct);
            db.Set<RolePermission>().AddRange(
                permIds.Select(pid => new RolePermission { RoleId = role.Id, PermissionId = pid }));
        }

        await db.SaveChangesAsync(ct);

        var created = await db.Set<Role>().IgnoreQueryFilters()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == role.Id, ct);
        return MapRole(created);
    }

    public async Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest req, CancellationToken ct)
    {
        var role = await db.Set<Role>().IgnoreQueryFilters()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Role not found");

        ValidateCallerAccess(role);

        if (req.Name        is not null) role.Name        = req.Name.Trim();
        if (req.Description is not null) role.Description = req.Description.Trim();
        role.UpdatedAt = DateTime.UtcNow;
        role.UpdatedBy = caller.UserId;
        await db.SaveChangesAsync(ct);
        return MapRole(role);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await db.Set<Role>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Role not found");

        ValidateCallerAccess(role);

        if (role.IsSystem)
            throw new InvalidOperationException("System roles cannot be deleted");

        var userCount = await db.Set<User>().IgnoreQueryFilters()
            .CountAsync(u => u.RoleId == id && !u.IsDeleted, ct);
        if (userCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete role: {userCount} user(s) are currently assigned to it");

        role.IsDeleted = true;
        role.DeletedAt = DateTime.UtcNow;
        role.DeletedBy = caller.UserId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<RoleDto> SetPermissionsAsync(Guid id, IReadOnlyList<string> permissionKeys, CancellationToken ct)
    {
        var role = await db.Set<Role>().IgnoreQueryFilters()
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Role not found");

        ValidateCallerAccess(role);

        db.Set<RolePermission>().RemoveRange(role.RolePermissions);

        var permIds = await db.Set<Permission>()
            .Where(p => permissionKeys.Contains(p.Key))
            .Select(p => p.Id)
            .ToListAsync(ct);
        db.Set<RolePermission>().AddRange(
            permIds.Select(pid => new RolePermission { RoleId = role.Id, PermissionId = pid }));

        await db.SaveChangesAsync(ct);

        // Invalidate cache for every user assigned this role
        var affected = await db.Set<User>().IgnoreQueryFilters()
            .Where(u => u.RoleId == id && !u.IsDeleted)
            .Select(u => new { u.Id, u.TenantId })
            .ToListAsync(ct);
        foreach (var u in affected)
            await rbac.InvalidateUserCacheAsync(u.Id, u.TenantId, ct);

        var updated = await db.Set<Role>().IgnoreQueryFilters()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == id, ct);
        return MapRole(updated);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid ResolveTenantId(RoleScope scope, Guid? subAccountId) =>
        (caller.Scope, scope) switch
        {
            (UserScope.Platform, RoleScope.SubAccount) =>
                subAccountId ?? throw new ArgumentException("subAccountId is required for SubAccount-scoped roles"),
            (UserScope.Platform, _) =>
                caller.AgencyId ?? Guid.Empty,
            (UserScope.Agency, RoleScope.Agency) =>
                caller.AgencyId!.Value,
            (UserScope.Agency, RoleScope.SubAccount) =>
                subAccountId ?? throw new ArgumentException("subAccountId is required for SubAccount-scoped roles"),
            (UserScope.SubAccount, RoleScope.SubAccount) =>
                caller.SubAccountId!.Value,
            _ => throw new ArgumentException(
                $"A {caller.Scope}-scoped caller cannot create {scope}-scoped roles"),
        };

    private void ValidateCallerAccess(Role role)
    {
        if (caller.Scope == UserScope.Platform) return;
        var expectedTenant = caller.Scope == UserScope.Agency
            ? caller.AgencyId : caller.SubAccountId;
        if (role.TenantId != expectedTenant)
            throw new UnauthorizedAccessException("Access denied to this role");
    }

    private static RoleDto MapRole(Role r) => new(
        r.Id, r.Name, r.Description, r.Scope.ToString(),
        r.SystemRoleType?.ToString(), r.IsSystem,
        r.RolePermissions.Select(rp => rp.Permission.Key).OrderBy(k => k).ToList(),
        r.TenantId);
}

public class RbacService(IDistributedCache cache, DbContext db, ILogger<RbacService> logger)
{
    private static readonly DistributedCacheEntryOptions CacheTtl =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, Guid tenantId, CancellationToken ct = default)
    {
        var cacheKey = $"rbac:{tenantId}:{userId}:{permission}";
        try
        {
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached is not null) return cached == "1";
        }
        catch (Exception ex) { logger.LogWarning(ex, "Redis unavailable — HasPermissionAsync falling back to DB"); }

        var has = await db.Set<RolePermission>()
            .AnyAsync(rp =>
                rp.Role.TenantId == tenantId &&
                rp.Role.Users.Any(u => u.Id == userId) &&
                rp.Permission.Key == permission, ct);

        try { await cache.SetStringAsync(cacheKey, has ? "1" : "0", CacheTtl, ct); }
        catch { /* Redis down — result still correct, just not cached */ }

        return has;
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var cacheKey = $"rbac:{tenantId}:{userId}:all";
        try
        {
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<List<string>>(cached)!;
        }
        catch (Exception ex) { logger.LogWarning(ex, "Redis unavailable — GetPermissionsAsync falling back to DB"); }

        var permissions = await db.Set<RolePermission>()
            .Where(rp => rp.Role.TenantId == tenantId && rp.Role.Users.Any(u => u.Id == userId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);

        try { await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(permissions), CacheTtl, ct); }
        catch { /* Redis down — result still correct, just not cached */ }

        return permissions;
    }

    public async Task InvalidateUserCacheAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        try { await cache.RemoveAsync($"rbac:{tenantId}:{userId}:all", ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Redis unavailable — cache invalidation skipped for user {UserId}", userId); }
    }
}
