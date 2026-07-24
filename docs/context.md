# Context — Current System State

> This file reflects what is **actually implemented** as of 2026-07-02.
> Update this file whenever a feature is completed or removed.
> Use this to avoid re-implementing existing functionality.

---

## Implemented Features

### Authentication & Accounts
- `POST /api/auth/login` — email + password, returns access token (**30 min**) + rotating refresh token (**14 days**)
- `POST /api/auth/register` — **self-service sign-up**: creates a new Organization and its OrganizationAdmin, auto-login
- `POST /api/auth/refresh` — rotates the refresh token; **reuse of a rotated token revokes the user's whole active set**
- `POST /api/auth/logout` — revokes the presented refresh token (idempotent)
- Rate limiting per client IP: login 5/min, register 5/min, refresh 20/min → `429`
- Passwords hashed with BCrypt; refresh tokens stored **hashed** (SHA-256)
- JWT claims: `sub` (int user id), `email`, `role`, `jti`, `organizationId?`, `apiaryId?`
- **Four roles** (`UserRole`): `SystemAdmin` (platform), `OrganizationAdmin` (whole org),
  `ApiaryAdmin` (one assigned apiary), `Beekeeper` (only explicitly assigned beehives)
- All resource authorization is centralized in `IAccessGuard` (Application/Common/Security)
- Frontend: `AuthContext`, `ProtectedRoute`, `AdminRoute`, `RoleRoute`, `SmartRedirect`;
  `apiClient` does single-flight refresh-on-401 with request replay
- Production bootstrap: SystemAdmin provisioned from `Bootstrap:SysAdminEmail/Password` env vars;
  demo accounts are seeded **only in Development** and locked on every production startup

### Organizations & Users (SystemAdmin)
- Full CRUD via `/api/admin/organizations` and `/api/admin/users`
  (`OrganizationsAdminController` + `UsersAdminController`)
- Role/org/apiary consistency rules enforced in `AdminService`
- Demo data (2 orgs, apiaries, hives) seeded via EF `HasData` migrations

### Organization Members (`/api/org/*`)
- OrganizationAdmin/ApiaryAdmin manage members: create ApiaryAdmin/Beekeeper accounts,
  assign apiaries to admins, assign beehives to beekeepers (`OrgManagementService`)

### Plans & billing (SPEC-09)
- Per-org subscription: `Organization.Plan` (Free/Standard/Pro/Max/**Partner** hidden) + `PlanValidUntil`
  + `PlanNotes`. Effective plan **computed** (`PlanHelper` — expired paid plan → Free). New orgs get a
  30-day Pro trial. `IPlanGuard` (config `Plans:*`, enforce on create only) gates apiaries/beehives/
  members/voice/advisor(+10-msg/mo Standard quota)/pastures/photo-AI/weekly-summary → **402 `plan-limit`**.
  v1 billing **manual + annual** (`PUT /api/admin/organizations/{id}/plan`; Paddle Phase 2). `GET
  /api/organizations/my-plan`; `/plans` page + global UpsellModal on 402; `PlanExpiring` alert (type 18).
  See `features/plans-billing.md` + ADR-028.

### Apiaries
- Full CRUD via `/api/apiaries`, org-scoped; ApiaryAdmin sees only their apiary,
  Beekeeper only apiaries containing their assigned hives
- `latitude`/`longitude` + map picker (react-leaflet)
- Weather: `GET /api/apiaries/{id}/weather` — 7-day forecast + current conditions from Open-Meteo (no API key)

### Beehives
- Full CRUD via `/api/beehives`; hive lists return inspection **counts** (grouped query, no inspection rows)
- Types/materials use Bosnian display labels (`BsLabels`)
- Auto-generated `uniqueId` (Guid) + QR code (Base64 PNG, QRCoder) on creation
- QR codes: only in the **detail** DTO; bulk label export via `GET /api/beehives/by-apiary/{id}/qr-codes`
- Public scan flow: `GET /api/beehives/scan/{uniqueId}` (anonymous) + `/scan/:uniqueId` route + in-app QR scanner (`@zxing`)
- `POST /api/beehives/regenerate-qr-codes` (SystemAdmin) regenerates all QR codes after a frontend URL change

### Inspections
- Full CRUD via `/api/inspections`; temperature auto-filled from the apiary's current weather (best-effort)
- **Voice input**: `POST /api/inspections/parse-voice` — audio → Groq Whisper large-v3 transcription (BCS)
  → Llama 3.3 70B field extraction → `{date, honeyLevel, broodStatus, notes}` + transcript.
  15 MB size limit + 10/min rate limit. Frontend records via `useVoiceInput`.
- **Photos (SPEC-05)**: up to 5 per inspection (8 MB, JPEG/PNG/WebP by header bytes), stored via
  `IFileStorage` (`Storage:Provider = Local | S3`, prod → S3-compatible bucket e.g. Cloudflare R2;
  new package `AWSSDK.S3`), streamed through the API (private bucket). Optional **AI frame analysis**
  (`Groq:VisionModel`, default Llama 4 Scout; rate limit `photo-analyze` 5/min/IP; images ≤ ~3 MB —
  Groq 4 MB base64 cap). See `features/inspection-photos.md` + ADR-027.

### Queens (Matice)
- Per-beehive queen tracking via `/api/beehives/{id}/queens` + `/api/queens/{id}` — active queen + full history
- At most one Active queen per hive (service rule **and** partial unique DB index);
  registering a new queen atomically closes the old one as Replaced
- Mark color derived from birth year (international code) when not supplied; Bosnian labels via `BsLabels`
- "Matica" card on `BeehiveDetailPage` (`QueenSection`): color dot, season badge (≥ 3. sezona = warning),
  replace/history/edit modals; access = same as inspections (assigned Beekeepers included)
- Covered by unit tests (`QueenServiceTests`, `QueenMarkColorHelperTests`)

### Diets (Feeding Programs)
- Full CRUD via `/api/diets`; entries auto-generated from duration + frequency
- State machine: `NotStarted → InProgress → Completed | StoppedEarly`
- `POST /api/diets/{id}/complete-early` (requires comment); per-entry completion endpoint
- Delete allowed only before start with no completed entries
- Covered by unit tests (`Melarium.Application.Tests`)

### Todos
- Full CRUD via `/api/todos`; scoped to either an Apiary or a Beehive
- Priority (Low/Medium/High), optional due date, optional assignee
- `GET /api/todos/open` — role-scoped open todos; assignable-users endpoint per beehive

### Expenses
- Full CRUD via `/api/expenses` with line items (`ExpenseItem`)
- Client-side receipt scanning (`ReceiptScanPage`): tesseract.js OCR (`hrv` model) + heuristic line parser

### Harvests (Vrcanja)
- Full CRUD via `/api/harvests` (apiary-scoped event + per-hive `HarvestEntry`); `HoneyType` with Bosnian `BsLabels`
- Role scoping via `IAccessGuard`: managers write within scope; **Beekeeper read-only**, only harvests containing an assigned hive
- Apiary immutable after creation; update replaces the entry set; entries must belong to the apiary (else 400)
- `GET /api/harvests/hive/{id}/yield` — per-hive season + per-year totals (hive detail "Prinos" card)
- Stats extended: `seasonTotalKg`, `estimatedRevenue`, `kgByApiary`, `kgByHoneyType`, `topHivesByYield`, `yearlyYield`
- UI: "Vrcanja" sidebar item, `HarvestsPage` + `HarvestFormPage`, apiary/hive detail sections, StatsPage charts
- Covered by unit tests (`HarvestServiceTests`). See `docs/features/harvests.md`.
- Harvest form warns (non-blocking) when the date falls inside a treatment/karenca window (SPEC-08 soft integration)

### Treatments (Evidencija tretmana)
- Legal medicine register per EU 2019/6 / BiH propisi — full CRUD via `/api/treatments`
  (apiary-scoped event + per-hive `TreatmentEntry`); purpose/substance/method enums with Bosnian `BsLabels`
- `karencaUntil`/`status` computed, never stored (`TreatmentStatusHelper`: U toku → Karenca → Završen)
- Role scoping via `IAccessGuard` (same matrix as Harvests): managers write, **Beekeeper read-only**,
  only treatments containing an assigned hive; apiary immutable; update replaces the entry set
- **PDF register** per apiary/year, client-side jsPDF with embedded DejaVu Sans (č/ć/đ), A4 landscape
  (`shared/utils/treatmentPdf.ts` + lazy `pdfFont.ts` chunk)
- Alert rules `StripsLeftIn` (trake > 42 dana) + `KarencaEnded`; advisor context "Zadnji tretman" line
- UI: "Tretmani" nav item, `TreatmentsPage` (+ PDF button, `?beehiveId=` history filter) +
  `TreatmentFormPage` (product presets, hive checkboxes), `HiveTreatmentCard`, `ApiaryTreatmentsSection`
- Covered by unit tests (`TreatmentServiceTests`, `TreatmentStatusHelperTests`). See `docs/features/treatments.md`.

### AI Advisor (AI Savjetnik)
- Bosnian chat advisor (text + voice) via `/api/advisor`; personal conversations, optionally bound to a hive
- Hive-bound conversations grounded in real data (`AdvisorContextBuilder`: inspections, diet, todos, queen, yield, latest treatment, weather)
- Reuses the Groq stack; transcription extracted to shared `ITranscriptionService`, chat via `IAdvisorAiClient`
- Ownership enforced (404 for others); 60-msg cap; transactional AI (nothing persisted on failure); `ai-chat` 10/min
- UI: "AI Savjetnik" sidebar, `/advisor` (all roles), "Pitaj savjetnika" on hive detail, voice→transcript→review→send
- `useVoiceInput` moved to `core/hooks/`. Covered by unit tests. See `docs/features/ai-advisor.md`.

### Offline Inspections (Offline unos pregleda)
- Frontend-only (SPEC-07): creating an inspection offline lands in an IndexedDB **outbox**
  (`core/offline/outbox.ts`, keyed by owner email — session has no numeric user id) and syncs
  automatically on reconnect through the normal axios path (`syncOutbox.ts`)
- Single-flight flush per tab + cross-tab via Web Locks; BroadcastChannel keeps tab badges live
- 4xx on sync → item `failed` with the API message (edit/discard on `/outbox`); network error → stays pending
- UI: offline banner + CloudOff badge in `Layout`, `/outbox` page (Pošalji sada/Uredi/Obriši),
  hive-page hint card; voice input disabled offline (server transcription)
- No SW background sync (ADR-026); Workbox `NetworkFirst` already covers all API GETs (read side)
- Dev harness: `await __outboxSelfTest()` in the console. See `docs/features/offline-inspections.md`.

### Pastures & Migration (Pašnjaci i selidbe)
- Migratory beekeeping (SPEC-10): org-scoped `Pasture` registry + `ApiaryMove` events via
  `/api/pastures` and `/api/apiaries/{id}/moves`; writes OrgAdmin/SystemAdmin (`Roles.OrgManagers`)
- **Move snapshots pasture coordinates into `Apiary.Latitude/Longitude`** — weather, frost alerts i
  mape prate selidbu bez ijedne izmjene njihovog koda; `FromPasture` se rješava na serveru
- Yield per pasture computed via `PastureAttribution` (Domain/Common): harvest pripada pašnjaku na
  kojem je pčelinjak bio na datum vrcanja; stats `kgByPasture` + "Matična lokacija" bucket
- Delete guards: pašnjak blokiran dok je referenciran; briše se samo posljednja selidba (revert)
- Optional `CertificateNumber` (veterinarska svjedodžba) po selidbi — legal, LOT precedent
- UI: "Pašnjaci" nav (OrgAdmin+), `PasturesPage` (mapa + `LocationPickerModal`), chip trenutnog
  pašnjaka + "Preseli" modal + "Selidbe" sekcija na `ApiaryDetailPage`, "Prinos po pašnjaku" u Stats
- Advisor kontekst: linija "Pašnjak: {name}, od {datum}". Testovi: `PastureAttributionTests`,
  `ApiaryMoveServiceTests`, `PastureServiceTests`. See `docs/features/apiary-migration.md`.

### Learning (Edukacija)
- Platform-wide educational articles (SPEC-06): SystemAdmin authors, everyone reads once published
- `/api/learning-topics` (published only, `isRead` per user, category/month filter) +
  `/api/admin/learning-topics` (CRUD, publish toggle, AI draft via Groq — `ai-chat` rate limit)
- `Months int[]` (Postgres) drives the "Aktuelno u {mjesecu}" section; null = evergreen
- First publish → one **in-app** notification per user (`LearningTopicPublished`, batch, no email), exactly once
- Read tracking: unique (TopicId, UserId), marked after ~5 s on the topic page, idempotent POST
- UI: "Edukacija" nav (all users), `LearningPage` + `LearningTopicPage` (react-markdown, ADR-025;
  **"Poslušaj"** TTS via `useSpeech` — bs→hr→sr voice pick, stops on navigation), admin list+form
  with markdown preview and AI draft panel
- Dev-only seed: 6 starter topics (`SeedLearningTopicsAsync`). Tests in `LearningTopicServiceTests`.
  See `docs/features/learning.md`.

### Calendar & Stats
- `GET /api/calendar` — role-scoped todos + feeding entries (Bosnian labels)
- `GET /api/stats` — org-scoped (platform-wide for SystemAdmin): totals, distributions,
  12-month inspection/temperature series, top hives (Recharts on the frontend)

### Notifications
- In-app bell (30 s polling) + email via background queue
- 9 `NotificationType`s fired on account/org/apiary/beehive assignment changes, hive creation, todo creation
- Email: `NotificationService` enqueues → `EmailNotificationWorker` (BackgroundService, Channel)
  resolves the recipient and sends via MailKit — **SMTP never blocks a request**
- Email silently skipped unless `Smtp:Host` + `Smtp:Password` are configured
- `POST /api/notifications/test-email` (SystemAdmin) — direct SMTP test
- All notification texts are in Bosnian
- **Smart alerts (SPEC-04):** `AlertScanWorker` (BackgroundService) runs daily at `Alerts:ScanHourUtc`,
  evaluating 6 toggleable rules — `InspectionOverdue`, `HoneyLevelDrop`, `FrostWarning` (Open-Meteo),
  `OldQueen` (March only), `StripsLeftIn` + `KarencaEnded` (SPEC-08) — deduped against the
  notifications table (`ExistsRecentAsync`), delivered via the existing bell + email queue
- **Weekly AI summary:** on Mondays, a deterministic per-org digest (`WeeklyDigestBuilder`) → one Groq
  call (`llama-3.3-70b-versatile`) → Bosnian bullet report delivered as `WeeklySummary` to OrgAdmins +
  ApiaryAdmins; AI failure skips silently. New config block `Alerts:*`. See `docs/features/smart-alerts.md`.

### Profile
- `GET/PUT /api/profile` — name/email + password change

---

## Infrastructure / Cross-Cutting

| Concern | Implementation |
|---|---|
| Database | **PostgreSQL** (Npgsql), EF Core 10, auto-migrate on startup |
| Projects | API → Application → Domain; **Entity** (persistence) + Infrastructure (email) implement Application interfaces |
| Validation | FluentValidation, **explicit `ValidateAsync` in controllers** (no auto-validation — see ADR-010) |
| Mapping | AutoMapper per-feature profiles; manual mapping where DTOs have computed fields (Diets, Admin) |
| Error handling | `GlobalExceptionMiddleware` → Problem-Details-style JSON; **exception details only in Development** |
| Auth | JWT Bearer HS256, 30 min access + 14 d refresh rotation |
| Secrets | **Not in the repo.** Env vars in production (`Jwt__Secret`, `Smtp__Password`, `Groq__ApiKey`, `ConnectionStrings__DefaultConnection`, `Bootstrap__*`); `appsettings.Development.json` / user-secrets locally |
| Rate limiting | Fixed-window per IP: login/register 5/min, refresh 20/min, parse-voice 10/min |
| Health check | `GET /health` (liveness, used by Render) |
| CORS | `AllowedOrigins` config (comma-separated), overridable via env var |
| API docs | Swagger UI at `/swagger` (all environments) |
| Frontend caching | TanStack Query v5; 30 s notification polling; PWA (Workbox NetworkFirst, 24 h) |
| Localization | UI + API `*Name` fields + notifications in Bosnian (`BsLabels` backend, label maps frontend) |
| Tests | `Melarium.Application.Tests` (xunit + NSubstitute): AccessGuard authorization matrix, Diet state machine, refresh-token rotation |
| Deployment | Backend: Render (Docker, TLS at proxy). Frontend: Vercel (`VITE_API_URL`). Dev proxy → `http://localhost:62648` |

---

## Pending / Not Yet Implemented

> Add items here when planned but not yet built.

**All roadmap specs shipped** (see `docs/specs/README.md`).

**Shipped (were specced):** SPEC-01 AI Advisor ✅, SPEC-02 Harvest Log ✅, SPEC-03 Queen Tracking ✅, SPEC-04 Smart Alerts & Weekly AI Summary ✅, SPEC-05 Inspection Photos & AI Frame Analysis ✅, SPEC-06 Learning Module ✅, SPEC-07 Offline Inspections ✅, SPEC-08 Treatment Log ✅, SPEC-09 Plans & Billing ✅ (v1 manual annual billing; Paddle Phase 2 remains), SPEC-10 Apiary Migration ✅

**Unspecced ideas:**

- Multi-language support (UI is Bosnian-only; no i18n framework)
- Reports/analytics export (PDF exists only for QR labels)
- Push notifications (PWA) — currently 30 s polling only
- Integration tests against a real PostgreSQL (unit tests only for now)
- Refresh token in httpOnly cookie (currently localStorage — see ADR-009)
