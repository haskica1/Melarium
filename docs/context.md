# Context — Current System State

> This file reflects what is **actually implemented** as of 2026-07-02.
> Update this file whenever a feature is completed or removed.
> Use this to avoid re-implementing existing functionality.

---

## Implemented Features

### Authentication & Accounts
- `POST /api/auth/login` — **email _or_ phone** + password, returns access token (**30 min**) + rotating refresh token (**14 days**)
- `POST /api/auth/register` — **self-service sign-up**: creates a new Organization and its OrganizationAdmin, auto-login
- `POST /api/auth/refresh` — rotates the refresh token; **reuse of a rotated token revokes the user's whole active set**
- `POST /api/auth/logout` — revokes the presented refresh token (idempotent)
- `POST /api/auth/forgot-password` — emails a single-use reset link; **always 204**, so the response
  cannot be used to test which addresses have accounts
- `POST /api/auth/reset-password` — redeems the token, sets the new password, **revokes every session**,
  and marks the address verified (receiving the mail proved control of it)
- `POST /api/auth/verify-email` — redeems a verification token; idempotent when already verified
- `POST /api/auth/resend-verification` — authenticated; re-sends the link to the signed-in user
- Rate limiting per client IP: login 5/min, register 5/min, refresh 20/min, **auth-email 3/min**
  (forgot-password, resend-verification), **auth-token 10/min** (reset, verify) → `429`
- **Phone is a second login identifier**: `User.Phone`, unique (partial index, NULL allowed),
  stored canonical E.164 via `Common/Validation/PhoneRules` — "061 123 456", "+387 61 123 456" and
  "0038761123456" all resolve to one account. `LoginDto.Identifier` routes on '@'; the legacy
  `email` field still binds (cached PWA clients)
- **Required wherever an account is created** — self-registration, `/api/admin/users`,
  org member creation. Still **nullable**, because accounts predating the field have none and keep
  signing in by email
- **Editable** in the profile and in admin user edit. A blank field means *leave the stored number
  unchanged*, never "clear it" — an older client that omits the field must not silently strip a
  login identifier. Uniqueness on change is `IUserRepository.IsPhoneTakenAsync(phone, excludeUserId)`;
  excluding your own id is what lets you re-save your profile, and normalising before comparing is
  what stops a differently-written form of your own number reading as a change
- Phone is **not verified** (no SMS): it can sign you in, but it must not become a recovery
  channel — password reset stays email-only
- Bad credentials return **401** (not 422), with an identical message and cost for
  "unknown account" and "wrong password" (dummy BCrypt verify equalises timing)
- Passwords hashed with BCrypt; refresh + emailed tokens stored **hashed** (SHA-256).
  One password policy for every entry point (`Common/Validation/PasswordRules`, min 8)
- **Email verification is soft**: recorded on `User.EmailVerifiedAt`, surfaced as a profile banner,
  never blocks sign-in. Accounts predating the feature were backfilled as verified by its migration
- **Session revocation** (`ISessionRevoker`) fires wherever trust changes: password reset, password
  change, and any edit to a user's role / organisation / apiary (all three are JWT claims)
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

### AI Assistant (AI Asistent, SPEC-17 all three phases + SPEC-18 Q&A merge)
- Voice/text command → proposals → **explicit confirmation** → records. Creates, updates, completes or
  deletes **pregled**/**zadatak**; one sentence may produce several actions, each individually uncheckable
- **Also answers beekeeping questions in the same conversation (SPEC-18):** an envelope with empty
  `actions` and a full `reply` **is** the answer — no separate router step. Grounded in a hive's real
  data (`HiveContextBuilder`, moved from the retired advisor's `AdvisorContextBuilder`: inspections,
  diet, todos, queen, yield, latest treatment, weather) when one is in scope; access re-checked before
  context is built (throws on session start, degrades silently on a later turn)
- **ADR-033:** the executor builds the same DTOs the forms post and calls the **existing** services
  (`InspectionService`/`TodoService`, incl. their `Update`/`Delete`), never repositories — access, plan
  limits, auto-temperature and todo notifications all come along; it also runs the controllers'
  validators itself
- Targets resolved from `IAccessGuard.GetAccessible{Apiaries,Beehives}Async` only, so an out-of-scope hive
  is unreachable by construction; `HiveNumberMatcher` reused; "sve košnice" expands over one apiary
- Pure + unit-tested: `AiEnvelopeParser` (never throws on model output), `AiTargetResolver`,
  `AssistantPromptBuilder` ("danas" via `AppTimeZone`, not `UtcNow`)
- Nothing persisted on AI failure; partial batch failure reported per action; double-confirm refused;
  ceiling `Ai:MaxActionsPerCommand` (50). Standard+ with **one combined** monthly interaction quota
  (`EnsureAiInteractionAsync`, `Plans:{Plan}:AiInteractionsPerMonth`) covering questions and commands
  alike — merged from the previously separate advisor/assistant quotas
- UI: floating `Sparkles` launcher in `Layout` (hidden offline), editable `ProposalCard`, `/assistant`
  history page, "AI Asistent" nav — the former separate "AI Savjetnik" nav item and `/advisor` route are
  retired. Assistant replies render as Markdown (`MarkdownMessage`); a vet/AFB-EFB disclaimer footer
  carried over from the advisor. Reuses `ai-chat`/`voice-parse` limits — no new policy
- **Conversation:** unresolved apiary/hive/todo/inspection candidates become tappable buttons
  (`AssistantClarificationBuilder`, pure, capped at 8) on the **latest** assistant turn only
  (`CandidatesJson`, zeroed on every earlier turn); tapping sends the text as an ordinary new turn.
  System prompt carries a continuation rule so a short follow-up combines with the fields dictated
  earlier in the session instead of being read as an isolated command
- **Update/complete/delete:** `AiTargetResolver` gained existing-record resolution — todos by title
  (search pool = `ITodoService.GetAllOpenForCurrentUserAsync()`, already role-scoped), inspections by
  date within one hive, no date = the most recent one. Fetched only when an action actually needs it.
  The resolved target is **fixed at propose time**, never re-picked from the confirm request. A batch
  with any update/delete needs a **second, separate confirmation** (`isDestructive` on the action DTO)
  — `CompleteTodo` is deliberately excluded: it is a one-tap, reversible toggle everywhere else in the
  app too. `AiActionExecutorTests` tests the real executor directly, not just through a mock
- **Old advisor conversation history migrated, not discarded:** `deploy/data-migration/advisor-merge/`
  copies `AdvisorConversations`/`AdvisorMessages` into the unified shape (run by hand against
  production; dropping the old tables is a separate, later deploy). `useVoiceInput` lives in
  `core/hooks/` (SPEC-01 precedent, unchanged). See `docs/features/ai-assistant.md`.

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
- AI Asistent kontekst: linija "Pašnjak: {name}, od {datum}". Testovi: `PastureAttributionTests`,
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

### Feedback (Povratne informacije, SPEC-13)
- Any signed-in user submits bug/žalba/pohvala/prijedlog/pitanje/ostalo via a header-reachable modal;
  `PageContext` + `UserAgent` captured client-side, optional screenshot (5 MB, type from header bytes,
  reuses `IFileStorage`). Rate limit `feedback` 3/min
- **Notification split:** in-app bell to **every** SystemAdmin (`NotifyManyInAppAsync`, no e-mail) plus
  **one** e-mail to `Feedback:NotifyEmail` — so a second SystemAdmin doesn't multiply the mail. Status
  change/reply notifies the submitter with bell **and** e-mail
- `QueuedEmail` gained a second addressing mode for this (`UserId` now `int?` + `ToEmail`/`ToName`,
  factories `ForUser`/`ForAddress`); `EmailNotificationWorker` prefers an explicit address. SMTP stays
  off the request path (ADR-021 unchanged)
- Not tenant-scoped → **no `IAccessGuard`**; flat SystemAdmin-vs-owner split like other `api/admin/*`.
  Another user's row returns **404, never 403**
- UI: `FeedbackFormModal`, "Moje povratne informacije" on `ProfilePage`, `FeedbackAdminPage`
  (`/admin/feedback`) + nav item with untriaged badge. See `features/feedback.md`

### In-app help & onboarding (SPEC-14)
- **Frontend only — no entity, no endpoint, no migration.** Help content is a static typed registry
  (`core/help/helpContent.ts`), lazily imported as its own chunk; it describes the UI, so it changes in
  the same commit as the UI. Edukacija (SPEC-06) stays the DB-backed CMS for long-form content, and the
  panel links to it **by category, never by topic id**
- One icon rendered from `Layout` and resolved by route (`matchPath`, most-specific-first); a route with
  no entry renders no icon. `HelpProvider`/`useHelpTrigger` let pages open it — `EmptyState` gained an
  optional `onHelp`
- Welcome flow once per (user, browser); its state lives in `useHelp` so it holds back the per-page
  auto-open (otherwise two dialogs stacked on a new user's first screen). "Preskoči uvod" pre-marks the
  three auto-open pages, which is the grandfathering for existing users
- "Prvi koraci" checklist is **derived from data** (apiary → hive → inspection), never stored, and
  removes itself when done — ADR-028's computed-state reasoning
- `?` opens help (ignored while typing). Preference toggle on `ProfilePage`. Flags in `localStorage`
  keyed by e-mail. See `features/help-onboarding.md`

### Invitations — "Pozovi prijatelja" (SPEC-15, **Faza 1**)
- A personal share link (`User.ReferralCode`, unhashed like `CalendarSettings.FeedToken`) invites people
  **to the platform** — the invitee gets their **own** organization. Not a way to add a member to yours
- Invitee gets **60** trial days instead of 30, at registration. Inviter's organization gets **+30** days
  when the invitee **verifies their e-mail** — capped at 180 lifetime / 5 per 30 days / one per invited org
- `IPlanCredit.GrantDaysAsync` (beside `PlanGuard`) is the **only** code that does arithmetic on
  `PlanValidUntil`. A lifetime plan is never given an expiry, `Plan` is only ever raised (and only from an
  effective Free), and **`PlanNotes` is never written** — `PlansPage` detects the trial by exact string
- Attribution: `?ref=` first, then a match on an address we had already invited. **An unknown or malformed
  code never fails a registration.** Attribution and reward run *after* the caller's own save commits —
  a shared `DbContext` means `try/catch` alone would not protect them. ADR-032
- **Phase 2 (sending invitations by e-mail) is not built.** See `features/invitations.md`

### Profile
- `GET/PUT /api/profile` — name/email + password change
- "Moje povratne informacije" (SPEC-13) + help preference toggle (SPEC-14) sections

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
| Secrets | **Not in the repo.** Env vars in production (`Jwt__Secret`, `Smtp__Password`, `Groq__ApiKey`, `ConnectionStrings__DefaultConnection`, `Bootstrap__*`, `Feedback__NotifyEmail`); `appsettings.Development.json` / user-secrets locally |
| Rate limiting | Fixed-window per IP: login/register 5/min, refresh 20/min, parse-voice 10/min, feedback 3/min |
| Health check | `GET /health` (liveness, used by Render) |
| CORS | `AllowedOrigins` config (comma-separated), overridable via env var |
| API docs | Swagger UI at `/swagger` — **Development only** (not exposed in production) |
| Security headers | `SecurityHeadersMiddleware` (nosniff, DENY framing, Referrer/Permissions-Policy, CSP) on the API; HSTS + SPA headers in nginx |
| Reverse proxy | `UseForwardedHeaders` restores the real client IP — rate limiting partitions per client, not per proxy |
| Frontend caching | TanStack Query v5; 30 s notification polling; PWA (Workbox NetworkFirst, 24 h) |
| Localization | UI + API `*Name` fields + notifications in Bosnian (`BsLabels` backend, label maps frontend) |
| Tests | `Melarium.Application.Tests` (xunit + NSubstitute): AccessGuard authorization matrix, Diet state machine, refresh-token rotation |
| Deployment | Backend: Render (Docker, TLS at proxy). Frontend: Vercel (`VITE_API_URL`). Dev proxy → `http://localhost:62648` |

---

## Pending / Not Yet Implemented

> Add items here when planned but not yet built.

**All roadmap specs shipped** (see `docs/specs/README.md`).

**Shipped (were specced):** SPEC-01 AI Advisor ✅ (superseded by SPEC-18, merged into AI Asistent), SPEC-02 Harvest Log ✅, SPEC-03 Queen Tracking ✅, SPEC-04 Smart Alerts & Weekly AI Summary ✅, SPEC-05 Inspection Photos & AI Frame Analysis ✅, SPEC-06 Learning Module ✅, SPEC-07 Offline Inspections ✅, SPEC-08 Treatment Log ✅, SPEC-09 Plans & Billing ✅ (v1 manual annual billing; Paddle Phase 2 remains), SPEC-10 Apiary Migration ✅, SPEC-13 User Feedback ✅, SPEC-14 In-App Help ✅, SPEC-15 Invite a Friend 🔨 (Faza 1: link + atribucija + nagrada; Faza 2 e-mail kanal ostaje)

**Unspecced ideas:**

- Multi-language support (UI is Bosnian-only; no i18n framework)
- Reports/analytics export (PDF exists only for QR labels)
- Push notifications (PWA) — currently 30 s polling only
- Integration tests against a real PostgreSQL (unit tests only for now)
- Refresh token in httpOnly cookie (currently localStorage — see ADR-009)
