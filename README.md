# Melarium App 🐝

Multi-tenant beekeeping management SaaS: organizations manage apiaries → beehives →
inspections, feeding programs (diets), todos, and expenses. Includes QR-code hive scanning,
voice-note inspections (Whisper + Llama via Groq), 7-day weather per apiary (Open-Meteo),
statistics, calendar, and in-app + email notifications. UI language: Bosnian.

**Stack:** .NET 10 (Clean Architecture, EF Core + PostgreSQL) · React 18 + TypeScript + Vite +
TanStack Query v5 + Tailwind (PWA)

---

## Repository Layout

```
backend/
  Melarium.API/              ← controllers, middleware, JWT auth, Program.cs
  Melarium.Application/      ← services, DTOs, validators, IAccessGuard (feature slices)
  Melarium.Domain/           ← entities + enums (no dependencies)
  Melarium.Entity/           ← EF Core DbContext, repositories, UnitOfWork, migrations
  Melarium.Infrastructure/   ← email (MailKit) + background email worker
  Melarium.Application.Tests/← xunit unit tests (authorization, diets, auth)
frontend/                   ← React SPA/PWA
docs/                       ← architecture, decisions (ADRs), API contracts, guidelines
```

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- PostgreSQL 15+ running locally (default connection expects `localhost:5432`,
  database `MelariumDB`, user `postgres` — see `backend/Melarium.API/appsettings.Development.json`)

## Running Locally

**Backend** (http://localhost:62648, Swagger at `/swagger` — Development only):

```bash
cd backend/Melarium.API
dotnet run
```

The database is migrated and demo users are seeded automatically on startup (Development only).
Demo logins: `sysadmin@beehive.com / SysAdmin123!`, `orgadmin@goldenhive.com / OrgAdmin123!`,
`admin@goldenhive.com / Admin123!`, `user1@goldenhive.com / User123!`.

**Frontend** (http://localhost:5173, proxies `/api` to the local backend):

```bash
cd frontend
npm install
npm run dev
```

**Tests:**

```bash
cd backend
dotnet test Melarium.Application.Tests/Melarium.Application.Tests.csproj
```

## Configuration & Secrets

`appsettings.json` is committed **without secrets** (empty placeholders). The app fails fast at
startup if a required value is missing.

| Key | Purpose | Local dev | Production (env var) |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL | `appsettings.Development.json` | `ConnectionStrings__DefaultConnection` |
| `Jwt:Secret` | HS256 signing key (≥ 32 chars) | dev-only value in `appsettings.Development.json` | `Jwt__Secret` |
| `Smtp:Password` | Resend API key for notification email (username is the literal `resend`) | user-secrets (optional — email is skipped if unset) | `Smtp__Password` |
| `Smtp:FromEmail` | Sender address — must be on a domain verified in Resend | default | `Smtp__FromEmail` |
| `Groq:ApiKey` | Voice-note transcription/parsing | user-secrets (optional) | `Groq__ApiKey` |
| `Bootstrap:SysAdminEmail` / `Bootstrap:SysAdminPassword` | Provisions the production SystemAdmin | not needed | `Bootstrap__SysAdminEmail`, `Bootstrap__SysAdminPassword` |
| `AllowedOrigins` | CORS (comma-separated) | defaults include `localhost:5173` | `AllowedOrigins` |
| `FrontendUrl` | Base URL embedded in hive QR codes | default | `FrontendUrl` |

Local secrets via user-secrets:

```bash
cd backend/Melarium.API
dotnet user-secrets set "Groq:ApiKey" "<key>"
dotnet user-secrets set "Smtp:Password" "<resend-api-key>"
```

**Production note:** demo accounts are locked (random password + revoked refresh tokens) on every
production startup; the real SystemAdmin comes exclusively from the `Bootstrap__*` env vars.

## Deployment

- **Backend:** Render — set the env vars above; TLS terminates at Render's proxy; liveness probe at `/health`.
- **Frontend:** Vercel — set `VITE_API_URL` to the deployed API base URL (e.g. `https://<app>.onrender.com/api`).

### Self-hosted (VPS, e.g. netcup)

Single-domain setup: nginx serves the built frontend and reverse-proxies `/api` to the
backend, so the frontend can call the relative `/api` path (no `VITE_API_URL` needed, no
cross-origin requests).

- `docker-compose.yml` (repo root) — PostgreSQL + backend API as containers.
- `.env.example` (repo root) — copy to `.env` on the server and fill in secrets.
- `deploy/nginx.melarium.conf.example` — nginx site config (static frontend + `/api` proxy).
- `deploy/deploy.sh` — pulls latest code, rebuilds the backend container and the frontend,
  redeploys both.

Full step-by-step walkthrough (DNS, server hardening, Docker install, TLS via certbot,
backups): [docs/deployment.md](docs/deployment.md).

## Documentation

Start with [docs/claude.md](docs/claude.md) (project rules), then
[docs/architecture.md](docs/architecture.md), [docs/decisions.md](docs/decisions.md) (ADRs),
[docs/context.md](docs/context.md) (implemented-feature inventory),
[docs/api-contracts.md](docs/api-contracts.md), and [docs/deployment.md](docs/deployment.md)
(self-hosted VPS deployment).
