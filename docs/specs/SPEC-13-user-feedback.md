# SPEC-13 — Korisnički feedback i prijava problema ("User feedback & issue reporting")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-30) — see `features/feedback.md` + ADR-030 |
| **Effort** | M (~1.5 days — screenshot attachment is in v1 per Asim's decision 2026-07-30) |
| **Depends on** | nothing new — reuses the notification/email queue (ADR-021) and `IFileStorage` (ADR-027) |
| **New secrets / packages** | no packages. One new config value: `Feedback:NotifyEmail` (env `Feedback__NotifyEmail`) |
| **Breaking** | No — purely additive (one new table, no existing column/consumer touched) |

## Goal

Today a user who hits a bug, has a complaint, wants to compliment something, or has an idea has no
in-app way to tell Asim — only outside channels (word of mouth, a personal message). This spec adds
a structured **feedback/issue-report form** reachable from anywhere in the app, delivered to
SystemAdmin via the existing in-app notification **and** email pipeline, plus an **admin dashboard**
to triage, track status, and (optionally) respond — closing the loop back to the user who reported it.

This is explicitly meant to cover more than bug reports: praise, complaints, feature ideas and plain
questions all go through the same door, categorized so they don't get lost together.

## User stories

- As any logged-in user, I can open a feedback form from the header (no matter what page I'm on),
  pick a category (bug/complaint/compliment/feature idea/question/other), write a message, and send it.
- As a user reporting a bug, I don't have to explain "which page I was on" — it's captured automatically.
- As a user, I can see the status of what I've reported ("Moje povratne informacije" on my profile),
  so I'm not left wondering if it disappeared into a void.
- As SystemAdmin, I get notified (bell + email, exactly like any other notification today) the moment
  someone submits feedback, with a preview of what it's about.
- As SystemAdmin, I open a dashboard listing everything ever submitted, newest first, filterable by
  type and status, with an unread/"Novo" count I can see at a glance.
- As SystemAdmin, I mark something as being looked at or resolved, optionally leave a short reply, and
  the person who submitted it gets notified back.

## Domain rules

### The model decision (read this first)

Feedback is **not** tenant-scoped the way apiaries/beehives are — it's a platform-level concern
between one user and SystemAdmin, not something an OrganizationAdmin manages for their org. So this
does **not** go through `IAccessGuard` (which exists specifically for the Organization → Apiary →
Beehive tenant hierarchy). Authorization here is the same flat "SystemAdmin vs. the record's own
owner" split already used by `UsersAdminController` / `OrganizationsAdminController` /
`LearningTopicsAdminController` — two controllers, one gated by `[Authorize(Roles = Roles.SystemAdmin)]`
and one that's just `[Authorize]` and scopes every query to `WHERE UserId = currentUser.UserId`. No
new authorization abstraction is needed.

**Categories, deliberately wider than just bugs** (`FeedbackType`): `Bug`, `Complaint` ("Žalba"),
`Compliment` ("Pohvala"), `FeatureRequest` ("Prijedlog"), `Question` ("Pitanje"), `Other` ("Ostalo").
The brief explicitly asked for bug-to-praise-to-complaint coverage; feature ideas and plain questions
are a natural, near-zero-cost extension of the same form and give users somewhere to put things that
aren't complaints but also aren't bugs.

**`Severity`** (`Low`/`Medium`/`High`/`Critical`) is a nullable field on every submission but only
*shown* in the UI when `Type` is `Bug` or `Complaint` — a compliment or feature idea has no severity.

### Rules

- Every submission belongs to the authenticated submitter (`UserId`) — there is no anonymous or
  logged-out feedback form in this spec (see Open Questions Q5 for why).
- `PageContext` (the route/page the user was on) and `UserAgent` are captured **client-side,
  automatically**, not typed by the user. They are descriptive only — never used for authorization
  or trusted as anything but a debugging hint.
- `OrganizationId` is snapshotted from `ICurrentUser.OrganizationId` at submit time (already available
  today, no new JWT claim needed) so SystemAdmin can see which organization/plan tier a piece of
  feedback came from without an extra join or lookup. Null for a SystemAdmin submitting feedback
  themselves (they have no org) — expected, not an error.
- Deleting the submitting user (SystemAdmin can already delete users) **must not delete their
  feedback history** — `UserId` is nullable with `ON DELETE SET NULL`, mirroring the existing
  `Diet.CreatedById`/`Todo.CreatedById` pattern. The content (type/subject/message) survives; only the
  "who" attribution is lost.
- A submission's `Status` (`New → InReview → Resolved` or `Dismissed`) is SystemAdmin-only to change.
  There is no separate "close the ticket yourself" action for the submitter — this isn't a support-desk
  product, just a triage queue for one admin.
- **v1 response model is a single `AdminResponse` field**, not a threaded conversation. Setting it (or
  changing status) fires one notification back to the submitter. A back-and-forth chat is explicitly
  out of scope (see Out of scope) — if usage shows people actually want to reply again, that's a
  follow-up, not a v1 requirement.
- Rate-limited per user/IP to stop accidental double-submits or spam, same fixed-window mechanism as
  existing auth-email endpoints.

## Backend

### Entities

```
Feedback : BaseEntity
  UserId            int?    (FK User, SET NULL)          // submitter; survives account deletion
  OrganizationId    int?    (FK Organization, SET NULL)   // snapshot for admin triage context
  Type              FeedbackType enum                     // Bug, Complaint, Compliment, FeatureRequest, Question, Other
  Severity          FeedbackSeverity? enum                // Low, Medium, High, Critical — UI-relevant for Bug/Complaint only
  Subject           string(150)
  Message           string(2000)
  PageContext       string(300)?                          // e.g. "/beehives/42" — captured client-side
  UserAgent         string(300)?
  ScreenshotStoragePath string(500)?                       // IFileStorage key — Phase C only, null in Phase A/B
  Status            FeedbackStatus enum                    // New, InReview, Resolved, Dismissed
  AdminResponse     string(1000)?
  RespondedAt       DateTime?
  RespondedById     int?    (FK User, SET NULL)
```

No junction tables, no child entities — this is deliberately one flat table.

### EF configuration

- `FeedbackConfiguration`: `HasOne(User).WithMany().HasForeignKey(UserId).OnDelete(SetNull)`; same
  `SetNull` treatment for `Organization` and `RespondedBy`. `HasIndex(f => f.Status)`,
  `HasIndex(f => f.CreatedAt)` (admin list is always newest-first), `HasIndex(f => f.Type)`.
  `ToTable("Feedbacks")`.
- Picked up automatically by `modelBuilder.ApplyConfigurationsFromAssembly(...)` in
  `MelariumDbContext` — no manual registration beyond adding the `DbSet<Feedback>` property.

### Repository — `IFeedbackRepository` (in `Melarium.Application/Common/Interfaces/`, per ADR-013 —
not the stale `Melarium.Infrastructure` path `common-patterns.md` still shows)

```csharp
Task<IEnumerable<Feedback>> GetByUserAsync(int userId);                          // "mine" list, newest first
Task<Feedback?> GetByIdForUserAsync(int id, int userId);                          // ownership-scoped single read
Task<IEnumerable<Feedback>> GetAllAsync(FeedbackType? type, FeedbackStatus? status); // admin list, filters in SQL
```

Plus one small, generically useful addition to the existing `IUserRepository`:

```csharp
Task<List<int>> GetSystemAdminIdsAsync();   // WHERE Role == UserRole.SystemAdmin
```

### Validation & business rules

- `type` required, valid enum.
- `severity` optional; if present must be a valid enum (not otherwise constrained to `Type` server-side
  — the UI only shows it conditionally, but a client sending it regardless is harmless).
- `subject` required, trimmed, 3–150 chars.
- `message` required, trimmed, 10–2000 chars (rejects empty/whitespace-only spam).
- `pageContext` / `userAgent` optional, ≤300 chars each.
- Admin status update: `status` required valid enum; `adminResponse` optional, ≤1000 chars.
- Screenshot: same rules as inspection photos — content type sniffed from **header bytes**
  (JPEG/PNG/WebP only), never trusted from the client; 5 MB cap (smaller than inspections' 8 MB, since
  this is a UI screenshot, not a macro frame photo). One screenshot per feedback. Uploading to a
  feedback that already has one → 422; the user deletes and re-uploads rather than silently orphaning
  a blob.

### Endpoints

**`FeedbackController`, `/api/feedback`, `[Authorize]` (any role):**

| Method | Path | Body → Returns |
|---|---|---|
| POST | `/feedback` | `{ type, severity?, subject, message, pageContext?, userAgent? }` → `201 FeedbackDto` |
| GET | `/feedback/mine` | → `FeedbackDto[]` (own submissions, newest first) |
| GET | `/feedback/mine/{id}` | → `FeedbackDetailDto` (404 if not the owner — never 403, don't confirm existence of other users' rows) |

**`FeedbackAdminController`, `/api/admin/feedback`, `[Authorize(Roles = Roles.SystemAdmin)]`:**

| Method | Path | Body → Returns |
|---|---|---|
| GET | `/admin/feedback?type=&status=` | → `AdminFeedbackDto[]` (all, newest first, includes submitter name/email/org) |
| GET | `/admin/feedback/{id}` | → `AdminFeedbackDetailDto` |
| PUT | `/admin/feedback/{id}/status` | `{ status, adminResponse? }` → `AdminFeedbackDetailDto` — updates status/response, notifies submitter |
| DELETE | `/admin/feedback/{id}` | → `204` (spam/test-entry cleanup — unlike Treatments this isn't a legal register, deletion is fine) |

Screenshot (in v1 per Asim's decision): `POST /feedback/{id}/screenshot` (multipart, only on the
submitter's own feedback, only while it has no screenshot yet) and
`GET /feedback/{id}/screenshot` (streamed, private-bucket pattern identical to
`GET /inspections/photos/{id}/file`; readable by the submitter **and** by SystemAdmin).

The screenshot is a **second request after the feedback row exists**, not a field on the create
payload. Reason: the create endpoint stays plain JSON (simple validation, simple client code), and the
upload gets the same shape as the already-working inspection-photo upload rather than a bespoke
mixed multipart+JSON endpoint. The client submits, then uploads if a file was picked; if the upload
fails, the feedback itself is already safely recorded and the user is told only the image failed.

### Notifications — two channels, deliberately split

Asim's decision (2026-07-30): **the email goes to one fixed address from configuration**, not to every
SystemAdmin's account address. That splits the two delivery channels, because the existing
`NotifyAsync` couples them (it always enqueues an email for the notified user):

- **In-app bell → every SystemAdmin.** Use the existing `NotificationService.NotifyManyInAppAsync`,
  which persists notifications **without** enqueuing emails — it exists precisely because
  broadcasting individual emails would be spam (SPEC-06's publish broadcast). Recipients come from a
  new `IUserRepository.GetSystemAdminIdsAsync()`. Type: `NotificationType.FeedbackSubmitted`.
- **Email → the single configured address.** One `_emailQueue.Enqueue(...)` per submission. Still goes
  through the background worker, so SMTP never touches the request path (ADR-021 holds).

`NotifyAsync` is therefore **not** used for the admin side — using it would email every SystemAdmin
*and* the fixed address, which is the option that was explicitly rejected.

**This requires a small change to the email layer**, because `QueuedEmail` today carries only a
`UserId` and `EmailNotificationWorker` always resolves the address via `Users.GetByIdAsync`:

- `QueuedEmail.UserId` becomes `int?`, and two optional fields are added: `ToEmail`, `ToName`.
  Existing positional call sites (three of them: two in `AuthService`, one in `NotificationService`)
  keep compiling — `int` converts implicitly to `int?`.
- Two static factories make the intent explicit at the call site: `QueuedEmail.ForUser(...)` and
  `QueuedEmail.ForAddress(...)`.
- `EmailNotificationWorker.SendAsync` prefers `ToEmail` when set, else resolves by `UserId`, else logs
  and skips. This is the *only* behavioural change to existing email delivery, and it is additive.

If `Feedback:NotifyEmail` is **not configured**, the email is skipped silently and logged — matching
`EmailService`'s existing behaviour when SMTP is missing. The feedback row and the in-app notifications
are unaffected. `appsettings.json` keeps an **empty placeholder**; the real address is an env var, since
the repo is public.

- **On status change or response** → `NotificationType.FeedbackStatusUpdated`, notifying the original
  submitter via `NotifyAsync` (bell **and** email — here the coupling is exactly what's wanted; a user
  whose report was answered should get a mail). Skipped silently if the user was since deleted.

### `ICurrentUser` — no changes needed

`ICurrentUser` doesn't expose `Email`, but nothing here needs it: the entity stores `UserId` (already
available today), and the admin DTO resolves the submitter's display name/email by loading the `User`
row (`_uow.Users.GetByIdAsync` or an `Include`) exactly the way `AdminService` and
`InspectionPhotoService` already do it for their own "who did this" displays. **`AuthService.cs`/JWT
claims stay untouched** — this is deliberate; that file is in `ignore.md`'s frozen list and there's a
precedented, already-used way to get a display name without going near it.

### Migration

Plain additive migration — one new table, no existing column changes, no backfill, no data risk.
Nothing like SPEC-12's hand-edited migration is needed here. `dotnet ef migrations add AddFeedback`
from `Melarium.Entity` is sufficient.

## Frontend

### Entry point

A "Pošalji povratnu informaciju" action added to the header profile dropdown in `Layout.tsx`, in the
same spot as the existing "Uredi profil" / "Paket i pretplata" actions — reachable from every
authenticated page, not tied to a specific route. Opens `FeedbackFormModal`, built on the existing
`Modal` component (no new dialog implementation).

### `FeedbackFormModal`

- React Hook Form (per ADR-007), modeled on `ApiaryFormPage.tsx`'s pattern — this is a simple flat
  form, not one of the dynamic multi-row forms (Harvest/Treatment) that had a reason to skip RHF.
- Fields: `Type` select (with a Bosnian label + icon per category), `Severity` select (rendered only
  when `Type` is Bug/Complaint), `Subject` input, `Message` textarea. No `PageContext`/`UserAgent`
  fields — captured invisibly at submit time via `window.location.pathname` and `navigator.userAgent`.
- On submit failure, follow the pattern the codebase actually relies on today (`ProfilePage.tsx`):
  `apiClient`'s interceptor flattens every backend error to a single `Error.message` string, so
  per-field FluentValidation-dictionary mapping does **not** survive to the component — use
  `setError('root', { message: e.message })` or a toast, not a field-by-field error dictionary.
- On success: toast "Hvala! Vaša povratna informacija je poslana." and close.

### "Moje povratne informacije"

A compact section on `ProfilePage` (not a new standalone route — it's "my account and my stuff",
same home as the existing password-change section) listing the user's own submissions with type icon,
subject, status badge, and the admin's response if one was left. Closes the loop without the user
needing to ask twice.

### Admin dashboard — `FeedbackAdminPage` (`/admin/feedback`)

Modeled directly on `LearningTopicsAdminPage.tsx`: hero header with a count summary, status-pill list
(reusing the exact `STATUS_STYLE` record pattern from `TreatmentsPage.tsx` — Novo/U razmatranju/
Riješeno/Odbijeno get their own badge colors), type filter + status filter, click a row to open the
detail view (submitter name/email/org, full message, page context, screenshot if any) with a status
dropdown + response textarea + save button.

- Route added under the existing `<Route element={<AdminRoute />}>` block in `App.tsx`, next to
  `admin/learning-topics`.
- Nav item added to `getNavItems()` in `Sidebar.tsx`, gated by `flags.isSystemAdmin` — label suggestion
  "Povratne informacije".
- An unread ("Novo") count badge on the nav item / bell, reusing `NotificationBell.tsx`'s exact
  badge markup (`absolute -top-0.5 -right-0.5 ... rounded-full bg-red-500 ...`).
- Follows the existing static-import convention for routes (no `React.lazy` — nothing else in the app
  route-splits either; see SPEC-14 for the one place this question comes up again).

### New files

- `core/models/index.ts` — new `// ── User Feedback (SPEC-13) ──` section: `FeedbackType`/
  `FeedbackTypeLabels`, `FeedbackSeverity`/`FeedbackSeverityLabels`, `FeedbackStatus`/
  `FeedbackStatusLabels`, `Feedback`, `AdminFeedback`, `CreateFeedbackPayload`,
  `UpdateFeedbackStatusPayload`.
- `core/services/feedbackService.ts` + `core/services/feedbackQueries.ts` — dedicated per-feature
  files, matching the dominant recent convention (`treatmentService.ts`/`treatmentQueries.ts`), not
  the older shared `queries.ts` that `CLAUDE.md`'s summary table is slightly stale about.
- `shared/components/FeedbackFormModal.tsx`
- `features/admin/FeedbackAdminPage.tsx`

## Edge cases

- User submits repeatedly in quick succession → rate-limited (429), same fixed-window mechanism as
  `auth-email` today.
- Message is empty or whitespace-only → rejected by validation (trim + min length), never reaches the
  admin inbox as noise.
- Submitting user is later deleted by SystemAdmin → the feedback row survives with `UserId = null`;
  admin still sees the original type/subject/message, just loses the "submitted by" attribution.
- Zero SystemAdmins exist at the moment of submission (shouldn't happen — bootstrap always provisions
  one) → notification loop does nothing; the feedback is still saved. Saving must never be
  conditional on notification succeeding — same "best-effort email" principle as ADR-021.
- A non-SystemAdmin calls `/api/admin/feedback` directly → 403, not a redirect (matches every other
  admin controller).
- A user requests someone else's `feedback/mine/{id}` → 404 (never reveal the row exists via 403).
- SystemAdmin deletes a feedback row the submitter is currently viewing → next fetch 404s cleanly on
  their side (they already saw it, low-stakes).
- Screenshot attached but fails validation (Phase C) → whole submission rejected with a clear message,
  not silently posted without the attachment (see Domain rules).

## Out of scope (v1)

Threaded/multi-message replies (single `AdminResponse` field only — see the model decision), anonymous
or logged-out feedback (a marketing "contact us" form for non-users is a different feature; this one is
explicitly about existing customers, per the brief), OrganizationAdmin visibility into their own
members' feedback (this is a platform-level concern between one user and SystemAdmin, not an
org-management feature — revisit only if it turns out to be genuinely wanted), CSV/export, SLA
tracking or auto-escalation on stale "New" items, in-app push (no push infra exists at all yet, per
`context.md`'s "Unspecced ideas").

## Phases

Both phases ship together; the split is only an implementation order.

- **Phase A — backend.** Entity + 3 enums, migration, repository, service, validators, both
  controllers, `GetSystemAdminIdsAsync`, `QueuedEmail`/worker fixed-address support, the two
  `NotificationType` values + `BsLabels`, screenshot upload/stream, rate-limit policy, config
  placeholder.
- **Phase B — frontend.** Models, `feedbackService`/`feedbackQueries`, `FeedbackFormModal` (incl.
  screenshot picker) + header entry point, "Moje povratne informacije" on `ProfilePage`,
  `FeedbackAdminPage` + nav item + route.

## Acceptance criteria

- [ ] Any authenticated user can submit feedback with a type, subject, and message from any page.
- [ ] Every SystemAdmin gets an **in-app** notification for every new submission, and **exactly one**
      email is sent, to `Feedback:NotifyEmail` — not one per SystemAdmin.
- [ ] With `Feedback:NotifyEmail` unset, submitting still succeeds and still notifies in-app; no
      exception, no failed request.
- [ ] The three existing `QueuedEmail` call sites (password reset, verification, notifications) still
      deliver to the user's own address — the `int?`/`ToEmail` change must not regress them.
- [ ] A screenshot can be attached; a non-image file (renamed `.exe`, PDF) is rejected on **header
      bytes**, not extension; >5 MB is rejected.
- [ ] `FeedbackAdminPage` lists all feedback newest-first, filterable by type/status, with a live
      "Novo" count.
- [ ] SystemAdmin can change status and optionally add a response; the submitter is notified back.
- [ ] A non-SystemAdmin calling any `/api/admin/feedback` route gets `403`.
- [ ] "Moje povratne informacije" shows only the current user's own submissions, never another user's.
- [ ] Deleting the submitting user leaves their feedback history intact (content, not attribution).
- [ ] Submit endpoint is rate-limited.
- [ ] All user-facing strings Bosnian; enum labels via `BsLabels` (backend) / label maps (frontend).
- [ ] Docs updated: `features/feedback.md` (new), `api-contracts.md`, `context.md`.

## Decisions taken (2026-07-30, Asim)

1. **Screenshot attachment** → **in v1**, not deferred.
2. **Email recipient** → **one fixed address from configuration** (`Feedback:NotifyEmail`), not each
   SystemAdmin's account address. Drove the `QueuedEmail`/worker change and the in-app/email split
   described under Notifications.
3. **Severity** → included (negligible cost).
4. **Two-way reply** → single `AdminResponse` field for v1; no threading.
5. **OrganizationAdmin visibility** → no. Flat user↔SystemAdmin channel.
6. **Anonymous/logged-out form** → out of scope; different audience.
7. **Delete permission** → SystemAdmin may delete (this is not a legal register, unlike Treatments).

## Still open

- **Rate limit number** — implemented as **3/min per IP**, reusing the same fixed-window shape and
  reasoning as the existing `auth-email` policy (each accepted request puts a message in an inbox).
  Say if you want it looser.
- **`Feedback:NotifyEmail` value** — must be set as an env var at deploy time
  (`Feedback__NotifyEmail=…`). Until it is set, submissions still work and SystemAdmins still get the
  bell notification, but **no email is sent**. This is the same silent-skip behaviour SMTP already has,
  and it is listed in the deploy checklist for that reason.
