# Architecture Decision Records

> This file is append-only. Never remove or edit past decisions.
> Format: Decision → Why → Alternatives considered.

---

## ADR-001: Clean Architecture for Backend

**Decision:** Four-layer Clean Architecture (API → Application → Domain → Infrastructure).

**Why:** Enforces strict separation of concerns. Business logic is isolated from EF Core and HTTP. Each layer is independently testable. Scales well as new features are added.

**Alternatives considered:**
- MVC monolith — simpler but mixes concerns and becomes unmaintainable quickly.
- CQRS / MediatR — considered but added unnecessary ceremony for this project scale.

---

## ADR-002: Repository + Unit of Work Pattern

**Decision:** Generic `Repository<T>` base with domain-specific extensions, coordinated by `IUnitOfWork`.

**Why:** Decouples services from EF Core. Simplifies testing by allowing mock repos. `UnitOfWork` ensures a single `SaveChangesAsync()` call per operation, preventing partial saves.

**Alternatives considered:**
- Direct DbContext in services — faster to write but leaks EF Core into Application layer.
- Dapper — considered for read performance, but EF Core is sufficient at current scale.

---

## ADR-003: JWT Authentication (Stateless)

**Decision:** HS256 JWT tokens, 8-hour expiry, stored in localStorage on the client.

**Why:** Stateless auth fits a REST API — no session store needed. Simple to implement and scale.

**Alternatives considered:**
- Cookie-based sessions — requires server-side session store, adds complexity.
- Refresh token rotation — future consideration if security requirements tighten.

---

## ADR-004: FluentValidation for Input Validation

**Decision:** All DTO validation via `AbstractValidator<T>` classes, registered with `AddFluentValidationAutoValidation()`.

**Why:** Keeps validation logic out of controllers. Declarative rules are easy to read and test. Auto-validation integrates with ModelState, so no manual validator calls needed.

**Alternatives considered:**
- DataAnnotations — less expressive, harder to unit test, limited for complex rules.

---

## ADR-005: AutoMapper for Entity ↔ DTO Conversion

**Decision:** All mapping defined in `MappingProfile.cs` using AutoMapper 13.

**Why:** Eliminates repetitive manual mapping code. Centralized in one place. Works naturally with the service layer pattern.

**Alternatives considered:**
- Manual mapping methods — more explicit but verbose; chosen as fallback only for complex projections.
- Mapster — similar capability, AutoMapper already established in project.

---

## ADR-006: React Query (TanStack Query) for Server State

**Decision:** All data fetching and caching via TanStack Query v5. No Redux or Zustand.

**Why:** Server state (API data) is fundamentally different from UI state. React Query handles caching, stale-time, invalidation, and loading/error states out of the box. Eliminates boilerplate `useEffect` + `useState` patterns.

**Alternatives considered:**
- Redux Toolkit Query — overkill for this app's complexity.
- SWR — similar to React Query but less feature-rich.
- Manual fetch + useState — rejected; error-prone and verbose.

---

## ADR-007: React Hook Form for Forms

**Decision:** All forms use React Hook Form. No controlled `useState` per field.

**Why:** Uncontrolled inputs with ref-based state improve performance. Built-in validation integration. Less re-rendering than fully controlled forms.

**Alternatives considered:**
- Formik — similar but heavier bundle, more verbose.
- Plain controlled components — fine for tiny forms, doesn't scale.

---

## ADR-008: Feature-Based Folder Structure (Frontend)

**Decision:** `features/<domain>/` for pages, `core/services/` for API logic, `shared/components/` for reusable UI.

**Why:** Co-locates everything related to a feature. Easy to find, add, or delete a feature without touching unrelated files.

**Alternatives considered:**
- Type-based structure (`pages/`, `components/`, `hooks/`) — becomes hard to navigate as features grow.

---

## ADR-009: Open-Meteo for Weather (No API Key)

**Decision:** Weather forecasts fetched from `api.open-meteo.com` using apiary coordinates.

**Why:** Free, no API key required, 7-day forecast at sufficient precision for beekeeping decisions.

**Alternatives considered:**
- OpenWeatherMap — requires API key, costs money at scale.
- WeatherAPI — similar; adds key management overhead.

---

## ADR-010: PWA with Vite PWA Plugin

**Decision:** App is a Progressive Web App using `vite-plugin-pwa` with Workbox NetworkFirst caching.

**Why:** Beekeepers work in fields with unreliable connectivity. PWA enables offline-capable UI and installability on mobile devices without an app store.

**Alternatives considered:**
- Native mobile apps — too costly to build and maintain.
- Simple SPA without PWA — no offline support.

---

## ADR-011: GlobalExceptionMiddleware + Problem Details

**Decision:** Single middleware catches all exceptions and maps to RFC 7807 Problem Details JSON.

**Why:** Consistent error format across all endpoints. Controllers stay clean — they never need try/catch. Frontend can rely on a predictable error shape.

**Alternatives considered:**
- Per-controller try/catch — repetitive, easy to forget in new controllers.
- Custom error response format — Problem Details is an HTTP standard, better for interoperability.

---

## ADR-012: SystemAdmin Role for Platform Management

**Decision:** Two roles: `Admin` (org-level) and `SystemAdmin` (platform-level). SystemAdmin manages organizations and users via `/api/admin`.

**Why:** Multi-tenant design requires a super-admin to provision organizations. Separates operational concerns from business logic.

**Alternatives considered:**
- Single role — no separation of platform vs. org management.
- Fine-grained permissions — over-engineered for current user base.

---

## ADR-013: Dedicated `Melarium.Entity` Persistence Project

**Decision:** Move all data access (DbContext, entity configurations, repositories, UnitOfWork, migrations) into a dedicated `Melarium.Entity` project. `Melarium.Infrastructure` is slimmed to external services (email).

**Why:** Makes the persistence boundary explicit and single-purpose. Migrations and EF Core concerns live in one clearly-named project. Infrastructure no longer mixes data access with outbound integrations.

**Notes:** Migration IDs were preserved during the move, so `__EFMigrationsHistory` still matches — no database change. EF CLI now targets `--project Melarium.Entity --startup-project Melarium.API`.

**Alternatives considered:**
- Keep everything in `Infrastructure` — the clean-architecture default, but conflates persistence with other infrastructure.

---

## ADR-014: Centralized Authorization (`ICurrentUser` + `IAccessGuard`)

**Decision:** All tenant/resource ownership checks live in the service layer via a single `IAccessGuard`, fed by an `ICurrentUser` abstraction over JWT claims. Controllers keep only coarse `[Authorize(Roles = …)]` gating. `ForbiddenAccessException` maps to 403.

**Why:** The previous design scattered authorization across controllers with duplicated, inconsistent checks — which had allowed cross-tenant access (an OrgAdmin could read/modify another organization's data) and IDOR on by-id reads. A single source of truth fixes the holes and prevents regressions.

**Alternatives considered:**
- Per-controller checks — the original approach; error-prone and duplicated.
- ASP.NET resource-based authorization handlers — heavier; the service-layer guard is simpler and reuses loaded entities.

---

## ADR-015: Role Rename for Clarity

**Decision:** Rename roles `Admin → ApiaryAdmin`, `OrgAdmin → OrganizationAdmin`, `User → Beekeeper` (`SystemAdmin` unchanged). Numeric enum values are preserved (1–4).

**Why:** The old names were misleading — "Admin" was actually the *narrowest* (apiary-level) role. The new names state the scope plainly.

**Notes:** Breaking change to the JWT role claim string (users must re-log in); no database migration needed since persisted ints are unchanged. Frontend role checks updated in lockstep.

---

## ADR-016: One Type Per File

**Decision:** Every public type (enum, interface, exception, service, DTO, validator, EF configuration, repository) lives in its own file. Validators are co-located per feature under `Features/<F>/Validators/`.

**Why:** Faster navigation and clearer single-responsibility. Supersedes the earlier convention of grouping a feature's DTOs/validators into one file.

**Alternatives considered:**
- Grouped-per-feature files — fewer files, but harder to locate a specific type as features grow.

## ADR-017: Refresh-Token Rotation (supersedes the auth part of ADR-003)

**Decision:** Access token shortened to 30 minutes; a rotating refresh token (14 days) is issued alongside it. Refresh tokens are stored **hashed** (SHA-256); every refresh revokes the presented token and links its replacement; presenting an already-rotated token revokes the user's entire active set (theft detection). Client keeps tokens in localStorage and refreshes via a single-flight 401 interceptor.

**Why:** ADR-003's 8-hour token was a large theft window with no revocation story. Rotation bounds the damage of a leaked access token to 30 minutes and makes refresh-token theft self-defeating.

**Notes:** localStorage (vs. httpOnly cookie) is a known XSS trade-off, accepted for now because rotation + reuse detection bound the damage; revisit if requirements tighten. Contract locked by `AuthServiceTests`.

---

## ADR-018: Explicit Validation Calls in Controllers (supersedes ADR-004's auto-validation)

**Decision:** Controllers call `await _validator.ValidateAsync(dto)` explicitly and return `BadRequest(validation.ToDictionary())`. FluentValidation's ASP.NET auto-validation is deliberately **not** enabled.

**Why:** Preserves the exact `errors`-dictionary response shape the frontend forms rely on, and follows FluentValidation's own guidance (auto-validation is deprecated by its authors). The per-action boilerplate (~6 lines) is the accepted cost; a shared action filter is a possible future refinement, not a requirement.

---

## ADR-019: Per-Feature Mapping Profiles + Manual Mapping for Computed DTOs (supersedes ADR-005's single MappingProfile)

**Decision:** Each feature owns a `<Feature>MappingProfile`. DTOs whose fields are computed (Diets progress counts, Admin aggregates) are mapped **manually** in the service instead of forcing AutoMapper.

**Why:** One shared `MappingProfile.cs` had become a merge hotspot and hid feature coupling. Manual mapping where projections are computed keeps the intent visible; AutoMapper remains the default for plain property copies.

---

## ADR-020: No Secrets in the Repository; Dev-Only Demo Seed + Production Bootstrap Admin

**Decision:** `appsettings.json` carries empty placeholders only. Real values come from environment variables in production (`Jwt__Secret`, `Smtp__Password`, `Groq__ApiKey`, `ConnectionStrings__DefaultConnection`, `Bootstrap__SysAdminEmail`, `Bootstrap__SysAdminPassword`) and from `appsettings.Development.json`/user-secrets locally. `Program.cs` fails fast when required values are missing. Demo users (public passwords) are seeded **only in Development**; every production startup locks the demo accounts (random password hash + refresh-token revocation) and provisions the real SystemAdmin from the `Bootstrap:*` values.

**Why:** The repository is public. A committed JWT secret means anyone can forge tokens; committed demo SystemAdmin credentials meant anyone could log into production. Both actually happened and were rotated on 2026-07-02.

---

## ADR-021: Notification Email via In-Memory Queue + Background Worker

**Decision:** `NotificationService` persists the in-app notification, then enqueues `(userId, title, message)` onto an unbounded `System.Threading.Channels` queue. `EmailNotificationWorker` (a `BackgroundService` in Infrastructure) dequeues, resolves the recipient in its own DI scope, and sends via MailKit. Email is skipped unless `Smtp:Host` and `Smtp:Password` are configured.

**Why:** Synchronous SMTP inside the request pipeline caused request timeouts, so delivery had been disabled entirely. Queue + worker restores email without adding a message broker; losing queued mail on process shutdown is acceptable because the in-app notification is already persisted (email is best-effort).

**Alternatives considered:**
- Hangfire / Quartz — persistent retries, but a heavy dependency for best-effort mail.
- `Task.Run` fire-and-forget — no backpressure, swallows failures, unscoped DbContext hazards.

---

## ADR-022: Lean List Payloads — Counts in SQL, QR Codes On Demand

**Decision:** List endpoints never carry derived heavy data: inspection/beehive counts are computed in the database (`GROUP BY` / `Select(x => x.Collection.Count)`) instead of `Include`-ing full child rows, and the Base64 QR PNG lives only on the beehive **detail** DTO plus a dedicated `GET /api/beehives/by-apiary/{id}/qr-codes` endpoint used by label export.

**Why:** Apiary/beehive lists previously loaded every inspection row of the organization and a ~KB QR blob per hive on every request — the heaviest queries in the app, on the most-visited pages, only to display counts.

---

## ADR-023: Bosnian as the Single UI Language (`BsLabels`)

**Decision:** All user-facing strings the API produces — `*Name` enum label fields, calendar labels, and notification titles/messages — are Bosnian, sourced from `Common/Localization/BsLabels` on the backend and the matching label maps in `core/models/index.ts` on the frontend. Logs, code, docs, and Swagger stay English.

**Why:** The UI is Bosnian; mixed English fragments (enum `.ToString()`, English notifications) looked broken. `BsLabels` already existed for Stats — it is now the single source instead of per-service formatting.

**Alternatives considered:**
- Full i18n framework — over-engineered while the product is single-language; revisit if a second language is needed.

---

## ADR-024: Extracted AI Client Seam for the Advisor (SPEC-01)

**Decision:** Transcription is extracted from `VoiceParsingService` into a shared
`ITranscriptionService` / `GroqTranscriptionService` (Whisper large-v3), and advisor chat goes through a
thin `IAdvisorAiClient` / `GroqAdvisorAiClient` wrapper over Groq chat completions. `VoiceParsingService`
now consumes `ITranscriptionService` (no behavior change). On the frontend, `useVoiceInput` moved from
`features/inspections/` to `core/hooks/` since it is now shared; the upload endpoint stays in each
caller's service (inspections → `parse-voice`, advisor → `/advisor/transcribe`).

**Why:** The advisor reuses the exact Groq transcription the inspection flow already had, and hiding the
chat call behind an interface makes `AdvisorService` unit-testable (Groq mocked) — the previous structure
had transcription and the model call welded inside `VoiceParsingService`. No new AI provider or secret;
reuses `Groq:ApiKey`.

**Alternatives considered:**
- Duplicate the transcription code in the advisor — divergent prompts/behavior over time.
- Call Groq directly from `AdvisorService` — untestable without hitting the network.

**Addendum, 2026-08-09 (SPEC-18):** the advisor itself is retired, merged into the assistant — but
`IAdvisorAiClient`/`GroqAdvisorAiClient` were not deleted, only renamed to `IProseAiClient`/
`GroqProseAiClient` and re-scoped to "free-form Groq prose replies" generally, because
`LearningTopicService` (SPEC-06) also depends on them to draft a topic's Bosnian summary — a real,
pre-existing consumer this ADR's "for the Advisor" framing did not anticipate. The transcription half of
this decision is unaffected; `ITranscriptionService` is still shared by voice inspections and the
assistant exactly as described above.

---

## ADR-025: `react-markdown` for Learning Article Rendering (SPEC-06)

**Decision:** Learning-topic bodies are authored as markdown and rendered with **`react-markdown`**
(the spec-flagged new dependency, approved with SPEC-06 implementation). No raw-HTML plugins
(`rehype-raw` etc.) are added — react-markdown's default escaping renders `<script>` and any embedded
HTML as inert text, which is the XSS guard for admin-authored content. Styling goes through a shared
`MarkdownArticle` component (`features/learning/`) with Tailwind-styled element mappings, reused by the
reader page and the admin preview. To keep the PWA precache working (workbox 2 MiB per-file limit),
`react-markdown` and `recharts` are split into their own vendor chunks via `manualChunks` in
`vite.config.ts`.

**Why:** Markdown is the right authoring format for admin-written articles (headings, lists, tables),
and hand-rolling a renderer is exactly the kind of parsing/XSS surface a maintained library eliminates.

**Alternatives considered:**
- `marked`/`markdown-it` + `dangerouslySetInnerHTML` — requires a separate sanitizer (DOMPurify) and
  careless use is an XSS foot-gun; react-markdown renders to React elements, never raw HTML.
- Tailwind `@tailwindcss/typography` plugin for styling — another dependency for what 15 element
  mappings in one component already do.

---

## ADR-026: In-App Outbox Instead of Service-Worker Background Sync (SPEC-07)

**Decision:** Offline inspection capture uses an **IndexedDB outbox flushed by the app itself**
(`core/offline/`): the form writes an `OutboxItem` on network-level failure, and a sync engine
(mount + `'online'` event + manual button) replays items through the normal
`inspectionService.create`. Cross-tab races are prevented with the Web Locks API. The outbox is
keyed by the owner's **email** (the client session has no numeric user id and the spec keeps the
backend unchanged). `localStorage` was rejected (multi-tab safety, size limits).

**Why:** replaying POSTs from a service worker (`workbox-background-sync`) bypasses the axios
auth/refresh interceptor (a queued request with an expired token would just fail), gives no UI
feedback (no toasts, no failed-item list, no edit path), and is much harder to reason about than
an in-app flush that reuses the exact same code path as an online submit.

**Alternatives considered:**
- `workbox-background-sync` — see above; also Background Sync API is Chromium-only.
- Server idempotency keys against double-submit — real fix, but backend changes are out of scope
  for v1; the crash window between `201` and item removal is documented and accepted.
- `idb` npm package — the hand-rolled wrapper is ~100 lines; not worth a dependency.

---

## ADR-027: Object Storage Behind `IFileStorage`, Photos Served Through the API (SPEC-05)

**Decision:** Inspection photos live in **blob storage behind an `IFileStorage` abstraction**
(`LocalDiskFileStorage` for dev, `S3FileStorage` for prod — any S3-compatible provider, recommended
**Cloudflare R2**; switch is config-only via `Storage:Provider`). Image bytes are **streamed through
the API** (`GET /inspections/photos/{id}/file`, auth-checked, `Cache-Control: private, max-age=86400`);
the bucket stays private and no presigned-URL machinery exists. The real content type is detected
from **file header bytes** (JPEG/PNG/WebP), never trusted from the client. New package: `AWSSDK.S3`
(Infrastructure only).

**Why:** QR codes as Base64 in Postgres are fine at 2 KB but wrong for MB photos (DB/backup/DTO
bloat), and Render's disk is ephemeral so prod needs object storage. API-streaming keeps
authorization in one place (`IAccessGuard`, same rights as the parent inspection), works identically
for Local and S3, and avoids leaking bucket URLs.

**Alternatives considered:**
- Presigned URLs — faster egress path but splits authorization into a second expiring mechanism and
  differs between Local and S3; unnecessary at current scale.
- Base64 in Postgres (QR precedent) — rejected, see above.
- Azure Blob / Google Cloud Storage SDKs — S3-compatible API covers R2/MinIO/AWS with one SDK.

**Known limit (Phase 2):** Groq caps base64-image requests at 4 MB, so photos over ~3 MB raw are
rejected for AI analysis with a Bosnian 422 (upload cap stays 8 MB). Server-side downscaling was out
of SPEC-05 scope — revisit if analysis rejections become common.

---

## ADR-028: Manual Annual Billing First, 402 for Plan Limits, Computed Effective Plan (SPEC-09)

**Decision:** Subscription plans are enforced by a single `IPlanGuard` (mirrors `IAccessGuard`) that
throws `PlanLimitException` → **HTTP 402** with a top-level `code: "plan-limit"` — deliberately
distinct from 403 so the frontend renders an upgrade prompt instead of "access denied". The
**effective plan is computed, never stored** (`PlanHelper.Effective`): an expired paid/Partner plan
behaves as Free, with no background job. **v1 billing is manual and annual** — SystemAdmin activates
a plan (`PUT /admin/organizations/{id}/plan`) after a bank transfer; the whole payment seam is just
`Organization.Plan` + `PlanValidUntil`. New organizations get a 30-day Pro trial implemented as a
pre-set expiring Pro (no extra machinery). A hidden **Partner** plan (= Max in enforcement) is
SystemAdmin-only and never shown in public UI/checkout.

**Why:**
- **Manual + annual, not monthly:** Stripe doesn't support BiH-based merchants, and manually
  reconciling monthly bank transfers for 20 KM is operationally unworkable. Annual collection with a
  "2 mjeseca gratis" discount keeps bookkeeping sane until Paddle (Phase 2) automates it.
- **402 over 403:** the two failure modes are semantically different — "you can't do this" vs. "your
  plan doesn't include this". A distinct status lets the axios interceptor globally raise the upsell
  modal without inspecting messages.
- **Computed effective plan:** the Diets/Treatments precedent — deriving state avoids a background
  job and a whole class of "we forgot to flip the flag" bugs. Enforcement is **create-only**, so a
  downgrade never locks or deletes existing data (legal artifacts like the treatment PDF register
  stay available).
- **Config-driven limits:** `Plans:{PlanType}:{Key}` with absent = unlimited means tuning a tier (or
  the advisor quota) is a config change, not a deploy.

**Alternatives considered:**
- Stored effective plan flipped by a nightly job — rejected (drift, extra moving part).
- 403 for plan limits — rejected (frontend couldn't cleanly distinguish upsell from authz denial).
- Immediate Stripe/monthly billing — impossible for BiH now; Paddle deferred to Phase 2 with the
  two-field seam already in place.
- Hardcoded limits — rejected; config keeps pricing/limits editable without a rebuild.

---

## ADR-029: Emailed Single-Use Tokens for Password Reset & Email Verification; Revoke Sessions on Trust Change

**Status:** Accepted

**Context:** The app shipped to production with no password recovery at all — a user who forgot
their password was permanently locked out, recoverable only by a SystemAdmin editing the account by
hand. Separately, a password change left every existing refresh token valid for its full 14 days, so
"I changed my password" did not actually evict an attacker.

**Decision:**
- One `UserToken` table with a `UserTokenPurpose` discriminator (`PasswordReset`,
  `EmailVerification`) rather than a table per flow. The lifecycle is identical — issue → email →
  redeem once → expire — and `(TokenHash, Purpose)` is the unique lookup, so a verification link can
  never be redeemed as a password reset.
- Only the SHA-256 hash is stored, mirroring `RefreshToken` (ADR-017). A leaked database row is not
  a usable link.
- Issuing a new token invalidates the user's outstanding ones of that purpose, so an older email
  cannot be replayed.
- `POST /auth/forgot-password` always returns 204. Any observable difference between a registered
  and unregistered address turns the endpoint into an account-enumeration oracle.
- `ISessionRevoker` centralises "end this user's sessions" and is called from password reset,
  profile password change, and any change to role / organisation / apiary — the three privileges
  carried in the JWT. Beehive assignments are resolved per request from the database, so they need
  no revocation.
- Failed login returns **401** with one message and one cost for both branches (a dummy BCrypt
  verify runs when the email is unknown, so timing does not leak existence).
- **Email verification is soft**: recorded, surfaced, never enforced at login. Its migration
  backfills every pre-existing account as verified.

**Why:**
- **Soft verification, grandfathered:** the app was already live. Enforcing verification — or
  leaving existing rows null — would have locked out the entire user base on deploy. The flag can be
  tightened later once adoption is real; the reverse is not recoverable.
- **Revoke-all on password change over revoke-others:** keeping the current session alive needs the
  caller's refresh token, which the profile endpoint does not receive. Signing everyone out is the
  safe default; the client handles it by logging out and prompting a fresh sign-in.
- **Reset also marks the address verified:** receiving the reset mail already proves control of the
  mailbox — asking for a second confirmation would be theatre.
- **Reuse the background email queue (ADR-021):** SMTP stays off the request path, so a slow or
  broken mail server cannot fail a password-reset request.

**Alternatives considered:**
- Separate `PasswordResetToken` / `EmailVerificationToken` tables — rejected as near-duplicate
  schema, config and repository code for one differing field.
- Storing tokens in plaintext for re-display — rejected; the "secret address" model only suits the
  calendar feed (ADR-011 scope), not a credential that can change a password.
- 404/400 on unknown address in forgot-password — rejected (account enumeration).
- Blocking login for unverified accounts — deferred; see the grandfathering rationale above.

---

## ADR-030: Feedback Splits Its Two Delivery Channels; `QueuedEmail` Gains an Explicit Recipient (SPEC-13)

**Status:** Accepted (2026-07-30)

**Context:** User feedback has to reach the operator. The existing notification path
(`NotificationService.NotifyAsync`) welds the two channels together: it persists an in-app
notification *and* enqueues an e-mail for the same user id. `QueuedEmail` carried only a `UserId`, and
`EmailNotificationWorker` always resolved the address by loading that user — so there was no way to mail
an address that isn't an account, and no way to notify N admins in-app while sending one e-mail.

**Decision:**
- **In-app to every SystemAdmin, e-mail to one configured address.** New feedback broadcasts via the
  existing e-mail-free `NotifyManyInAppAsync` (recipients from a new
  `IUserRepository.GetSystemAdminIdsAsync()`), and separately enqueues **one** message to
  `Feedback:NotifyEmail`. A reply to the submitter still uses `NotifyAsync` — there the coupling is
  exactly what is wanted.
- **`QueuedEmail` gains a second addressing mode:** `UserId` becomes `int?` and optional
  `ToEmail`/`ToName` are added, with `ForUser(...)` / `ForAddress(...)` factories so the intent is
  visible at the call site. The worker prefers an explicit address, else resolves by `UserId`, else logs
  and skips. Delivery stays on the background queue.
- **Feedback is not tenant-scoped**, so it does **not** go through `IAccessGuard`: authorization is the
  flat SystemAdmin-vs-owner split the other `api/admin/*` controllers already use. Reading another
  user's row returns **404, not 403**.
- Notification failure never fails the submission — the row is saved first and both channels are wrapped.

**Why:**
- **One address over one-per-admin:** adding a second SystemAdmin should not multiply the operator's
  inbox, and the destination is an operations address that need not correspond to an account.
- **Extending `QueuedEmail` over calling `IEmailService` directly:** a direct call would put SMTP back
  on the request path, which is precisely what ADR-021 removed. Three existing call sites were moved to
  `ForUser` with no behaviour change.
- **404 over 403 on someone else's feedback:** a 403 confirms the row exists, turning by-id reads into an
  existence oracle. Same reasoning as the forgot-password 204 in ADR-029.
- **Silent skip when unconfigured:** mirrors `EmailService`'s existing behaviour for missing SMTP, so a
  missing env var degrades to "no e-mail", never a failed submission. It is logged.

**Alternatives considered:**
- `NotifyAsync` per SystemAdmin — rejected: an e-mail per admin, which is the option explicitly not wanted.
- A separate `IOperatorMailer` service — rejected: a second delivery mechanism to keep correct, for what
  is one optional field on the existing queue item.
- Screenshot as part of the create payload — rejected: a mixed multipart+JSON endpoint for what the
  inspection-photo flow already solves as a second request. The report is saved even if the upload fails.

---

## ADR-031: In-App Help Is a Static Code Registry, Not Content in the Database (SPEC-14)

**Status:** Accepted (2026-07-30)

**Context:** New users had no explanation of what any page does. The app already has a DB-backed CMS for
admin-authored content — Learning Topics (SPEC-06) — so the obvious move was another table plus an admin
editor.

**Decision:** Per-page help content lives in a **typed registry in the frontend**
(`core/help/helpContent.ts`), lazily imported as its own chunk. No entity, no endpoint, no migration.
Edukacija remains the DB-backed CMS for long-form beekeeping knowledge, and the help panel links to it by
**category, never by topic id**. The icon is rendered **once** from `Layout` and resolved by route via
`matchPath`; a route with no entry renders no icon. Onboarding progress ("Prvi koraci") is **derived from
existing data**, never stored.

**Why:**
- **Content that describes a UI is documentation of code.** When a page changes, its help text must change
  in the same commit or it starts lying. A database copy has no mechanism to notice the UI moved — and the
  failure mode is silent and user-facing.
- **It works offline.** This is a PWA used in fields with no signal; DB-backed help would need a request.
- **One icon, not thirty.** Around thirty routes would each need an edit, and any page whose author forgot
  would silently have no help — the exact inconsistency the feature exists to remove.
- **Derived onboarding state** follows ADR-028's computed-effective-plan reasoning: it avoids the "we
  forgot to flip the flag" class of bug, and it is correct in cases a flag gets wrong (a user added to an
  organisation that already has apiaries is not told to create their first one). It also means the card
  disappears on its own, with no dismissal state to store.
- **Editing without a deploy is the one real loss**, and it is small here: the person who writes the help
  text is the person who runs the deploy.

**Alternatives considered:**
- Table + SystemAdmin editor — rejected above; the cost is entity + migration + repository + service +
  two controllers + admin CRUD UI, to buy an ability that mostly matters to someone who deploys anyway.
- Interactive product tours with element highlighting — rejected: a real dependency, and every DOM change
  can break a tour anchor. The panel plus the derived checklist covers the need at a fraction of the cost.
- Per-page icons added to each page component — rejected; see "one icon, not thirty".

**Known limit (Phase C):** the "seen"/"don't auto-open" flags live in `localStorage` keyed by e-mail, so
they are per browser — the same user gets the first-run experience again on their phone. Moving them to
the account is one small table and one endpoint, deliberately deferred until it proves to matter.

---

## ADR-032: Referral Rewards Go Through One Guarded Credit Path and Never Touch `PlanNotes` (SPEC-15)

**Status:** Accepted (2026-08-06) — Phase 1 implemented

**Context:** "Pozovi prijatelja" pays an existing customer in plan days when someone they invited joins
and verifies their e-mail. Before this, `Organization.PlanValidUntil` had exactly two writers and both
assigned an **absolute** value chosen by a human: `AdminService` (SystemAdmin activating a plan) and
`AuthService` (the registration trial). The reward is the first writer that does **arithmetic on an
existing value**, and the first plan change no person approves.

**Decision:**

1. All granting goes through `IPlanCredit.GrantDaysAsync`, which lives beside `PlanGuard` rather than in
   the invitation feature. `PlanGuard` refuses actions and depends on who is asking; `PlanCredit` gives
   and must behave identically no matter what triggered it. The algorithm is a **pure static method** so
   its invariants are unit-testable without a database (`InvitationRewardTests`).
2. The reward **never writes to `PlanNotes`**. The itemised record lives on the `Invitation` rows.
3. Attribution and reward run **after** the caller's own work is committed — attribution after
   `IssueTokensAsync`, the reward after the verification `SaveChangesAsync`.
4. An unknown, expired or malformed referral code **never fails a registration**.

**Why:**

- **The plan is only ever raised, never lowered.** The upgrade test is `effective == Free`, not an ordinal
  comparison, because `PlanType` runs Free=1 … Partner=5 — so `Plan = Pro` on a Partner organization would
  be a downgrade from 5 to 3. A `Plan < Pro` test would have shipped that bug looking correct.
- **A lifetime plan is never given an expiry date.** `PlanValidUntil == null` means "bez isteka" for
  Partner and early-adopter organizations; writing `today + 30` there converts an unlimited plan into one
  that expires in a month. It is the single most destructive thing this feature could do, so the null
  check comes first and grants nothing.
- **`PlanNotes` is load-bearing UI, not a comment field.** `PlansPage` detects the registration trial with
  `planNotes === 'Probni period'` — an exact string match. A tidy audit line appended there would silently
  remove the trial notice from the plans page for exactly the users most likely to be inviting people.
  This was found by reading the frontend, not by a failing test, which is why the prohibition is an ADR
  and a test rather than a comment.
- **`try/catch` does not isolate an EF failure.** The invitation code shares the request's `IUnitOfWork`.
  If its `SaveChangesAsync` throws, the entities it touched stay tracked and `Modified`; the next
  `SaveChangesAsync` in the same method re-attempts them and throws again *outside* the `try`. Running the
  grant before the verification commit would therefore have left the user unverified **and unable to fix
  it by retrying**, because the grant would fail identically every time. Ordering is the mechanism;
  the `try/catch` is only the second line of defence.
- **Losing attribution is cheaper than losing a sign-up.** A referral code arrives from a link pasted into
  a group chat months ago. Rejecting a registration because of it would trade a customer for a statistic.

**Alternatives considered:**
- Granting inside `InvitationService` — rejected: plan semantics would then live in two places, and the
  next feature that grants days would copy the arithmetic rather than the invariants.
- Writing the running total into `PlanNotes` for at-a-glance admin visibility — rejected above. If that
  number is wanted, it is a computed column over the invitation rows.
- Rewarding at registration instead of verification — rejected: it makes fake accounts free to farm. At
  verification, a farmer needs a real, receiving mailbox per fake organization.
- Scaling the reward or the caps by plan — rejected: it couples growth to billing and punishes exactly the
  behaviour being paid for.

**Bounded by configuration, not by code:** `Invitations:Reward:*` — 30 days per accepted invitation, **180
lifetime per organization**, at most 5 per rolling 30 days, and one reward per invited organization ever.
The lifetime cap is the only thing bounding what the feature can cost against fraud; the length of the
referral code is not a control and must never be treated as one.

---

## ADR-033: The AI Proposes DTOs; the Existing Services Execute Them (SPEC-17)

**Status:** Accepted (2026-08-08, Phase A; extended 2026-08-09, Phases B and C)

**Context.** SPEC-17 is the first feature in which an AI writes to the database. The obvious
implementation — let the assistant build entities and save them — would have been shorter and is the
one a future refactor is most likely to drift back toward.

**Decision.** `AiActionExecutor` builds the **same DTOs the forms post** (`CreateInspectionDto`,
`CreateTodoDto`) and calls the **existing** application services. It never touches a repository, and
`AiAssistantService` never creates an inspection or a todo itself.

**Why this and not the shorter path:**

- **Four behaviours ride on those services and none of them would fail loudly if skipped.**
  `IAccessGuard` on the target, `IPlanGuard` limits, the automatic temperature from the apiary's
  weather (`InspectionService.CreateAsync`), and the todo notification cascade to superiors and the
  assignee (`TodoService`). A repository write compiles fine and silently loses all four.
- **Validation lives in the controllers here, not the services.** So the executor resolves
  `IValidator<T>` from DI and runs it itself. Without that one step, data authored by an AI would pass
  *fewer* checks than data typed by a human — the exact inversion of what anyone would intend.
- **The proposal is not a capability grant.** The confirmation card is editable, so the confirm request
  can carry any hive id. It is re-resolved against `IAccessGuard.GetAccessibleBeehivesAsync()` and
  re-validated. Rule 1 is what makes this true by default rather than by a check somebody remembers.

**The invariant that outranks the rest:** name resolution searches **only** the caller's accessible
apiaries and hives. An out-of-scope hive is not "forbidden", it is invisible — so the next action kind
added in Phase C cannot forget an authorization check that was never a separate step.

**Consequences accepted, not overlooked:**

- **Partial failure is reported, not rolled back.** Each service calls `SaveChangesAsync` itself, so a
  failure on action 3 cannot cleanly undo 1 and 2 — the same shared-`DbContext` trap ADR-032 documents.
  Pre-flight makes doomed actions non-confirmable; execution then records a status per action and the
  response names exactly what landed. Never a fake "success".
- **Confirmation claims the rows before executing.** `Pending → Confirmed` is saved first, so a phone
  double-tap finds nothing pending and is refused. A row left `Confirmed` with a null `ResultEntityId`
  therefore means "claimed but never completed" — recoverable information, and strictly better than a
  silently duplicated inspection.
- **`ResultEntityId` is not a foreign key.** Deleting the created inspection must not delete the audit
  row saying the assistant created it.

**Two details that look cosmetic and are not:**

- **"Danas" comes from `AppTimeZone`, never `DateTime.UtcNow`.** Between local midnight and 01:00/02:00
  the two disagree by a day. `VoiceParsingService` has this bug today; SPEC-17 does not inherit it, and
  the executor additionally clamps a local midnight that is still future by UTC so
  `CreateInspectionValidator`'s "no future dates" rule cannot reject a legitimately-today inspection.
- **Apiary-name normalization maps `đ` to `dj`, not to `d`.** That is the transliteration Whisper and
  phone keyboards produce, so "Đurđevak" and "Djurdjevak" resolve to one apiary. Mapping to a bare `d`
  makes exactly that common pair fail to match — caught by a test, not by review.

**Alternatives considered:** native Groq tool-calling instead of one JSON envelope — rejected for now:
the envelope keeps the parser pure and unit-testable and matches the proven `VoiceParsingService`
pattern. Merging the assistant into the advisor — rejected: a router guessing "question or command" is
a new failure mode, and a chat bubble is a poor host for an editable form.

---

### Addendum, 2026-08-09 — Phase C: update and delete extend the same rule, not a new one

**Context.** Phase C added five action kinds that touch an **existing** record instead of proposing a
new one — `UpdateTodo`, `CompleteTodo`, `UpdateInspection`, `DeleteTodo`, `DeleteInspection`. The
question was whether this needed new machinery or whether the Phase A design already generalized.

**Decision.** It generalized. `AiActionExecutor` still builds the DTOs the edit forms already post
(`UpdateTodoDto`, `UpdateInspectionDto` — both pre-existing, unmodified) and calls the **existing**
`UpdateAsync`/`DeleteAsync` on the same two services. No new repository code, no new validators. The
one addition is a resolver *stage*, not a new rule: `AiTargetResolver.ResolveExistingTodo`/
`ResolveExistingInspection` find the record (title for a todo, date within a hive for an inspection —
absent date defaults to the most recent one), using the same pure, `IAccessGuard`-scoped-set pattern
apiary/hive resolution already established. Ambiguity produces candidates in the exact shape
`AssistantClarificationBuilder` (Phase B) already renders as buttons — one more branch, not a parallel
mechanism.

**One new invariant Phase C introduces:** an existing-record target is **fixed at propose time** and is
never re-picked from the confirm request — there is no dropdown for "a different todo" the way a create
action's card lets you swap the apiary/hive. `BuildConfirmedPayloadAsync` short-circuits to `stored with
{ Fields = fields }` for these kinds, ignoring any `apiaryId`/`beehiveId` the client sends. This does not
weaken §5.3's untrusted-input rule: the executor still re-fetches the record and the underlying
service still re-runs its own access check before touching it, so a record that changed or became
inaccessible between propose and confirm fails exactly as it would from the normal edit form.

**The one place D5 was narrowed on purpose.** D5 (SPEC-17 §1) says an update or delete needs a second,
separate confirmation. `CompleteTodo` is its own action kind, deliberately **not** counted as
destructive (`IsDestructive` checks four kinds, not five): checking a todo off is a one-tap, instantly
reversible toggle everywhere else in this app already — the plain todo checkbox asks nothing. Holding
the assistant to a stricter bar than the rest of the UI already applies to the identical action would
have been inventing a risk that is not there, not honoring the spirit of D5. The second-confirmation
modal itself reuses the shared `Modal` primitive rather than `ConfirmDialog` — that dialog's single
`message: string` prop cannot hold a per-action old→new diff or a delete's full record — and
`ProposalCard.tsx` exports `DeletePreview`/`UpdateSummary` so the modal shows the **same** content the
card already rendered, never a second, driftable copy.

**Consequence accepted, not overlooked:** bulk operations ("obriši sve zadatke o postolju") and moving
an inspection to a different hive or date via update are both out of scope — each resolver returns at
most one existing target, and `UpdateInspectionAsync` never applies `fields.date` as a new value (the
prompt uses it only to say *which* inspection is meant). Recorded in SPEC-17 §12 as decisions, not gaps
found later.

---

### Addendum, 2026-08-09 — SPEC-18: the advisor is retired, merged into the assistant

**Context.** This ADR's own "Alternatives considered" (above the Phase A body) already recorded that
merging the assistant into the advisor was considered and rejected at SPEC-17's design time: *"a router
guessing 'question or command' is a new failure mode, and a chat bubble is a poor host for an editable
form."* A day after SPEC-17 shipped complete, the question was asked again — this time the other
direction, folding the advisor **into** the assistant rather than the reverse — and the answer changed.

**Decision.** `/advisor` (SPEC-01) is retired. `AiAssistantService` now also answers plain questions,
using the same envelope Phase A already defined: a turn with `actions: []` and a full `reply` **is** the
answer. No router, no classification step, no new field. `AssistantPromptBuilder`'s system prompt was
rewritten to say so instead of deflecting to the advisor, and gained an optional `contextBlock` —
built by `HiveContextBuilder` (renamed from the advisor's `AdvisorContextBuilder`, otherwise unchanged)
when a hive is in scope, using the same access-check discipline `AdvisorService` already had (throw on
a fresh binding, degrade silently on a later turn).

**Why the original objections do not hold today:**
- **"A router guessing 'question or command' is a new failure mode."** There is no router. The
  classification was never a separate step to begin with — it is a side effect of what the single
  model call already returns. This was not obvious in the abstract at SPEC-17 design time; it only
  became visible once Phase A's envelope shape existed to inspect.
- **"A chat bubble is a poor host for an editable form."** `ProposalCard` was never rendered inside the
  reply bubble — it is, and always was, a separate element below it (§5 above). A Q&A answer sharing the
  bubble does not collide with a form that was never there.

**Consequences accepted, not overlooked:**
- **One combined monthly quota, not two.** `EnsureAiInteractionAsync` replaces both the assistant's own
  `EnsureAiCommandAsync` and the advisor's `EnsureAdvisorMessageAsync`. Not merely simpler: the quota
  gate must run *before* the Groq call (so a caller cannot spend past their limit), and before that call
  returns there is no way to know whether the turn would have been a question or a command — so a
  single pre-flight number is the only design the ordering actually permits, not a simplification chosen
  for its own sake.
- **Old conversations are migrated, not archived or discarded.** `deploy/data-migration/advisor-merge/`
  copies every `AdvisorConversations`/`AdvisorMessages` row into `AiAssistantSessions`/`AiAssistantTurns`
  (zero `AiAssistantAction` rows per migrated turn — under the new semantics that is correct, not a
  gap). Run by hand against production, same rigor as the July 2026 old-database import: `pg_dump`
  first, an id-map audit trail, a verify pass before the id-map is exported and the staging table
  dropped. Dropping `AdvisorConversations`/`AdvisorMessages` themselves is a **separate, later** deploy,
  done only once the backfill is confirmed correct — never in the same sitting as the copy.
- **`IAdvisorAiClient`/`GroqAdvisorAiClient` survive, renamed.** See ADR-024's addendum — a real,
  pre-existing consumer (`LearningTopicService`) depended on them for reasons unrelated to the advisor,
  which this decision's original framing had not anticipated.
- **`PlanFeature.AiAssistant` is deleted, not merely unused.** It was already vestigial before this
  change — referenced only inside `EnsureFeatureAsync`'s message switch, never actually passed to that
  method (the assistant's gate was always the hand-rolled `EnsureAiCommandAsync`/now
  `EnsureAiInteractionAsync`) — so removing it deletes dead code rather than live surface.

---

## ADR-034: Organization Activity Is Derived from the Data, Not Stored (partially supersedes SPEC-16 §3)

**Context.** The SystemAdmin organization table needed to answer "is this organization being used?".
SPEC-16 §3 had already designed an answer: a stored `Organization.LastActivityAt`, written by an
`ActivityTrackingWorker` off a channel, fed by a middleware that fires on every 2xx non-GET request
plus the two auth entry points. That design was chosen partly so that *reading* the app would count,
not only writing to it (§3.2).

**Decision.** The column is computed on read instead, from the records themselves.
`IOrganizationRepository.GetLastActivityAsync` takes `MAX(COALESCE(UpdatedAt, CreatedAt))` per
organization across fifteen queries — the fourteen tables an organization owns, plus `RefreshToken`
by `CreatedAt` for sign-in and session refresh — and merges them in memory. No column, no migration,
no worker, no middleware, no config.

**Why, given a spec already existed:**
- **It is correct retroactively.** SPEC-16 §3.3 names this as its own weakness: a stored column
  starts null everywhere and "the first 90 days after deploy the table must be read with that
  reserve". A derived value describes the entire history that is already in the database, on the day
  it ships. For a table whose purpose is deciding what to do about *old* organizations, that is not a
  nice-to-have — it is the feature.
- **It cannot silently miss a write path.** `MelariumDbContext.SaveChangesAsync` already stamps
  `UpdatedAt` on every modified `BaseEntity`, so a new feature is counted the day it is written. The
  middleware approach depends on every future write actually travelling through a non-GET HTTP
  request — true today, and a silent gap the first time something writes from a worker or a job.
- **The read-only-usage argument survives.** `RefreshToken.CreatedAt` gives sign-in and rotation
  without a new column, so the signal SPEC-16 wanted the heartbeat for is present anyway.

**Consequences accepted, not overlooked:**
- **Fifteen queries per admin list load**, plus fifteen narrowed ones on each single-organization
  read. This is a SystemAdmin-only page; each query is a plain indexed `GROUP BY`. Measured against
  a schema-only translation pass, all fifteen translate to Postgres SQL — verified before merge,
  since a LINQ shape that cannot be translated fails at runtime, not at build time.
- **Not a UNION.** `Concat` + `GroupBy` would be one round-trip, and is exactly the shape EF is least
  reliable at translating. Fifteen boring queries that always work beat one clever one that might not.
- **Rounds are queried separately from their parents** (`FeedingEntry`, `TreatmentRound`): ticking a
  feeding round off does not reliably bump the parent `Diet`, so without them an organization feeding
  its hives every second day would read as idle.
- **SPEC-16 is not closed by this.** Only its activity column is delivered. `IsActive`, `FirstPaidAt`,
  the computed `OrgStatus` badge, the billing worklists and the purge fix remain unbuilt, and the
  spec stays the record for them. Whoever picks it up must not re-add `LastActivityAt`.

---

## ADR-035: One Groq Model Id, Read From Configuration (2026-08-17 outage)

**Context.** Groq announced the deprecation of `llama-3.3-70b-versatile` on 2026-06-17 and stopped
serving it to free/developer-tier keys on 2026-08-17, answering `404 model_not_found`. That id was
pasted into four separate request bodies, so one upstream retirement took down four features at once —
the AI assistant, voice-note inspection parsing, the weekly AI summary and learning-article drafts —
with no code change on our side and no way to fix it without a redeploy. The vision id
(`meta-llama/llama-4-scout-17b-16e-instruct`, retired 2026-07-17) sat in two more.

**Decision.** `Features/Ai/GroqModels.cs` holds both ids. Every call site reads
`GroqModels.Chat(config)` / `GroqModels.Vision(config)`, backed by `Groq:ChatModel` /
`Groq:VisionModel`. The next retirement is one line in `.env` plus a restart.

- `Groq:AssistantModel` is **gone**, folded into `Groq:ChatModel`. It only ever existed as a
  commented-out line in `.env.example`; a deployment that had actually set it must rename it, or it
  silently falls back to the default.
- A blank config value means "not set", never "use the empty model id" — otherwise a stray blank line
  in `.env` becomes a 404 from Groq.
- The vision default is **`qwen/qwen3.6-27b`**, settled the same day after checking what Groq still
  serves: it is the only model in their catalogue that takes image input at all, and it covers what
  both image callers actually need — `json_object` **in the same request as the image**, a system
  message, and up to 5 images (we send one). It is not on the deprecation list, and its free-tier
  limits are identical to the chat model's (30 rpm / 1k rpd / 8k tpm / 200k tpd).

**Consequence that must not be lost:** the chat model is required to support
`response_format: json_object` — the assistant and `VoiceParsingService` both depend on JSON mode. A
model without it fails in a way that looks identical to an outage.

**Related, and why this was hard to see:** `AiAssistantService.CallAiAsync` catches every exception and
throws `BusinessRuleException("AI servis trenutno nije dostupan…")` **without an inner exception**, so
the real `404 model_not_found` never reached the logs — the cause had to be read off Groq's own
dashboard. The frontend then swallowed even that message (see `features/ai-assistant.md`). Two layers
of well-intentioned error handling turned a one-line upstream change into an invisible failure.
