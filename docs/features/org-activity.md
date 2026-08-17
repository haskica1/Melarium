# Feature: Organization Activity (Zadnja aktivnost)

## Overview

The SystemAdmin organization table answers one question it previously could not: **is this
organization actually being used?** Two columns carry the answer — `Košnice` (hive count) and
`Zadnja aktivnost` (last sign of life).

Both are **derived on read, never stored.** There is no `LastActivityAt` column, no heartbeat
worker, and no migration. The consequence that motivated the choice: the value is correct
**retroactively**, for every organization, from the moment it ships — a stored column would start
empty and say nothing useful about the past.

## What counts as activity

`MelariumDbContext.SaveChangesAsync` stamps `UpdatedAt` on every modification of every `BaseEntity`,
so `UpdatedAt ?? CreatedAt` is a reliable "last touched" moment for any row — **no service has to
cooperate**, which is the property that makes this measurable from existing data at all.

`IOrganizationRepository.GetLastActivityAsync` takes the newest such moment across fifteen queries,
one per table, and merges them in memory:

| # | Signal | Reached through |
|---|---|---|
| 1 | Pčelinjak | `Apiary.OrganizationId` |
| 2 | Košnica | `Beehive.Apiary` |
| 3 | Pregled | `Inspection.Beehive.Apiary` |
| 4 | Matica | `Queen.Beehive.Apiary` |
| 5 | Prehrana | `Diet.Apiary` |
| 6 | Hranjenje (odrađena runda) | `FeedingEntry.Diet.Apiary` |
| 7 | Tretman | `Treatment.Apiary` |
| 8 | Runda tretmana | `TreatmentRound.Treatment.Apiary` |
| 9 | Vrcanje | `Harvest.Apiary` |
| 10 | Trošak | `Expense.OrganizationId` |
| 11 | Pašnjak | `Pasture.OrganizationId` |
| 12 | Član | `User.OrganizationId` |
| 13 | Zadatak na pčelinjaku | `Todo.Apiary` (where `ApiaryId != null`) |
| 14 | Zadatak na košnici | `Todo.Beehive.Apiary` (where `BeehiveId != null`) |
| 15 | Prijava / rotacija sesije | `RefreshToken.User` — **`CreatedAt` only** |

**Why rounds (6, 8) are queried separately from their parents.** A diet is defined once and then fed
for weeks; ticking a round off does not reliably bump the parent `Diet`. Without these two, an
organization actively feeding its hives every second day would look idle.

**Why todos are two queries (13, 14).** A todo carries exactly one of `ApiaryId`/`BeehiveId`, so the
two kinds are two plain joins rather than one conditional join EF would have to guess at.

**Why refresh tokens use `CreatedAt`, not `UpdatedAt` (15).** A row is written on sign-in and on
every rotation while the app is in use. `UpdatedAt` also moves when a token is revoked from the
outside (an admin revoking sessions), which is not the organization acting.

**Tables deliberately absent, because another one already covers them:** `ApiaryMove` (bumps its
apiary), `HarvestEntry` / `ExpenseItem` (written inside the parent's own update), `QueenEditLog`
(bumps the queen), `DietBeehive` / `TreatmentEntry` (bump their parent). The one real omission is
`InspectionPhoto` — an upload lands on the same day as the inspection it belongs to.

## Query shape

One `GROUP BY organization, MAX(COALESCE(UpdatedAt, CreatedAt))` per table, merged in memory —
deliberately **not** a single UNION. Each of these translates to SQL on every EF version; grouping
across a set operation is the kind of query that fails at runtime rather than at build time.

The optional `organizationId` parameter narrows every query to one organization, so the single-org
read path does not scan the platform. `AdminService` routes **every** `AdminOrganizationDto` through
this, including the ones returned by create/update, so the DTO never reports a placeholder `0` hives
for an organization that has forty.

## UI Rules

- `AdminDashboardPage` → `ActivityCell`: relative time in Bosnian (`prije 4 dana`, via `date-fns`
  `bs` locale) over the exact date, or `nikad`.
- Coloured by freshness: **≤ 30 days** emerald, **31–90** amber, **> 90 or never** red.
  90 is the dormancy threshold agreed in SPEC-16 §0 D1; 30 splits "working normally" off from "worth
  a look" so the two never share a colour.
- It is a **label, not a verdict** — no organization is declared abandoned here. A person reads the
  date and decides, which is exactly what SPEC-16 D2 reserves for a human.

## Relationship to SPEC-16

SPEC-16 (📋 Planned) specifies this column via a stored `LastActivityAt` plus an
`ActivityTrackingWorker` and a middleware heartbeat. **This implementation delivers the column, and
only the column, by a different route.** Still unbuilt from that spec: `IsActive` (the manual
lock that blocks sign-in), `FirstPaidAt`, the computed `OrgStatus` badge
(Aktivna/Uspavana/Za brisanje), the billing worklists, and the organization-purge fix.

Known difference in behaviour, accepted: an organization that only *reads* the app still registers,
because signal 15 counts session refresh — but a genuinely idle open tab is a weaker signal here
than under SPEC-16 §3.2, since the fourteen data signals dominate whenever any real work happens.

## Key Files

| What | File |
|---|---|
| The fifteen queries + hive counts | `Melarium.Entity/Repositories/OrganizationRepository.cs` |
| Contract | `Melarium.Application/Common/Interfaces/IOrganizationRepository.cs` |
| Wiring into the DTO | `Melarium.Application/Features/Admin/AdminService.cs` |
| Tests | `Melarium.Application.Tests/AdminOrganizationMetricsTests.cs` |
| Column + colouring | `frontend/src/features/admin/AdminDashboardPage.tsx` (`ActivityCell`) |
