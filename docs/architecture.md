# Architecture

## System Overview

```
Browser (React PWA)
    │  HTTPS / JSON
    ▼
Melarium.API          ← Controllers, Middleware, JWT auth, ICurrentUser
    │
Melarium.Application  ← Services, DTOs, Validators, AutoMapper, interfaces, IAccessGuard
    │
Melarium.Domain       ← Entities, Enums (pure — no dependencies)
    ▲
    ├── Melarium.Entity         ← EF Core DbContext, Repositories, UoW, Migrations (persistence)
    └── Melarium.Infrastructure ← External services (email)
    │
PostgreSQL
```

## Dependency Rules

```
API → Application → Domain              (allowed)
Entity → Application, Domain             (allowed — implements Application interfaces)
Infrastructure → Application             (allowed — implements Application interfaces)
API → Entity, Infrastructure             (via DI only, never direct)
Domain → anything else                   (FORBIDDEN)
Application → Entity / Infrastructure     (FORBIDDEN — depends only on its own interfaces)
```

## Backend Layers

### Melarium.Domain
- `BaseEntity`: `Id (int)`, `CreatedAt`, `UpdatedAt`
- All entities inherit `BaseEntity`
- Contains enums, no logic beyond simple property rules
- **No dependencies on other layers**

### Melarium.Entity (persistence)
- `MelariumDbContext` — EF Core (PostgreSQL/Npgsql), auto-stamps `UpdatedAt` on `SaveChangesAsync`
- `Repository<T>` — generic base with CRUD + predicate search; one concrete repo per aggregate (one file each)
- One `IEntityTypeConfiguration<T>` per entity in `Configurations/`
- `UnitOfWork` — single `SaveChangesAsync`, lazy-initialized repos
- Owns EF Core Migrations; database auto-migrates and seeds on startup
- Registered via `AddEntity(configuration)`

### Melarium.Infrastructure (external services)
- `EmailService` (MailKit/SMTP) and other outbound integrations
- `ChannelEmailQueue` (`IEmailQueue`) + `EmailNotificationWorker` (`BackgroundService`) —
  notification emails are delivered off the request path (see ADR-021)
- Registered via `AddInfrastructure()`

### Melarium.Application
- One folder per domain feature: `Apiaries/`, `Beehives/`, `Inspections/`, `Diets/`, `Todos/`, `Auth/`, `Admin/`, `Weather/`, …
- One type per file: `IXxxService.cs`, `XxxService.cs`, `DTOs/<OneDtoPerFile>.cs`, `Validators/<OneValidatorPerFile>.cs`, `XxxMappingProfile.cs`
- `Common/Interfaces/` holds repository + `IUnitOfWork` + `ICurrentUser` interfaces (interfaces live here, **not** in Domain)
- `Common/Security/` holds `Roles` and `IAccessGuard`/`AccessGuard` — the single source of truth for tenant/resource authorization
- `Common/Exceptions/` — `NotFoundException`, `BusinessRuleException`, `ValidationException`, `ForbiddenAccessException` (one per file)
- Services receive `IUnitOfWork`, `ICurrentUser`, and `IAccessGuard` via constructor injection

### Melarium.API
- Controllers: thin — validate input → call service → return HTTP result (no authorization logic; that lives in the service layer via `IAccessGuard`)
- `CurrentUser` implements `ICurrentUser` from JWT claims via `IHttpContextAccessor`
- `GlobalExceptionMiddleware` — maps exceptions to Problem Details (RFC 7807); `ForbiddenAccessException` → 403
- JWT Bearer auth — all endpoints require `[Authorize]` except `POST /api/auth/{login,register,refresh,logout}` and the public QR scan lookup
- Rate limiting (fixed window per IP): login/register 5/min, refresh 20/min, parse-voice 10/min
- Startup: config guards (fail fast on missing `Jwt:Secret` / connection string), auto-migrate,
  then dev-only demo seed **or** production demo-account lock + `Bootstrap:*` SystemAdmin provisioning
- CORS: configured via `AllowedOrigins`

## Frontend Architecture

```
src/
├── core/
│   ├── context/     ← AuthContext (user, login, logout)
│   ├── models/      ← TypeScript interfaces mirroring backend DTOs
│   └── services/    ← Axios calls + React Query hooks
├── features/        ← One folder per domain (pages live here)
└── shared/
    └── components/  ← Layout, ProtectedRoute, AdminRoute, reusable UI
```

### Data Flow (Frontend)

```
Component
  → React Query hook (queries.ts)
    → Service function (xxxService.ts)
      → apiClient (axios + interceptors)
        → Backend API
```

### Auth Flow

1. `LoginPage`/`RegisterPage` call `authService` → store access token (30 min) + refresh token (14 d) + user in localStorage
2. `AuthContext` reads localStorage on mount, exposes `user` and `isAuthenticated`
3. `apiClient` request interceptor attaches `Bearer <token>` to every request
4. On 401: single-flight `POST /auth/refresh` (rotates the refresh token) → replay the original request;
   if the refresh fails → logout + redirect to `/login`
5. `ProtectedRoute` blocks unauthenticated users; `RoleRoute`/`AdminRoute` gate by role

## Domain Hierarchy

```
Organization
  └── Users (many)
  └── Apiaries (many)
        └── Beehives (many)
              ├── Inspections (many)
              ├── Diets (many)
              │     └── FeedingEntries (many)
              └── Todos (many, optional — can also belong to Apiary)
```

## Key Configuration

| Setting | Location | Value |
|---|---|---|
| DB connection | env var / `appsettings.Development.json` | PostgreSQL (Npgsql); **empty in committed appsettings.json** |
| Access token | `Jwt:AccessTokenMinutes` | 30 min |
| Refresh token | `Jwt:RefreshTokenDays` | 14 days, rotating, stored hashed |
| JWT claims | `AuthService` | `sub`, `email`, `role`, `jti`, `organizationId?`, `apiaryId?` |
| Secrets | env vars / user-secrets | `Jwt__Secret`, `Smtp__Password`, `Groq__ApiKey`, `Bootstrap__*` — never committed |
| Dev API proxy | `vite.config.ts` | `/api` → `http://localhost:62648` (local backend) |
| PWA theme | `vite.config.ts` | Amber `#d97706` |
