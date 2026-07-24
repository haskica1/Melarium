# Ignore — Do Not Touch

> These files and areas are stable and must not be modified unless explicitly directed.
> Accidental changes here can break migrations, auth, or data integrity.

---

## Database Migrations

**Path:** `backend/Melarium.Entity/Migrations/`

**Rule:** Never edit, rename, or delete existing migration files.

**Why:** EF Core migration history is sequential. Editing past migrations corrupts the schema version chain and breaks `dotnet ef database update` for all environments.

**Allowed:** Adding new migration files only (`dotnet ef migrations add <Name>` from `backend/Melarium.Entity`).

---

## MelariumDbContext — Existing Entity Configurations

**Path:** `backend/Melarium.Entity/MelariumDbContext.cs` + `backend/Melarium.Entity/Configurations/`

**Rule:** Do not change existing entity configurations, column types, or table names.

**Why:** Any change requires a migration. Accidental renames cause data loss on `UPDATE`.

**Allowed:** Adding new `DbSet<T>` properties and new `IEntityTypeConfiguration<T>` files for new tables.

---

## GlobalExceptionMiddleware

**Path:** `backend/Melarium.API/Middleware/GlobalExceptionMiddleware.cs`

**Rule:** Do not add new exception types or change existing HTTP status mappings without a deliberate decision. Do not re-expose exception details in production responses.

**Why:** The exception-to-status mapping is the contract the frontend relies on. Detailed 500 bodies leak internals — they are Development-only by design.

**Allowed:** Adding a mapping for a genuinely new exception type; update `decisions.md` when doing so.

---

## Mapping Profiles — Existing Maps

**Path:** `backend/Melarium.Application/Features/<Feature>/<Feature>MappingProfile.cs` (one per feature)

**Rule:** Do not modify existing `CreateMap` entries. Only append new ones.

**Why:** Changing an existing map can silently break all endpoints that use it, with no compile-time error.

**Allowed:** Adding new `CreateMap` lines for new entities or DTOs.

---

## AuthService — JWT Claim Structure

**Path:** `backend/Melarium.Application/Features/Auth/AuthService.cs`

**Rule:** Do not rename or remove existing JWT claims (`sub`, `email`, `role`, `jti`, `organizationId`, `apiaryId`).

**Why:** `CurrentUser` (API layer) and the frontend both read these claims. Renaming one breaks both without a compile error.

**Allowed:** Adding new claims if a feature needs them; document in `api-contracts.md`.

---

## Refresh-Token Rotation Logic

**Path:** `backend/Melarium.Application/Features/Auth/AuthService.cs` (`RefreshAsync`, `IssueTokensAsync`)

**Rule:** Do not weaken rotation: tokens are stored hashed, a presented token is always revoked on refresh, and reuse of a rotated token revokes the user's whole active set.

**Why:** This is the theft-detection mechanism. `Melarium.Application.Tests/AuthServiceTests.cs` locks the contract — tests failing here means a security regression.

---

## apiClient.ts — Interceptor Logic

**Path:** `frontend/src/core/services/apiClient.ts`

**Rule:** Do not change the 401 handling (single-flight refresh → replay → logout on failure) or the token attachment pattern.

**Why:** This is the auth backbone of the frontend. Changing it breaks session management for every request in the app.

**Allowed:** Adding new interceptors for new cross-cutting concerns (e.g., request ID header). Do not remove existing ones.

---

## core/models/index.ts — Existing Interfaces

**Path:** `frontend/src/core/models/index.ts`

**Rule:** Do not rename or remove existing interface properties. TypeScript may not catch all usages.

**Why:** Interface properties are used across many components and services. Renaming silently breaks string-keyed accesses and form field bindings.

**Allowed:** Adding new properties to existing interfaces, adding new interfaces.

---

## DatabaseInitializer (Seed & Production Lock)

**Path:** `backend/Melarium.Entity/Seed/DatabaseInitializer.cs`

**Rule:** Do not re-enable demo seeding in production, and do not remove `LockDemoAccountsAsync` / `EnsureBootstrapAdminAsync` from the production startup path in `Program.cs`.

**Why:** Demo credentials are public (committed to this repository). Production locks them on every startup and provisions the real SystemAdmin from `Bootstrap:*` env vars.

**Allowed:** Adding new demo data for Development; document in `context.md`.

---

## Committed Configuration — No Secrets

**Path:** `backend/Melarium.API/appsettings.json` (+ `appsettings.Development.json`)

**Rule:** Never commit real secrets (JWT secret, SMTP password, API keys, production connection strings). Placeholders stay empty; real values come from env vars or user-secrets.

**Why:** The repository is public. A committed secret is a compromised secret (this happened once — everything was rotated).

---

## Summary Table

| Area | Path | Rule |
|---|---|---|
| Migrations | `Melarium.Entity/Migrations/` | Never edit existing files |
| DbContext configurations | `Melarium.Entity/MelariumDbContext.cs`, `Configurations/` | Never change existing entity configs |
| Exception middleware | `Melarium.API/Middleware/GlobalExceptionMiddleware.cs` | Never change mappings; details stay dev-only |
| AutoMapper | `Features/<F>/<F>MappingProfile.cs` | Never modify existing maps |
| JWT claims | `Features/Auth/AuthService.cs` | Never rename or remove claims |
| Refresh rotation | `Features/Auth/AuthService.cs` | Never weaken rotation/reuse detection |
| Axios interceptors | `frontend/src/core/services/apiClient.ts` | Never change auth/401 logic |
| Frontend models | `frontend/src/core/models/index.ts` | Never rename/remove interface properties |
| Seed & lock | `Melarium.Entity/Seed/DatabaseInitializer.cs` | Demo seed dev-only; keep production lock |
| Secrets | `appsettings*.json` | Never commit real secrets |
