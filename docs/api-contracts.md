# API Contracts

## Base URL

- Dev: `https://localhost:62647/api`
- Frontend proxy: `/api` → backend (configured in `vite.config.ts`)

## Authentication

All endpoints require `Authorization: Bearer <jwt>` except
`POST /api/auth/{login, register, refresh, logout}` and `GET /api/beehives/scan/{uniqueId}`.

**Endpoints:**

| Method | Endpoint | Notes |
|---|---|---|
| POST | `/auth/login` | 5/min per IP; returns access + refresh token |
| POST | `/auth/register` | 5/min per IP; creates a new organization + OrganizationAdmin |
| POST | `/auth/refresh` | 20/min per IP; rotates the refresh token (reuse revokes the whole set) |
| POST | `/auth/logout` | revokes the presented refresh token (idempotent) |

**Login request:** `{ identifier, password }` — `identifier` is an **email address or a phone
number**; the server picks the lookup by whether it contains an `@`. Phone numbers are matched
after normalisation to E.164, so any way of writing the same number works. `{ email, password }`
is still accepted as a legacy alias for `identifier` (clients cached before the rename).

**Register request:** `{ firstName, lastName, email, phone, password, organizationName,
organizationDescription? }` — `phone` is **required** and must be a parseable number
(bare numbers are assumed BiH, `+387`). Both a duplicate email and a duplicate phone return
`422` with a Bosnian message naming the field that clashed.

**JWT Claims:**

| Claim | Key | Description |
|---|---|---|
| User ID | `sub` | int |
| Email | `email` | string |
| Role | `role` | `SystemAdmin`, `OrganizationAdmin`, `ApiaryAdmin`, or `Beekeeper` |
| Token ID | `jti` | Guid |
| Organization | `organizationId` | int, absent for SystemAdmin |
| Apiary | `apiaryId` | int, only for ApiaryAdmin |

**Token lifetime:** access token 30 minutes; refresh token 14 days (rotating, stored hashed).

---

## Standard Response Format

### Success
HTTP `200 OK`, `201 Created`, `204 No Content` — response body is the DTO directly (not wrapped).

### Error — Problem Details (RFC 7807)

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Not Found",
  "status": 404,
  "detail": "Apiary with id '...' was not found."
}
```

| Exception | HTTP Status |
|---|---|
| `NotFoundException` | 404 |
| `ValidationException` | 400 (includes `errors` map) |
| `BusinessRuleException` | 422 |
| `AiUnavailableException` | 503 — upstream AI provider rate-limited us, was down, or timed out; `detail` is a Bosnian instruction the user can act on (ADR-036) |
| Unhandled | 500 |

---

## Endpoints

### Auth

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/auth/login` | Public | Returns token + user |

**Login request:** `{ identifier, password }` — see the Authentication section above.
**Login response:** `{ token, userId, email, role, organizationId }`

---

### Apiaries

| Method | Path | Returns |
|---|---|---|
| GET | `/apiaries` | `ApiaryDto[]` |
| GET | `/apiaries/{id}` | `ApiaryDetailDto` (includes beehives) |
| POST | `/apiaries` | `201 + ApiaryDto` |
| PUT | `/apiaries/{id}` | `200 + ApiaryDto` |
| DELETE | `/apiaries/{id}` | `204` |
| GET | `/apiaries/{id}/weather` | `WeatherForecastDto` |

**ApiaryDto:** `{ id, name, location, latitude, longitude, organizationId, createdAt }`
**ApiaryDetailDto:** extends ApiaryDto + `beehives: BeehiveDto[]`

---

### Beehives

| Method | Path | Returns |
|---|---|---|
| GET | `/beehives/by-apiary/{apiaryId}` | `BeehiveDto[]` — **excludes merged-away hives** (SPEC-19) |
| GET | `/beehives/merged?apiaryId=` | `BeehiveDto[]` — the archive; the only list that returns them |
| GET | `/beehives/{id}` | `BeehiveDetailDto` (includes inspections; finds merged hives too) |
| POST | `/beehives` | `201 + BeehiveDto` |
| PUT | `/beehives/{id}` | `200 + BeehiveDto` |
| DELETE | `/beehives/{id}` | `204` |

**BeehiveDto:** `{ id, name, type, material, apiaryId, qrCode (Base64 PNG), uniqueId (Guid), createdAt,
mergedIntoBeehiveId?, mergedIntoBeehiveName?, mergedAt? }`
**BeehiveDetailDto:** extends BeehiveDto + `inspections: InspectionDto[]`, `mergeId?`, `canUndoUntil?`

---

### Colony merges — sastavljanje društava (SPEC-19)

| Method | Path | Returns |
|---|---|---|
| POST | `/beehive-merges` | `201 + BeehiveMergeDto` |
| POST | `/beehive-merges/{id}/undo` | `200 + BeehiveMergeDto` — 400 after 24 h or if already undone |
| GET | `/beehive-merges/by-beehive/{beehiveId}` | `BeehiveMergeDto[]` — merges this hive **received** |
| GET | `/beehive-merges/preview?sourceBeehiveId=&targetBeehiveId=` | `MergePreviewDto` |

**POST body:** `{ sourceBeehiveId, targetBeehiveId, mergedAt, reason, method, queenOutcome, notes? }`
`reason`, `method` and `queenOutcome` are the **numeric** enum values (see Enum Reference).
The **source** hive is the one that leaves the apiary. 400 on: same hive twice, either hive already
merged, hives in different organizations, a future date, or `queenOutcome = KeptSource` on a queenless
source. 403 unless the caller can manage **both** apiaries.

**BeehiveMergeDto:** `{ id, sourceBeehiveId, sourceBeehiveName, sourceApiaryId, targetBeehiveId,
targetBeehiveName, targetApiaryId, mergedAt, reason, reasonName, method, methodName, queenOutcome,
queenOutcomeName, notes?, createdByName?, createdAt, canUndoUntil?, undoneAt? }`
`canUndoUntil` is computed server-side from `createdAt + 24 h`; the client never derives it.

**MergePreviewDto:** `{ beehiveId, beehiveName, apiaryName, openTodoCount, activeDietNames[],
ongoingTreatmentNames[], sourceQueenSummary?, targetQueenSummary?, karencaUntil?, karencaProductName? }`

---

### Inspections

| Method | Path | Returns |
|---|---|---|
| GET | `/inspections/by-beehive/{beehiveId}` | `InspectionDto[]` (newest first) |
| GET | `/inspections/{id}` | `InspectionDto` |
| POST | `/inspections` | `201 + InspectionDto` |
| PUT | `/inspections/{id}` | `200 + InspectionDto` |
| DELETE | `/inspections/{id}` | `204` — also deletes attached photo blobs (best-effort) |
| POST | `/inspections/parse-voice` | `200 + ParseVoiceResult` — multipart (`audio`), 15 MB cap, `voice-parse` 10/min/IP, paid-plan feature (`402 plan-limit`); Whisper transcription + field extraction. `503` when Groq is rate-limiting, down or slow (ADR-036) |

**InspectionDto:** `{ id, beehiveId, date, temperature, honeyLevel, broodStatus, notes, createdAt }`

**ParseVoiceResult:** `{ date?, temperature?, honeyLevel?, broodStatus?, notes?, transcript }` — every field is null unless the recording mentioned it. Client budget is 120 s (two sequential Groq calls plus the upload).

#### Inspection photos (SPEC-05)

| Method | Path | Returns |
|---|---|---|
| POST | `/inspections/{id}/photos` | `201 + InspectionPhotoDto` — multipart (`file` + optional `caption`); max 5/inspection, 8 MB, JPEG/PNG/WebP validated by header bytes → `422` with Bosnian message otherwise |
| GET | `/inspections/{id}/photos` | `InspectionPhotoDto[]` (no image bytes) |
| GET | `/inspections/photos/{photoId}/file` | image stream, `Cache-Control: private, max-age=86400` — auth-checked, storage never public |
| DELETE | `/inspections/photos/{photoId}` | `204` — deletes row + blob (blob best-effort) |
| POST | `/inspections/photos/{photoId}/analyze` | `200 + InspectionPhotoDto` (fresh `analysisJson`) — Groq vision, rate-limited `photo-analyze` 5/min/IP; `422` for >~3 MB images (Groq 4 MB base64 request cap) or unparseable model output |

**InspectionPhotoDto:** `{ id, inspectionId, contentType, sizeBytes, caption?, analysisJson?, createdAt }`

**analysisJson (parsed):** `{ isFramePhoto, broodPattern (1–5|null), queenCellsVisible?, anomalies: string[], summary? }` — Bosnian, observations only (never diagnoses). Access to all photo endpoints mirrors the parent inspection (`IAccessGuard`).

---

### Moja organizacija (SPEC-22)

The caller's **own** organization. There is no `{id}` anywhere in this group: the organization is
resolved from `ICurrentUser.OrganizationId`, so a member cannot address another tenant's row.
Reading is open to every member of the organization, writing is `OrganizationAdmin` only. An org-less
SystemAdmin gets **403** (not 404) — the row exists for everyone else, they are simply not in one.

| Method | Path | Returns |
|---|---|---|
| GET | `/organizations/my` | `MyOrganizationDto` |
| PUT | `/organizations/my` | `200 + MyOrganizationDto` — body `{ name, description? }`; OrgAdmin only |
| POST | `/organizations/my/logo` | `200 + MyOrganizationDto` — multipart `file`, ≤ 2 MB, JPEG/PNG/WebP by header bytes; OrgAdmin only |
| GET | `/organizations/my/logo` | image stream, `Cache-Control: private, no-cache` — auth-checked, storage never public |
| DELETE | `/organizations/my/logo` | `200 + MyOrganizationDto` — clears both columns, deletes the blob (best-effort); OrgAdmin only |
| GET | `/admin/organizations/{id}/logo` | image stream — SystemAdmin only, for the system table |

**MyOrganizationDto:** `{ id, name, description?, hasLogo, createdAt, userCount, apiaryCount, beehiveCount }`
— deliberately **not** `AdminOrganizationDto`: plan, payment note and activity belong to the SystemAdmin
screens, not to a tenant looking at itself. The logo is never inlined; `hasLogo` says whether the stream
endpoint has anything to give.

The logo cache header differs from the inspection photo's `max-age=86400` on purpose: this URL never
changes, so a replaced logo would keep serving the old image for a day. Uploading replaces any previous
logo and deletes its blob **after** the new key is committed. A row pointing at a missing blob answers
`404`, not `500`.

Renaming does **not** revoke sessions — the organization name is not a JWT claim (`organizationId` is,
and it does not change).

---

### Account deletion & ownership transfer

Deleting your **own** account, and the handover that has to happen first when you are the
administrator of an organization that still has members. Both are ordinary app features; they became
a priority because the app stores require in-app account deletion (Apple 5.1.1(v), Google Play).

| Method | Path | Returns |
|---|---|---|
| GET | `/profile/deletion-preview` | `AccountDeletionPreviewDto` — what deleting this account would do |
| DELETE | `/profile` | `204` — body `{ password, organizationNameConfirmation? }` |
| POST | `/org/transfer-ownership` | `204` — body `{ memberId }`; OrgAdmin only |

**AccountDeletionPreviewDto:** `{ mode, organizationName?, memberCount, apiaryCount, beehiveCount,
deletesTreatmentRegister }`. `mode` is one of:

| `mode` | Meaning | What the dialog must ask for |
|---|---|---|
| `account` | Only this account and its personal records go | password |
| `organization` | Caller is the last member **and** the OrgAdmin — the organization and everything in it goes too | password **and** the organization's exact name |
| `transfer-required` | Caller is the OrgAdmin of an organization that still has members | nothing — deletion is refused until they hand over |

The rule lives in **one** private helper in `ProfileService`, which both the preview and the deletion
call. A preview that said `account` while the deletion decided `organization` would destroy an
organization nobody was warned about.

`DELETE /profile` carries a **body** (the password). That is deliberate: the alternative, a password
in the query string, would put a credential in server logs and browser history. The password is
re-typed rather than taken from the session so an unlocked phone is not two taps from deleting the
account. A wrong password is **422**, the same shape and cost as any other refusal.

Refusals are all `BusinessRuleException` → **422**, not 409: wrong password, an OrgAdmin who still
has members, a mistyped organization name, and the last SystemAdmin trying to delete themselves.

**What deletion removes:** the user row, and by cascade their sessions, notifications, read markers,
AI history, calendar settings and hive assignments. **What survives, anonymised** (`SetNull`, already
the schema's design before this feature existed): feedback, invitations, and the `CreatedBy` of
apiaries, hives, harvests, treatments, expenses, diets, moves and merges. Beekeeping records have no
foreign key to a user at all — they hang off the organization.

Assigned todos are released (`Todo.AssignedToId = null`) **before** the delete: that FK is the one
configured `NoAction`, so PostgreSQL refuses the delete outright rather than clearing it. The same
crash still exists in `AdminService.DeleteUserAsync` and is not fixed here.

When the organization goes too, the user and the organization are removed in **one** `SaveChanges`.
`Organization.Users` is `Restrict` and EF deletes dependents before principals, so the ordering is
already correct; two separate saves could leave a deleted user beside an organization nobody can
reach. Deleting the organization cascades into its apiaries and therefore into the **treatment
register** — which is why `deletesTreatmentRegister` exists and why the dialog says so out loud.

**Transfer of ownership** exists because an organization has exactly one `OrganizationAdmin` and no
other way to appoint one. The successor becomes `OrganizationAdmin` with `ApiaryId` cleared; the
caller steps down to `Beekeeper` (not `ApiaryAdmin` — that role must be tied to an apiary, and there
is no honest way to pick one). Both sessions are revoked, the caller's included, because role is a
JWT claim. A `memberId` from another organization answers **404**, not 403, so the endpoint cannot be
used to ask whether a user id exists.

---

### Plans & billing (SPEC-09)

| Method | Path | Returns |
|---|---|---|
| GET | `/organizations/my-plan` | `MyPlanDto` — any authenticated org member; org-less SystemAdmin → `404` |
| PUT | `/admin/organizations/{id}/plan` | `200 + AdminOrganizationDto` — SystemAdmin only; body `{ plan, planValidUntil?, planNotes? }`; accepts all five plans incl. Partner |

**MyPlanDto:** `{ plan, planName, effectivePlan, effectivePlanName, planValidUntil?, planNotes?, usage: { apiaries, apiariesLimit?, beehives, beehivesLimit?, members, membersLimit?, aiInteractionsThisMonth, aiInteractionsLimit? } }` — a null limit means unlimited for the effective plan. `aiInteractionsThisMonth`/`Limit` (SPEC-18) cover both AI questions and commands under one combined quota — previously two separate fields for the advisor and the assistant.

**PlanType:** `Free=1, Standard=2, Pro=3, Max=4, Partner=5` (Partner is hidden from public UI).

**Plan-limit error:** exceeding a plan limit returns **402 Payment Required** with a top-level
`code: "plan-limit"` and `errors.detail[0]` = Bosnian message (distinct from 403 so the frontend
renders an upsell). Gated actions: create apiary/beehive/member, voice parse, AI assistant interaction
(question or command, + monthly quota on Standard), pasture/move create, photo AI analysis.
`AdminOrganizationDto` gained `plan`/`planName`/`planValidUntil`/`planNotes`.

---

### Queens

| Method | Path | Returns |
|---|---|---|
| GET | `/beehives/{beehiveId}/queens` | `QueenDto[]` (newest introduction first) |
| POST | `/beehives/{beehiveId}/queens` | `201 + QueenDto` — new queen is Active; existing active queen auto-closed as Replaced (atomic) |
| PUT | `/queens/{id}` | `200 + QueenDto` — `422` when activating while another queen is active; records one `QueenEditLog` row per changed field |
| GET | `/queens/{id}/history` | `QueenEditLogDto[]` (newest first) — field-level edit log for that queen record |
| DELETE | `/queens/{id}` | `204` |

**QueenDto:** `{ id, beehiveId, year, markColor, markColorName, isMarked, isClipped, origin, originName, status, statusName, introducedDate, endDate?, notes?, createdAt }`

**QueenEditLogDto:** `{ id, fieldLabel, oldValue?, newValue?, editedAt, editedByName? }`

**Create body:** `{ year, markColor?, isMarked, isClipped, origin, introducedDate, notes? }` — `markColor` omitted → derived from `year` (international color code).

---

### Harvests

| Method | Path | Returns |
|---|---|---|
| GET | `/harvests?apiaryId=&year=` | `HarvestDto[]` (role-scoped; incl. `totalKg`, `entryCount`, `apiaryName`, `estimatedRevenue`) |
| GET | `/harvests/{id}` | `HarvestDetailDto` (entries with hive names) |
| POST | `/harvests` | `201 + HarvestDetailDto` |
| PUT | `/harvests/{id}` | `200 + HarvestDetailDto` (apiary immutable — not in body) |
| DELETE | `/harvests/{id}` | `204` |
| GET | `/harvests/hive/{beehiveId}/yield` | `HiveYieldDto` `{ currentSeasonKg, byYear:[{year, kg}] }` |

**Create body:** `{ apiaryId, date, honeyType, pricePerKg?, notes?, entries:[{beehiveId, quantityKg, framesExtracted?}] }`
**Access:** apiary-scoped (like apiary management); managers write, **Beekeeper read-only** for harvests
containing an assigned hive. Foreign/duplicate hive in `entries` → `400`.
`GET /api/stats` gains: `seasonTotalKg`, `estimatedRevenue`, `kgByApiary[]`, `kgByHoneyType[]`,
`topHivesByYield[]`, `yearlyYield[]`.

---

### Treatments (Evidencija tretmana)

| Method | Path | Returns |
|---|---|---|
| GET | `/treatments?apiaryId=&beehiveId=&year=` | `TreatmentDto[]` (role-scoped; incl. computed `karencaUntil`, `status`/`statusName`, `hiveCount`, `hiveNames`) |
| GET | `/treatments/{id}` | `TreatmentDetailDto` (entries with hive names + `doseNote`, `createdByName`) |
| POST | `/treatments` | `201 + TreatmentDetailDto` |
| PUT | `/treatments/{id}` | `200 + TreatmentDetailDto` (apiary immutable — not in body) |
| DELETE | `/treatments/{id}` | `204` |

**Create body:** `{ apiaryId, purpose, productName, activeSubstance, method, dosePerHive, startDate,
endDate?, withdrawalDays, batchNumber?, supplier?, notes?, entries:[{beehiveId, doseNote?}] }`
**Access:** apiary-scoped (same matrix as Harvests); managers write, **Beekeeper read-only** for
treatments containing an assigned hive. Foreign/duplicate hive in `entries` → `400`; `entries` non-empty;
`withdrawalDays` 0–365. Status derived: no `endDate` → U toku; `today ≤ karencaUntil` → Karenca; else Završen.
The PDF register is generated client-side (jsPDF) — no PDF endpoint.

---

### Learning topics (Edukacija)

| Method | Path | Returns |
|---|---|---|
| GET | `/learning-topics?category=&month=` | `LearningTopicSummaryDto[]` — **published only**, incl. per-caller `isRead` (one grouped query) |
| GET | `/learning-topics/{id}` | `LearningTopicDetailDto` (published only, incl. `bodyMarkdown`) |
| POST | `/learning-topics/{id}/read` | `204` — idempotent read marker for the current user |

**Authoring (SystemAdmin only, `/api/admin/learning-topics`):**

| Method | Path | Returns |
|---|---|---|
| GET | `/admin/learning-topics` | `AdminLearningTopicDto[]` (incl. unpublished drafts) |
| GET | `/admin/learning-topics/{id}` | `AdminLearningTopicDto` |
| POST | `/admin/learning-topics` | `201 + AdminLearningTopicDto` (created as draft) |
| PUT | `/admin/learning-topics/{id}` | `200 + AdminLearningTopicDto` |
| DELETE | `/admin/learning-topics/{id}` | `204` (cascades read markers) |
| PUT | `/admin/learning-topics/{id}/publish` | `{ isPublished }` → `200`; publish requires non-empty body; **first** publish broadcasts one in-app notification per user (`LearningTopicPublished`, no email) |
| POST | `/admin/learning-topics/generate-draft` | `{ title, outline? }` → `{ bodyMarkdown, summary }` (Groq; `ai-chat` rate limit; never publishes) |

**Save body:** `{ title, category, months?: int[]|null, summary, bodyMarkdown }` — `months` 1–12,
null/empty = evergreen; drafts may have an empty body.

---

### Announcements (Šta je novo)

| Method | Path | Returns |
|---|---|---|
| GET | `/announcements?type=` | `AnnouncementListDto` = `{ items, unreadCount }` — **published only**, newest first by `publishedAt`, incl. per-caller `isRead` (one grouped query) |
| GET | `/announcements/banner` | `AnnouncementBannerDto` = `{ announcement, unreadCount }` — the **newest published** announcement with its `bodyMarkdown`, or `announcement: null` when there is none or the caller has already seen it. Called on every page; feeds both the banner and the nav badge in one request |
| GET | `/announcements/{id}` | `AnnouncementDetailDto` (published only, incl. `bodyMarkdown`) |
| POST | `/announcements/{id}/read` | `204` — idempotent "seen" marker for the current user. Set both by dismissing the banner and by closing the modal (SPEC-21 D2) |

No plan or role targeting — every authenticated user sees every published announcement (D5).

**Authoring (SystemAdmin only, `/api/admin/announcements`):**

| Method | Path | Returns |
|---|---|---|
| GET | `/admin/announcements` | `AdminAnnouncementDto[]` (incl. unpublished drafts) |
| GET | `/admin/announcements/{id}` | `AdminAnnouncementDto` |
| POST | `/admin/announcements` | `201 + AdminAnnouncementDto` (created as draft) |
| PUT | `/admin/announcements/{id}` | `200 + AdminAnnouncementDto` — does **not** clear seen markers, so an edit never resurrects a dismissed banner (D9) |
| DELETE | `/admin/announcements/{id}` | `204` (cascades seen markers) |
| PUT | `/admin/announcements/{id}/publish` | `{ isPublished }` → `200`; publish requires a non-empty body; **first** publish stamps `PublishedAt`. Sends **nothing** — no e-mail, no bell notification (D4) |

**Save body:** `{ title, type, bodyMarkdown }` — `type` is `1 New | 2 Improvement | 3 Fix`; drafts
may have an empty body.

---

### Pastures & apiary moves (Pašnjaci i selidbe)

| Method | Path | Returns |
|---|---|---|
| GET | `/pastures` | `PastureDto[]` (org-scoped; incl. `apiariesOnPasture`) — all roles |
| POST | `/pastures` | `201 + PastureDto` — OrgAdmin/SystemAdmin |
| PUT | `/pastures/{id}` | `200 + PastureDto` — OrgAdmin/SystemAdmin |
| DELETE | `/pastures/{id}` | `204`; `400` while any apiary sits on it or any move references it |
| GET | `/apiaries/{id}/moves` | `ApiaryMoveDto[]` newest first (apiary-view access) |
| POST | `/apiaries/{id}/moves` | `{ toPastureId, movedAt, certificateNumber?, notes? }` → `201`; sets the apiary's current pasture + coordinate snapshot — OrgAdmin/SystemAdmin |
| DELETE | `/apiaries/{id}/moves/{moveId}` | `204` — latest move only (else `400`); reverts pasture + coordinates |

**Rules:** `fromPastureId` is server-resolved (null = matična lokacija); move to the current
pasture or a foreign-org pasture → `400`; `movedAt` max +1 day. `GET /api/stats` gains
`kgByPasture[]` (current year, "Matična lokacija" bucket; empty without moves).

---

### Todos

| Method | Path | Returns |
|---|---|---|
| GET | `/todos/by-apiary/{apiaryId}` | `TodoDto[]` |
| GET | `/todos/by-beehive/{beehiveId}` | `TodoDto[]` |
| GET | `/todos/{id}` | `TodoDto` |
| POST | `/todos` | `201 + TodoDto` |
| PUT | `/todos/{id}` | `200 + TodoDto` |
| DELETE | `/todos/{id}` | `204` |

**TodoDto:** `{ id, title, description, priority, dueDate, isCompleted, apiaryId?, beehiveId? }`

---

### Diets

| Method | Path | Returns |
|---|---|---|
| GET | `/diets/by-beehive/{beehiveId}` | `DietDto[]` |
| GET | `/diets/{id}` | `DietDetailDto` (includes feeding entries) |
| POST | `/diets` | `201 + DietDto` |
| PUT | `/diets/{id}` | `200 + DietDto` |
| DELETE | `/diets/{id}` | `204` |
| POST | `/diets/{id}/complete-early` | `200` — requires `{ comment }` |
| POST | `/diets/{id}/entries/{entryId}/complete` | `200` |

**DietDto:** `{ id, beehiveId, startDate, reason, duration, frequency, foodType, status, createdAt }`
**DietDetailDto:** extends DietDto + `feedingEntries: FeedingEntryDto[]`

---

### Admin (SystemAdmin only)

| Method | Path | Description |
|---|---|---|
| GET | `/admin/organizations` | List all orgs with details |
| GET | `/admin/organizations/{id}` | Org detail |
| POST | `/admin/organizations` | Create org |
| PUT | `/admin/organizations/{id}` | Update org |
| DELETE | `/admin/organizations/{id}` | Delete org |
| GET | `/admin/users` | List all users with org |
| POST | `/admin/users` | Create user — `phone` **required** |
| PUT | `/admin/users/{id}` | Update user — `phone` optional; blank leaves it unchanged |
| DELETE | `/admin/users/{id}` | Delete user |
| GET | `/admin/organizations/{id}/logo` | Organization logo stream (SPEC-22); `404` when it has none |

**AdminOrganizationDto** additionally carries `beehiveCount` and `lastActivityAt` (nullable). Both are
**derived, never stored**: the hive count resolves through the owning apiary, and `lastActivityAt` is the
newest create-or-update across everything the organization owns (apiary, hive, inspection, queen, diet +
feeding rounds, treatment + rounds, harvest, expense, pasture, member, apiary/hive todo) plus refresh-token
issue, i.e. sign-in and session refresh. `null` = no sign of life beyond the row's own creation. Every
endpoint in the table above that returns an `AdminOrganizationDto` fills both, so the two fields never
carry a placeholder zero. See `features/org-activity.md`.

**Owner contact and logo on `AdminOrganizationDto` (SPEC-22):** `ownerName`, `ownerEmail`, `ownerPhone`,
`orgAdminCount`, `hasLogo`. The owner is the organization's **OrganizationAdmin**, not `createdByName` —
for an organization the SystemAdmin created, `CreatedById` is the SystemAdmin. Resolution order: the
OrgAdmin whose id equals `CreatedById`, else the longest-standing OrgAdmin, else `null` (an organization
with no admin at all). Derived from the already-included `Users` collection — no extra query.

**Account signals on `AdminUserDto` (SPEC-22):** `emailVerifiedAt` (null = never confirmed; soft, never
blocks sign-in) and `lastLoginAt` = `MAX(RefreshToken.CreatedAt)` for that user, null when they have
never signed in. A token row is written on sign-in **and** on every refresh, so it reads as "last time
this account was used"; nothing prunes the table, so the maximum covers the whole history. Same source
as `lastActivityAt` above — see ADR-040.

**Phone on user payloads.** Every endpoint that creates an account (`/auth/register`,
`/admin/users`, org member creation) requires a `phone`; every endpoint that updates one
(`/admin/users/{id}`, `/profile`) takes it optionally, where **blank means "leave the stored number
unchanged"** — it never clears it. A number held by another account returns `422` with
`"Korisnik s ovim brojem telefona već postoji."`; re-submitting your own (in any notation) is not a
conflict. `AdminUserDto`, `OrgMemberDto` and the profile response all return `phone` (null for
accounts created before the field existed).

---

### AI Assistant (SPEC-17 + SPEC-18)

Merged in SPEC-18 from a formerly separate `/api/advisor` — that endpoint, and the `AdvisorConversation`/
`AdvisorMessage` data behind it, no longer exist in the running app (old conversation history was
migrated into the shape below; see `deploy/data-migration/advisor-merge/`).

| Method | Path | Returns |
|---|---|---|
| GET | `/assistant/sessions` | `AssistantSessionSummary[]` (own only, newest activity first) — each carries `beehiveId?`/`beehiveName?` |
| GET | `/assistant/sessions/{id}` | `AssistantSessionDetail` (turns + actions; 404 if not owner) |
| POST | `/assistant/sessions` | `201 + AssistantSessionDetail` — `{ text, transcript?, apiaryId?, beehiveId? }`; `ai-chat` 10/min |
| POST | `/assistant/sessions/{id}/turns` | `200 + AssistantSessionDetail`; `ai-chat` 10/min |
| POST | `/assistant/turns/{id}/confirm` | `200 + { message, results[] }` — `{ actions: [{ id, apiaryId?, beehiveId?, fields }] }` |
| POST | `/assistant/turns/{id}/reject` | `204` |
| POST | `/assistant/transcribe` | `{ transcript }` — multipart audio, 15 MB cap, `voice-parse` policy |
| DELETE | `/assistant/sessions/{id}` | `204` (owner only) |

**A turn with an empty `actions` array and a full `reply` is a question's answer, not an unfinished
proposal (SPEC-18).** There is no separate "ask a question" endpoint or flag — the same `POST /sessions`
and `POST /sessions/{id}/turns` calls handle both; the model's own envelope decides which this turn was.
When `beehiveId` resolves to an accessible hive, the answer is grounded in that hive's real data
(inspections, diet, todos, queen, yield, latest treatment, weather) the same way a command's target
resolution uses it — the session remembers the hive it was first bound to, so later turns in the same
session stay grounded even without repeating `beehiveId`.

**Proposals are not writes.** `POST /sessions` only stores `Pending` actions; records are created on
`confirm`, which returns a result **per action** — a batch can partly fail and the response says so.

**The confirm body is untrusted.** The card is editable, so ids and fields are re-resolved against the
caller's accessible apiaries/hives and re-validated with the same validators the forms use. Confirming a
turn whose actions have already left `Pending` → `422` ("Ovaj prijedlog je već obrađen"), which is what
stops a double-tap from duplicating records.

`apiaryId`/`beehiveId` on the request are the page the user is on; they fill gaps only, and an explicitly
spoken name always wins. Text length 1–4000; 40 turns/session cap → `422`. AI outage → `422` and
**nothing persisted**. Plan-gated (Standard+, **one combined** monthly quota covering questions and
commands alike — `Plans:{Plan}:AiInteractionsPerMonth`, checked before the Groq call since a turn's kind
is only known after it returns) → `402` with `code: "plan-limit"`. Reuses `Groq:ApiKey`; model from
`Groq:ChatModel` (see `GroqModels`) — the same id every Groq text feature uses.

**Clarification.** Each turn in `AssistantSessionDetail.turns[]` carries a `candidates: [{
label, text }][]` array — non-empty only on the **latest** assistant turn, and only when the resolver
had something specific to offer (capped at 8, apiary/hive/todo/inspection ambiguity alike). Tapping a
candidate is not a distinct endpoint: the client sends `candidates[i].text` to
`POST /sessions/{id}/turns` exactly as if it had been typed.

**Update, complete and delete.** `action.kind` also takes `UpdateTodo`, `CompleteTodo`,
`UpdateInspection`, `DeleteTodo`, `DeleteInspection` — each targets an **existing** record the resolver
found by title (todo) or date (inspection), never a new one. Two fields distinguish these from a create
action on the wire:

- `isDestructive: bool` — `true` for `UpdateTodo`/`UpdateInspection`/`DeleteTodo`/`DeleteInspection`,
  `false` for every create kind **and** for `CompleteTodo` (a one-tap, reversible toggle, not held to
  the same bar as overwriting or destroying a record). The client is expected to require an extra,
  separate confirmation step before calling `/confirm` for a batch containing a `true`.
- `previousFields` — the existing record's current values (same shape as `fields`), so the client can
  show "prije → poslije" without a second request.

For these kinds, `apiaryId`/`beehiveId` on a `POST /turns/{id}/confirm` item are **ignored** — the
target is the one the resolver found at propose time, not something the client re-picks. Only `fields`
flows through, and only the fields actually present are applied; the executor keeps everything else
from the current record (`null` in the request means "do not change this").

---

### Invitations — "Pozovi prijatelja" (SPEC-15, Phase 1)

Any authenticated role, including a member of someone else's organization — the invitee always gets
their **own** organization, so this touches neither the inviter's data nor their seats:

| Method | Path | Returns |
|---|---|---|
| GET | `/invites/summary` | `InvitationSummaryDto` — `{ sentCount, acceptedCount, rewardDaysEarned, rewardDaysRemaining, shareUrl, inviteeTrialDays, rewardDaysPerInvite }`. **Mints the caller's referral code on first call**, so it is a GET with a side effect on first use only (same lazy model as the calendar feed token) |
| GET | `/invites/mine` | `InvitationDto[]` — own rows, newest first. `statusName` carries **three** labels from two stored states; "Registrovao se — čeka potvrdu" is a registered invitee who has not verified yet |

Anonymous — the visitor has no account yet:

| Method | Path | Returns |
|---|---|---|
| GET | `/invites/ref/{code}` | `{ inviterFirstName, trialDays }` or **404**. `auth-token` policy 10/min. **First name only** — never a surname, never an address. Not an enumeration risk: a referral code identifies an invitation, not an account |

`POST /auth/register` gains an optional trailing **`referralCode`**. An unknown, expired or malformed
value is **ignored, never rejected** — registration always succeeds and falls back to the standard
30-day trial. A recognised one (by code, or by an address we had already invited) yields the longer
`Invitations:InviteeTrialDays` trial instead.

Sending invitations by e-mail (`POST /invites`) is **Phase 2** and does not exist yet.

---

### Feedback (SPEC-13)

Any authenticated role — `/api/feedback`:

| Method | Path | Returns |
|---|---|---|
| POST | `/feedback` | `201 + FeedbackDto` — `{ type, severity?, subject, message, pageContext?, userAgent? }`; `feedback` policy 3/min |
| GET | `/feedback/mine` | `FeedbackDto[]` (own only, newest first) |
| GET | `/feedback/mine/{id}` | `FeedbackDto` — **404** for another user's row, never 403 |
| POST | `/feedback/{id}/screenshot` | `200 + FeedbackDto` — multipart `file`, own row, 5 MB cap, JPEG/PNG/WebP by **header bytes**; `422` if one is already attached |
| GET | `/feedback/{id}/screenshot` | Image stream, `private, max-age=86400` — submitter or SystemAdmin |

SystemAdmin only — `/api/admin/feedback`:

| Method | Path | Returns |
|---|---|---|
| GET | `/admin/feedback?type=&status=` | `AdminFeedbackDto[]` (newest first; submitter + org included) |
| GET | `/admin/feedback/summary` | `{ newCount }` — nav badge |
| GET | `/admin/feedback/{id}` | `AdminFeedbackDto` |
| PUT | `/admin/feedback/{id}/status` | `AdminFeedbackDto` — `{ status, adminResponse? }`; notifies the submitter when either changed |
| DELETE | `/admin/feedback/{id}` | `204` (spam/test cleanup — not a legal register) |

**Notification split:** submitting fires an **in-app-only** broadcast to every SystemAdmin
(`NotifyManyInAppAsync`) plus **one** e-mail to the configured `Feedback:NotifyEmail` address — not one
e-mail per admin. A status change or reply notifies the submitter with bell **and** e-mail. Saving never
depends on either succeeding. Unset `Feedback:NotifyEmail` → e-mail silently skipped and logged.

**No endpoints for in-app help (SPEC-14)** — its content is a static frontend registry, by design.

---

## Enum Reference

Enums travel as their **numeric** values in both directions — the API registers no
`JsonStringEnumConverter`, so `"reason": "WeakColony"` is rejected with a 400 (`The JSON value could
not be converted…`) before the request reaches the service. The exceptions are DTOs that expose the
name on purpose: `UserRole`, and the assistant's `kind` / `kindLabel` / `status`.

```
BeehiveType:     Langstroth | DadantBlatt | Warré | TopBar | Other
BeehiveMaterial: Wood | Plastic | Polystyrene
HoneyType:       Acacia | Linden | Chestnut | Sunflower | Meadow | Forest | Rapeseed | Other  (BsLabels: Bagrem, Lipa, …)
NotificationType: … | InspectionOverdue=10 | HoneyLevelDrop=11 | FrostWarning=12 | OldQueen=13 | WeeklySummary=14
                  | FeedbackSubmitted=21 (in-app only) | FeedbackStatusUpdated=22
FeedbackType:     Bug | Complaint | Compliment | FeatureRequest | Question | Other  (BsLabels: Prijava problema, Žalba, Pohvala, Prijedlog, Pitanje, Ostalo)
FeedbackSeverity: Low | Medium | High | Critical
FeedbackStatus:   New | InReview | Resolved | Dismissed  (BsLabels: Novo, U razmatranju, Riješeno, Odbijeno)
HoneyLevel:      Low | Medium | High
TodoPriority:    Low | Medium | High
DietStatus:      NotStarted | InProgress | Completed | StoppedEarly
DietReason:      LackOfFood | WinterFeeding | SpringStimulation | (+ 6 more)
FoodType:        SugarSyrup | Fondant | Pollen | ProteinPatties | Custom
UserRole:        Admin | SystemAdmin  (string, not numeric)
MergeReason:     Queenless=1 | LayingWorkers=2 | WeakColony=3 | PoorQueen=4 | Consolidation=5 | Robbing=6 | Other=7
MergeMethod:     Newspaper=1 | Direct=2 | Other=3
MergeQueenOutcome: KeptTarget=1 | KeptSource=2 | None=3
```
