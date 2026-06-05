# PhantomPulse — Architecture & Developer Guide

## Request Flow

```
HTTP Request
   │
   ├─ GlobalExceptionMiddleware       maps exceptions → HTTP status codes
   ├─ TenantMiddleware                reads scope + tenant_id from JWT → TenantContext
   └─ PermissionEnforcementMiddleware reads [RequirePermission] attr → Redis RBAC check
         │
         ▼
   Controller                         validates input, calls service, maps response
         │
         ▼
   Service                            business logic, EF Core queries, enforces scoping
         │
         ▼
   DbContext (AppDbContext)           snake_case naming, global query filter on TenantId
         │
         ▼
   ApiResponse<T>  /  PagedData<T>   consistent envelope → HTTP 200
```

---

## Multi-Tenancy

Three scopes control every query and permission check:

| Scope | TenantId | Who |
|---|---|---|
| `Platform` | `Guid.Empty` (bypasses filter) | PhantomPulse super-admins |
| `Agency` | AgencyId | Resellers / white-label owners |
| `SubAccount` | SubAccountId | End-client CRM users |

`TenantMiddleware` sets `TenantContext` from the JWT `scope` + `tenant_id` claims.
`AppDbContext` applies `WHERE tenant_id = @tenantId` globally via EF query filters.
Services that need cross-scope visibility (e.g. `UserService` listing agency users) call
`.IgnoreQueryFilters()` and filter manually.

---

## Auth & Permissions

- JWT claims: `sub`, `email`, `role`, `scope`, `tenant_id`, `agency_id?`, `sub_account_id?`
- **Permissions are NOT stored in the JWT.** They are loaded per-request via `RbacService`
  which caches the full permission list in Redis for 5 min (`rbac:{tenantId}:{userId}:all`).
- `[RequirePermission("module.action")]` attribute triggers the middleware check.
- Platform-scope users and the `PlatformAdmin` role bypass all permission checks.

---

## Module Layout

```
src/
├── PhantomPulse.Api/             Entry point, middleware, Swagger, DI wiring
├── PhantomPulse.Infrastructure/  DbContext, migrations, Redis, Hangfire, SignalR
├── PhantomPulse.SharedKernel/    BaseEntity, ICurrentUser, TenantContext, enums
└── Modules/
    ├── Foundation/               Auth, Users, Roles, RBAC, permissions catalog
    ├── Crm/                      Contacts, Deals, pipelines
    ├── Messaging/                Conversations, WhatsApp, email, SMS
    ├── Automation/               Workflows, triggers, Hangfire jobs
    └── Campaigns/                Bulk messaging, campaign scheduling
```

Each module follows: `Controllers → Services → Entities | DTOs`

---

## Key Conventions

- **Responses** always wrapped in `ApiResponse<T>` (or `PagedData<T>` for lists).
- **Soft delete** — never `DELETE` rows; set `IsDeleted = true` on `BaseEntity`.
- **EF snake_case** — all columns/tables use snake_case via `EFCore.NamingConventions`.
- **Roles are tenant-scoped.** Agency roles: `TenantId = AgencyId`. SubAccount roles: `TenantId = SubAccountId`.
- **RolePermissionMatrix** defines the default permission set for each `SystemRoleType`.
- **SubAccountProvisioner** auto-creates `AccountAdmin`, `Manager`, `User` roles for any new sub-account.

---

## Local Dev Credentials

> These are dev-only values from `appsettings.json`. Never commit production secrets.

| What | Value |
|---|---|
| PostgreSQL | `Host=localhost;Database=phantompulse;Username=postgres;Password=sql@123` |
| Redis | `localhost:6379` |
| JWT Secret | `CPDlhafW4EmnedAViXIjY6nxxMrJWtskL289hH62Gas=` |
| JWT Issuer / Audience | `phantompulse` / `phantompulse-api` |

### Seeded Accounts

| Role | Email | Password | Scope |
|---|---|---|---|
| Platform Admin | `admin@phantompulse.io` | `Admin@123!` | Platform — bypasses all tenant/permission checks |
| Agency Owner | `owner@demo.com` | `Owner@123!` | Agency — full access to Demo Agency + Demo Client |

Demo Agency ID: `bbbbbbbb-0001-0000-0000-000000000000`
Demo SubAccount ID: `bbbbbbbb-0002-0000-0000-000000000000`

### Running Locally

```bash
# 1. Start dependencies
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=sql@123 postgres:16
docker run -d -p 6379:6379 redis:7

# 2. Apply migrations
dotnet ef database update --project src/PhantomPulse.Infrastructure

# 3. Run (seeds data automatically on startup)
dotnet run --project src/PhantomPulse.Api

# Swagger UI
http://localhost:5000/swagger
```

---

## Exception → HTTP Status Map

| Exception | Status |
|---|---|
| `KeyNotFoundException` | 404 Not Found |
| `UnauthorizedAccessException` | 401 Unauthorized |
| `ArgumentException` | 400 Bad Request |
| `InvalidOperationException` | 409 Conflict |
| Anything else | 500 Internal Server Error |
