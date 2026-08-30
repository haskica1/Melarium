# Moja organizacija (SPEC-22)

> Implemented 2026-08-30. Spec: [`specs/SPEC-22-org-profile.md`](../specs/SPEC-22-org-profile.md).

## What it is

An OrganizationAdmin can now edit the organization they created at sign-up — its **name**, its
**description**, and its **logo** — from `/organization`. Until this shipped, an organization was
created by `AuthService.RegisterAsync` and then unreachable to its own owner: a typo in the name
could only be fixed by the SystemAdmin through `/admin`.

The same change fills in the SystemAdmin's own tables, which were carrying data they never showed.

## The org is read from the token, never from a route

There is no `/api/organizations/{id}`. Every method on `IOrgProfileService` resolves the organization
from `ICurrentUser.OrganizationId`, so there is no id to tamper with and no `IAccessGuard` call to
forget. The role split lives on the controller attributes: **any member reads, only OrganizationAdmin
writes**.

A SystemAdmin has no organization of their own, so these endpoints answer **403, not 404** — the row
exists for everyone else, they simply are not in one. That is also why the nav item is hidden for
them: they administer every *other* organization through `/admin`.

| Method | Path | Who |
|---|---|---|
| GET | `/organizations/my` | any member |
| PUT | `/organizations/my` | OrganizationAdmin |
| POST | `/organizations/my/logo` | OrganizationAdmin — multipart `file`, ≤ 2 MB |
| GET | `/organizations/my/logo` | any member |
| DELETE | `/organizations/my/logo` | OrganizationAdmin |
| GET | `/admin/organizations/{id}/logo` | SystemAdmin |

`MyOrganizationDto` is deliberately **not** `AdminOrganizationDto`: plan, payment note and activity
are SystemAdmin data, not something a tenant reads about itself. It carries the counts
(`userCount`, `apiaryCount`, `beehiveCount`) so the page shows what the organization *is* rather
than two input fields.

## The logo

Two nullable columns (`LogoStoragePath`, `LogoContentType`) and the existing `IFileStorage`
(ADR-027) — no `LogoUrl`, no public bucket. The blob is streamed through the API behind the auth
check, exactly like an inspection photo, which means an `<img src>` cannot fetch it: the frontend
pulls the bytes through `apiClient` and renders an object URL. The URL lives in the React Query
cache (`orgQueryKeys.myOrganizationLogo`) so the page and the header do not download it twice, and
it is revoked before the entry is replaced.

`Cache-Control` is **`private, no-cache`**, unlike the inspection photo's day-long cache: this URL
never changes, so a replaced logo would keep showing the old image until the cache expired.

Three details that are load-bearing:

- **The format is decided by the file's header bytes** (`Common/Validation/ImageRules`), never by
  the client's `Content-Type` or the extension. Same JPEG/PNG/WebP set as inspection photos.
- **Replacing a logo deletes the old blob only after the new key is committed**, and deletion is
  best-effort — a storage hiccup must not undo a saved change, and a failed DB write must not leave
  the just-uploaded blob orphaned.
- **On the client, a PNG or WebP that already fits passes through untouched**
  (`prepareLogoForUpload`). Re-encoding it to JPEG would flatten a transparent logo onto a white
  square. A phone photo or an iPhone HEIC does go through the canvas and comes back a bounded JPEG.

## Renaming does not sign anyone out

The organization *name* is not a JWT claim (`organizationId` is, and it does not change), so no
session is revoked. But the cached session in `localStorage` carries `organizationName` — the label
under the profile avatar — so the page pushes the new name through `updateUser`, otherwise the old
one would survive until the next sign-in. That is why `updateUser`/`updateStoredUser` accept
`organizationName` as well as the three personal fields.

## The system tables

### Organizacije

| Added | Where it comes from |
|---|---|
| Logo beside the name | `hasLogo` + `GET /admin/organizations/{id}/logo` (only fetched for orgs that have one) |
| **Vlasnik** — name, e-mail (`mailto:`), phone (`tel:`) | the org's OrganizationAdmin, derived in `AdminService.MapOrganization` |
| Filter **Paket** — each plan, plus "⏰ Istekao paket" | `plan` / `planValidUntil` |
| Filter **Aktivnost** — ≤30 d, 30–90 d, >90 d, nikad | `lastActivityAt` (ADR-034), same thresholds `ActivityCell` colours |
| Sorting on every column | client-side |

**"Vlasnik" is the OrgAdmin, not `CreatedBy`.** For a self-registered organization those are the
same person, but for one the SystemAdmin created, `CreatedById` is *Asim*. The rule: the OrgAdmin
whose id equals `CreatedById` (the founder, while they still hold the role), otherwise the
longest-standing OrgAdmin, otherwise `null` — and an organization with **no** admin shows "bez
admina", which is a state worth seeing. It is computed from `org.Users`, which the repository
already includes, so it costs no extra query.

### Korisnici

| Added | Where it comes from |
|---|---|
| **Kontakt** — e-mail with a verified/unverified mark, plus phone | `emailVerifiedAt`, `phone` |
| **Zadnja prijava** | `lastLoginAt` — see below |
| **Registrovan** | `createdAt` |
| Assigned-hive count for Beekeepers | `assignedBeehiveIds.length` |
| Filters **Uloga** and **Status** (unverified / never signed in / idle 90+ days) | — |

**"Zadnja prijava" is derived, not stored**: `MAX(RefreshToken.CreatedAt)` per user
(`IUserRepository.GetLastLoginAtAsync`). A token row is written on sign-in *and* on every refresh,
so it really reads as "last time this account was used". Nothing prunes the table, so the maximum
covers the account's whole history. Same source ADR-034 already uses for organization activity, and
the same reasoning — correct retroactively, no new column, no heartbeat. See **ADR-040**.

### Vitals

A fifth tile, **Istekli paketi**. v1 billing is manual and annual (SPEC-09) with no dunning job;
this tile is the reminder. When a filter is on, the count beside a section title becomes
`shown / total`, so a filter is never invisible.

## Deliberately not built

Contact and official fields on the organization (e-mail, phone, address, JIB/ID, beekeeping-register
number) were offered — with the argument that they would later fill the header of the SPEC-08
treatment PDF — and were **declined for v1**: basic fields only, for now. So the migration adds two
columns, not eight. Also out: the logo in the treatment PDF or on QR labels, the logo replacing 🐝
in the sidebar, a SystemAdmin uploading a logo for someone else's org, CSV export, and an expandable
detail row.

## Deploy

`dotnet ef database update` — migration `AddOrganizationLogo` (two nullable columns, no data
change). Nothing else: no new env var, no new package, no new bucket. `Storage:Provider` already
points at the same place inspection photos use.
