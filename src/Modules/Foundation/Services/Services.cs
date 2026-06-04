using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PhantomPulse.Foundation.Entities;
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
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        var permissions = user.Role?.RolePermissions.Select(rp => rp.Permission.Key).ToList() ?? [];

        var rt = BuildRefreshToken(user.Id, user.TenantId);
        db.Set<RefreshToken>().Add(rt);
        await db.SaveChangesAsync(ct);

        var name = $"{user.FirstName} {user.LastName}".Trim();
        return new AuthResult(GenerateToken(user, permissions), rt.Token, user.Id, user.Email, name, user.Role?.Name ?? "", permissions);
    }

    public async Task<AuthResult> SignupAsync(string name, string email, string password, CancellationToken ct = default)
    {
        var exists = await db.Set<User>().IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct);
        if (exists) throw new ArgumentException("An account with this email already exists.");

        var agencyId = Guid.NewGuid();
        var subAccountId = Guid.NewGuid();
        var parts = name.Trim().Split(' ', 2);
        var emailPrefix = email.Split('@')[0].ToLowerInvariant().Replace(".", "-");

        // 1. Create the Agency
        var agency = new Agency
        {
            Id = agencyId,
            Name = $"{parts[0]}'s Agency",
            Slug = $"{emailPrefix}-agency-{agencyId.ToString("N")[..6]}",
            IsActive = true,
            CustomDomain = null,
        };
        db.Set<Agency>().Add(agency);

        // 2. Create a default SubAccount under the agency
        var subAccount = new SubAccount
        {
            Id = subAccountId,
            AgencyId = agencyId,
            Name = $"{parts[0]}'s Account",
            Slug = $"{emailPrefix}-{subAccountId.ToString("N")[..6]}",
            IsActive = true,
        };
        db.Set<SubAccount>().Add(subAccount);

        // 3. Create the Agency Owner role (agency-scoped, TenantId = AgencyId)
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

        // 4. Create the owning user (agency-scoped, TenantId = AgencyId)
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

        // 5. Grant all permissions to the Agency Owner role
        var allPermissions = await db.Set<Permission>().ToListAsync(ct);
        db.Set<RolePermission>().AddRange(allPermissions.Select(p => new RolePermission
        {
            RoleId = ownerRole.Id,
            PermissionId = p.Id,
        }));
        await db.SaveChangesAsync(ct);

        var permKeys = allPermissions.Select(p => p.Key).ToList();
        var rt = BuildRefreshToken(user.Id, agencyId);
        db.Set<RefreshToken>().Add(rt);
        await db.SaveChangesAsync(ct);

        return new AuthResult(GenerateToken(user, permKeys, ownerRole.Name), rt.Token, user.Id, user.Email, name.Trim(), ownerRole.Name, permKeys);
    }

    public async Task<AuthResult> RefreshAsync(string refreshTokenValue, CancellationToken ct = default)
    {
        var stored = await db.Set<RefreshToken>().IgnoreQueryFilters()
            .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token");

        if (stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired or revoked");

        stored.IsRevoked = true;
        stored.RevokedAt = DateTime.UtcNow;

        var newRt = BuildRefreshToken(stored.User.Id, stored.User.TenantId);
        stored.ReplacedByToken = newRt.Token;
        db.Set<RefreshToken>().Add(newRt);
        await db.SaveChangesAsync(ct);

        var u = stored.User;
        var permissions = u.Role?.RolePermissions.Select(rp => rp.Permission.Key).ToList() ?? [];
        var roleName = u.Role?.Name ?? "";
        var displayName = $"{u.FirstName} {u.LastName}".Trim();
        return new AuthResult(GenerateToken(u, permissions, roleName), newRt.Token, u.Id, u.Email, displayName, roleName, permissions);
    }

    private string GenerateToken(User user, IEnumerable<string> permissions, string? roleName = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Auth:Secret"]!));
        var claims = new List<Claim>
        {
            new("sub",       user.Id.ToString()),
            new("email",     user.Email),
            new("role",      roleName ?? user.Role?.Name ?? ""),
            new("tenant_id", user.TenantId.ToString()),
        };
        claims.AddRange(permissions.Select(p => new Claim("permissions", p)));

        var token = new JwtSecurityToken(
            issuer: config["Auth:Issuer"],
            audience: config["Auth:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static RefreshToken BuildRefreshToken(Guid userId, Guid tenantId) => new()
    {
        TenantId = tenantId,
        UserId = userId,
        Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        ExpiresAt = DateTime.UtcNow.AddDays(30),
    };
}

public record AuthResult(
    string Token,
    string RefreshToken,
    Guid UserId,
    string Email,
    string Name,
    string Role,
    IReadOnlyList<string> Permissions);

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
