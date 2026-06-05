using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
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

public class AuthService(DbContext db, IConfiguration config)
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

        var permIds = RolePermissionMatrix.GetPermissionIds(SystemRoleType.AgencyOwner);
        db.Set<RolePermission>().AddRange(permIds.Select(id => new RolePermission
        {
            RoleId = ownerRole.Id,
            PermissionId = id,
        }));

        var rt = BuildRefreshToken(user.Id);
        db.Set<RefreshToken>().Add(rt);
        await db.SaveChangesAsync(ct);

        await SubAccountProvisioner.EnsureRolesAsync(db, subAccountId, ct);

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
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
            ?? throw new KeyNotFoundException("User not found");

        var permissions = user.Role?.RolePermissions
            .Select(rp => rp.Permission.Key)
            .OrderBy(k => k)
            .ToList() ?? [];

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

            await SubAccountProvisioner.EnsureRolesAsync(db, targetSubAccountId.Value, ct);
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

internal static class SubAccountProvisioner
{
    internal static async Task EnsureRolesAsync(DbContext db, Guid subAccountId, CancellationToken ct)
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
            db.Set<RolePermission>().AddRange(
                RolePermissionMatrix.GetPermissionIds(type)
                    .Select(pid => new RolePermission { RoleId = role.Id, PermissionId = pid }));
        }
        await db.SaveChangesAsync(ct);
    }
}

public class RbacService(IDistributedCache cache, DbContext db)
{
    public async Task<bool> HasPermissionAsync(Guid userId, string permission, Guid tenantId, CancellationToken ct = default)
    {
        var cacheKey = $"rbac:{tenantId}:{userId}:{permission}";
        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null) return cached == "1";

        var has = await db.Set<RolePermission>()
            .AnyAsync(rp =>
                rp.Role.TenantId == tenantId &&
                rp.Role.Users.Any(u => u.Id == userId) &&
                rp.Permission.Key == permission, ct);

        await cache.SetStringAsync(cacheKey, has ? "1" : "0",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);
        return has;
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var cacheKey = $"rbac:{tenantId}:{userId}:all";
        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<List<string>>(cached)!;

        var permissions = await db.Set<RolePermission>()
            .Where(rp => rp.Role.TenantId == tenantId && rp.Role.Users.Any(u => u.Id == userId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);

        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(permissions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);
        return permissions;
    }

    public async Task InvalidateUserCacheAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => await cache.RemoveAsync($"rbac:{tenantId}:{userId}:all", ct);
}
