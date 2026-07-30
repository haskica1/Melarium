# Feedback — Povratne informacije korisnika (SPEC-13)

> Implemented 2026-07-30. Spec: [`specs/SPEC-13-user-feedback.md`](../specs/SPEC-13-user-feedback.md).

## What it is

An in-app channel from any signed-in user to the platform operator: bug reports, complaints,
compliments, feature ideas and plain questions, with an optional screenshot. SystemAdmin triages them
on a dedicated page and can reply; the reply reaches the submitter as a notification and an e-mail.

## Model

`Feedback` (table `Feedbacks`) — one flat table, no children.

| Field | Notes |
|---|---|
| `UserId?`, `OrganizationId?` | Both `ON DELETE SET NULL` — deleting an account or organisation keeps the report, only the attribution is lost |
| `Type` | `FeedbackType`: Bug, Complaint, Compliment, FeatureRequest, Question, Other |
| `Severity?` | `FeedbackSeverity` Low/Medium/High/Critical — the UI only asks for it on Bug/Complaint |
| `Subject` (150), `Message` (2000) | Trimmed; min 3 / min 10 chars |
| `PageContext?`, `UserAgent?` | Captured client-side, never typed by the user. Debug hints only — never used for authorization |
| `ScreenshotStoragePath?`, `ScreenshotContentType?` | `IFileStorage` key; content type detected from header bytes |
| `Status` | `FeedbackStatus`: New → InReview → Resolved \| Dismissed. SystemAdmin-only |
| `AdminResponse?` (1000), `RespondedAt?`, `RespondedById?` | Single reply field, not a thread |

**Not tenant-scoped**, so it does **not** go through `IAccessGuard` — that exists for the
Organization → Apiary → Beehive hierarchy. Authorization here is the flat "SystemAdmin vs. the row's
own owner" split already used by the other `api/admin/*` controllers.

## Endpoints

`FeedbackController` — `/api/feedback`, `[Authorize]` (any role):

| Method | Path | Notes |
|---|---|---|
| POST | `/feedback` | Rate-limited `feedback` policy (3/min per IP) |
| GET | `/feedback/mine` | Own submissions, newest first |
| GET | `/feedback/mine/{id}` | **404** (not 403) for someone else's row — a 403 would confirm it exists |
| POST | `/feedback/{id}/screenshot` | multipart, own row, one screenshot only (422 if one exists) |
| GET | `/feedback/{id}/screenshot` | Streamed; submitter or SystemAdmin |

`FeedbackAdminController` — `/api/admin/feedback`, `[Authorize(Roles = Roles.SystemAdmin)]`:
`GET /` (filters `type`, `status`), `GET /summary` (badge count), `GET /{id}`,
`PUT /{id}/status`, `DELETE /{id}`.

## Notification: two channels, deliberately split

The operator e-mail goes to **one configured address**, not to every SystemAdmin's account address.
That splits the channels, because `NotificationService.NotifyAsync` couples them (it always enqueues
an e-mail for the notified user):

- **In-app bell → every SystemAdmin** via `NotifyManyInAppAsync` (persists notifications, sends no
  e-mail — the same method SPEC-06 uses so a publish broadcast isn't inbox spam). Recipients from
  `IUserRepository.GetSystemAdminIdsAsync()`. Type `FeedbackSubmitted` (21).
- **E-mail → `Feedback:NotifyEmail`**, one message per submission, still through the background queue
  so SMTP never touches the request path (ADR-021 holds).
- **On status change / reply → the submitter** via `NotifyAsync` (bell *and* e-mail — here the
  coupling is what's wanted). Type `FeedbackStatusUpdated` (22). Skipped if the account is gone.

Neither channel may fail the submission: the row is saved first, and both are wrapped so a
notification error is logged, not surfaced.

### Email-layer change this required

`QueuedEmail` gained a second addressing mode, because it previously carried only a `UserId` and the
worker always resolved the address from the account:

- `UserId` is now `int?`, plus optional `ToEmail` / `ToName`.
- Two factories make the intent visible: `QueuedEmail.ForUser(...)` and `QueuedEmail.ForAddress(...)`.
- `EmailNotificationWorker` prefers `ToEmail` when set, else resolves by `UserId`, else logs and skips.

All three pre-existing call sites (password reset, e-mail verification, notifications) were moved to
`ForUser` and are unchanged in behaviour.

## Configuration

```
Feedback:NotifyEmail        →  env var Feedback__NotifyEmail
```

`appsettings.json` keeps an **empty placeholder** (the repo is public). When unset, submissions still
succeed and SystemAdmins still get the bell notification — only the e-mail is skipped, and the skip is
logged. Same deliberate silent-skip behaviour `EmailService` already has for missing SMTP.

## Frontend

- **Entry point:** "Prijavi problem / pohvali" in the header profile dropdown and in the mobile menu —
  reachable from every authenticated page, not tied to a route.
- `FeedbackFormModal` (`shared/components/`) — built on the shared `Modal`; React Hook Form.
  Type is picked from six labelled buttons (each with a one-line hint) rather than a `<select>`, so the
  right category is obvious. `PageContext`/`UserAgent` are captured invisibly at submit.
- `MyFeedbackSection` on `ProfilePage` — the submitter's own history with the admin's reply.
- `FeedbackAdminPage` (`/admin/feedback`, under `AdminRoute`) — list + type/status filters + detail
  modal with status dropdown and reply box. Nav item "Povratne informacije" (SystemAdmin only) with an
  untriaged-count badge.
- `feedbackService.ts` + `feedbackQueries.ts`, matching the per-feature convention.

**Screenshots are fetched as a blob through `apiClient`** and rendered from an object URL — an
`<img src>` cannot carry the Bearer header. This mirrors `AuthImage` in `InspectionPhotos`.

**Loading/empty/error branches use `isPending` / `isSuccess`, not `isLoading`.** In React Query v5
`isLoading` is `isPending && isFetching`, so during the pause *between* retries neither `isLoading` nor
`isError` is true while `data` is still undefined — which rendered "Nema prijava." on a failed request.
`isSuccess` gates the empty state so a failure can never read as "no data".
