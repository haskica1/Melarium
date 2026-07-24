# Feature: Smart Alerts & Weekly Summary (Pametna upozorenja)

## Overview

Makes notifications **proactive**: a daily background scan watches the data beekeepers already enter
and warns about things they'd otherwise notice too late, plus a Monday AI-written weekly digest per
organization. Implemented per [SPEC-04](../specs/SPEC-04-smart-alerts.md). Reuses the existing
notification infra (in-app bell + email worker) and the Groq stack — no new AI provider.

## Worker

`AlertScanWorker : BackgroundService` (Infrastructure) runs once daily at `Alerts:ScanHourUtc`
(default 5 UTC ≈ 07:00 CEST): it computes the delay to the next occurrence and `Task.Delay`s, then
creates a DI scope and runs the scan. On **Mondays** it also runs the weekly summary. All logic lives
in the Application services so it is unit-testable without a timer; the worker stays thin and never
dies on a failed run (logs + retries next day).

## Part A — Rule-based alerts (`IAlertRuleService`)

Each rule is toggleable via `Alerts:{RuleName}:Enabled` (all default true). Recipients are resolved
per rule, the recipient set is deduplicated, and every candidate is checked against the existing
`Notifications` table via `INotificationRepository.ExistsRecentAsync` before inserting — so re-running
the scan produces **no duplicates** (no new dedup table). Delivery reuses `INotificationService`
(in-app bell + email queue).

| # | Type (enum) | Trigger | Recipients | Dedup window |
|---|---|---|---|---|
| 1 | `InspectionOverdue` (10) | no inspection in `StaleInspectionDays` (21); measured from the last inspection, or hive `CreatedAt` when never inspected | hive's assigned Beekeepers + apiary's ApiaryAdmins + org's OrgAdmins | 7 days |
| 2 | `HoneyLevelDrop` (11) | last 2 inspections strictly decreasing **and** latest = `Low` | same as #1 | 7 days |
| 3 | `FrostWarning` (12) | Open-Meteo daily min < 0 °C within next 48 h (apiary must have coordinates) | all users with access to the apiary | 3 days |
| 4 | `OldQueen` (13) | active queen in ≥ 3rd season; evaluated **only in the March scan month** | same as #1 | 300 days |
| 5 | `StripsLeftIn` (15) | treatment with `Method = Trake` and no `endDate`, started ≥ `StripRemovalDays` (42) days ago (SPEC-08) | all users with access to the apiary | 7 days |
| 6 | `KarencaEnded` (16) | treatment karenca (`karencaUntil`) expired within the last 3 days (SPEC-08) | same as #5 | 7 days |

`relatedEntityId` = hive id (rules 1/2/4, type `Beehive`), apiary id (rule 3, type `Apiary`), or
treatment id (rules 5/6, type `Treatment`).
Apiary without coordinates → frost skipped silently. Weather API unreachable → frost skipped for that
apiary, other rules unaffected.

## Part B — Weekly AI summary (`IWeeklySummaryService`)

On Mondays, for each organization with any activity in the last 7 days:

1. **`WeeklyDigestBuilder`** turns deterministically-gathered facts (inspection count + up to 10
   notable brood/notes lines, honey-level trend per apiary, feedings done, todos created/completed/
   overdue, harvest kg, 7-day weather outlook per apiary) into a compact Bosnian facts block. Pure +
   unit-tested; kg uses InvariantCulture so it's locale-stable.
2. **One Groq call** (`llama-3.3-70b-versatile`, temp 0.3, max 700 tokens): system prompt = Bosnian
   weekly report, 5–8 bullet lines, most actionable first, only stated facts.
3. Delivered as `WeeklySummary` (14) to the org's **OrganizationAdmins + ApiaryAdmins** (deduped with
   a 6-day window against a same-Monday double run; `relatedEntityId` = org id).
4. AI failure → caught and skipped silently (summary is nice-to-have; rules never depend on it). Orgs
   with zero activity get nothing. Skipped entirely when `Groq:ApiKey` is unset.

## Config (`appsettings.json`)

```json
"Alerts": {
  "ScanHourUtc": 5,
  "StaleInspectionDays": 21,
  "StaleInspection": { "Enabled": true },
  "HoneyLevelDrop":  { "Enabled": true },
  "FrostWarning":    { "Enabled": true },
  "OldQueen":        { "Enabled": true },
  "WeeklySummary":   { "Enabled": true }
}
```

Read via the `IConfiguration` indexer + manual parse (no `Configuration.Binder` dependency added).

## Frontend

No new pages. `NotificationBell` type→icon map extended with the five new types (⏰ 📉 ❄️ 👑 📰);
`WeeklySummary` renders its multi-line bullet text with `whitespace-pre-line` instead of the
two-line clamp.

## Tests

`AlertRuleServiceTests` — stale fires past threshold, dedup suppresses, fresh hive doesn't fire,
honey-drop fires only when decreasing to Low, frost fires below 0 °C and is skipped without
coordinates. `WeeklyDigestBuilderTests` — digest contains all counts/sections; `HasActivity`
gating. (The worker itself is intentionally thin and untested.)
