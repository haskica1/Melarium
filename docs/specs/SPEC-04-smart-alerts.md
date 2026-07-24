# SPEC-04 — Smart Alerts & Weekly AI Summary ("Pametna upozorenja")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-03) |
| **Effort** | M (~2 days) |
| **Depends on** | notifications infra (exists); queen rule needs SPEC-03; yield line in summary needs SPEC-02 — both degrade gracefully |
| **New secrets** | none (reuses `Groq:ApiKey`, Open-Meteo is keyless) |
| **New packages** | none |

## Goal

Today notifications only react to *account/assignment events*. This spec makes the app **proactive**:
it watches the data the beekeeper already enters and warns about the things they'd otherwise notice
too late — plus a Monday morning AI-written weekly digest per organization.

## Part A — Rule-based alerts (daily scan)

### Worker

`AlertScanWorker : BackgroundService` (same pattern as `EmailNotificationWorker`; creates a scope,
resolves services). Runs once daily at `Alerts:ScanHourUtc` (default `5` ≈ 07:00 CEST); computes
next-run and `Task.Delay`s. All rule logic lives in `IAlertRuleService` (Application) so it's
unit-testable without the worker.

### Rules (each individually toggleable via config `Alerts:{RuleName}:Enabled`, all default true)

| # | Rule | Trigger | Recipients | Text (Bosnian, template) |
|---|---|---|---|---|
| 1 | `StaleInspection` | hive has **no inspection in N days** (`Alerts:StaleInspectionDays`, default 21) | users assigned to the hive + apiary's ApiaryAdmin + OrganizationAdmin | "Košnica '{name}' nije pregledana {n} dana." |
| 2 | `HoneyLevelDrop` | last **2** inspections have strictly decreasing `HoneyLevel`, latest = Low | same as rule 1 | "Košnici '{name}' opada nivo meda — razmisli o prihrani." |
| 3 | `FrostWarning` | Open-Meteo daily min < 0 °C within next 48 h for an apiary with coordinates (via existing `IWeatherService`; one call per apiary per scan) | all users with access to the apiary | "Najavljen mraz za pčelinjak '{name}' ({minTemp} °C). Provjeri prihranu i utopljenost." |
| 4 | `OldQueen` (**only if SPEC-03 shipped**) | active queen in ≥ 3rd season, fired **once per season** (March scan month) | same as rule 1 | "Matica u košnici '{name}' je u {n}. sezoni — planiraj zamjenu." |

**Dedup:** before inserting, check for an existing notification of the same type + same entity
within a window (rule 1/2: 7 days; rule 3: 3 days; rule 4: 300 days) — query the existing
`Notifications` table (extend `INotificationRepository` with `ExistsRecentAsync(userId, type,
relatedEntityId, since)`), no new dedup table. Delivery reuses `INotificationService` → in-app bell
+ existing email queue automatically.

### New `NotificationType` values

Continue the enum: `InspectionOverdue = 10`, `HoneyLevelDrop = 11`, `FrostWarning = 12`,
`OldQueen = 13`, `WeeklySummary = 14`. (Check the frontend notification icon/label map and extend it.)

## Part B — Weekly AI summary (Monday, same worker)

On Mondays (after the daily scan), per organization with any activity in the last 7 days:

1. **Gather digest data** (compact, deterministic — `WeeklyDigestBuilder`, unit-testable):
   inspections count + notable broodStatus/notes lines (max 10, truncated), honey-level trend per
   apiary, feedings done, todos created/completed/overdue, harvest kg (if SPEC-02), 7-day weather
   outlook for each apiary (one line each).
2. **One Groq call** (`llama-3.3-70b-versatile`, temp 0.3, max_tokens 700): system prompt =
   beekeeping assistant writing a **Bosnian weekly report**; rules: only stated facts, no invention,
   5–8 bullet lines, start with the most actionable item, friendly-professional tone.
3. Deliver as notification type `WeeklySummary` to **OrganizationAdmin + ApiaryAdmins** of that org
   (email body = the bullets; in-app shows title "Sedmični pregled" and the text).
4. AI failure → log + skip silently (the summary is nice-to-have; rules must never depend on it).
   Orgs with zero activity get nothing (no noise).

## Config (`appsettings.json` placeholder block)

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

## Frontend

No new pages. Extend the notification bell's type→icon/label map with the five new types
(distinct icons: clock, droplet-down, snowflake, crown, newspaper). `WeeklySummary` notifications
render multi-line text properly (whitespace-pre-line).

## Edge cases

- Apiary without coordinates → frost rule skipped for it (no warning, no error).
- Weather API down → skip frost rule this scan, log warning; other rules unaffected.
- New hive created yesterday → rule 1 measures from hive `CreatedAt` when no inspection exists yet
  (a 1-day-old hive is not "21 days stale").
- App restarts mid-day → worker computes next occurrence, never double-fires (dedup guards anyway).
- Users must **not** get duplicates when they match multiple recipient criteria (dedupe recipient set).

## Out of scope (v1)

Per-user notification preferences/opt-out UI; push notifications (PWA); configurable thresholds
per organization (config is global); swarm-season heuristics beyond rule 2; SMS.

## Acceptance criteria

- [x] Each rule fires on fabricated data and respects its dedup window — unit tests on
      `IAlertRuleService` (worker itself stays thin/untested).
- [x] Rule 1 respects per-hive assignment scoping (Beekeeper gets alerts only for own hives).
- [x] Frost warning appears for an apiary with coords when the forecast dips below 0 °C (manually
      testable by pointing coords at a cold location, or by abstracting the forecast provider in tests).
- [x] Weekly summary email + in-app notification arrive for an org with seeded activity; contains
      only facts present in the digest input (spot-check).
- [x] All five texts in Bosnian; bell icons/labels extended.
- [x] Scan is idempotent: running it twice in a row produces no duplicate notifications.
- [x] Docs updated: `features/smart-alerts.md`, `context.md` (incl. new config keys), this spec → ✅.
