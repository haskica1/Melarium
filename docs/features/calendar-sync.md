# Feature: Calendar Sync (SPEC-11, Faza A)

External-calendar visibility + a reliable morning reminder for beekeeping obligations. Implemented per
[SPEC-11](../specs/SPEC-11-calendar-sync.md). **Faza A shipped** (ICS feed + daily 08:00 agenda);
native OAuth (Faza B) and CalDAV/email (Faza C) are planned.

## What it does

- **Private ICS feed:** each user gets a secret subscription URL (`…/api/calendar/feed/{token}.ics`)
  they add once to Google / Apple / Outlook / any CalDAV client. Read-only, one-way (app → calendar).
- **Daily 08:00 agenda:** one consolidated in-app + email notification of the day's obligations —
  the reliable reminder (subscribed calendars refresh slowly and Google ignores feed `VALARM`).
- **Obligations** = feedings (pending) + todos with a due date + derived deadlines (strip removal,
  karenca end, recommended inspection). Each category is toggleable per user.

## Backend

Shared aggregation is the core: `ICalendarObligationService.GatherAsync(ctx, from, to, categories)`
returns a flat `CalendarObligation` list, used by both the feed and the agenda. Access is resolved by
`ICalendarAccessResolver` (extracted from `CalendarService` so all three paths share one authorization).

| Piece | Location |
|---|---|
| `CalendarSettings` entity (per-user token + toggles) | `Domain/Entities`, migration `AddCalendarSettings` |
| Obligation model + resolver + aggregation | `Application/Features/Calendar/` |
| `IcsWriter` (hand-rolled RFC 5545, no NuGet) | `Application/Features/Calendar/IcsWriter.cs` |
| `ICalendarFeedService` (token, settings, `BuildFeedAsync`) | `Application/Features/Calendar/` |
| `IDailyAgendaService` + `DailyAgendaWorker` | `Application/Features/Reminders/`, `Infrastructure/Reminders/` |
| Endpoints | `CalendarController`: `GET feed-url`, `POST feed-url/rotate`, `GET/PUT settings`, `GET feed/{token}.ics` (`[AllowAnonymous]`) |

Notes:
- **Timezone:** `App:TimeZone` (default `Europe/Sarajevo`) via `AppTimeZone` helper — 08:00 local →
  UTC is DST-aware. Whole app is otherwise UTC.
- **Feed token:** opaque 256-bit, stored plaintext (must be re-shown — "secret address" model),
  unique-indexed, rotatable. Feed is anonymous; the token is the only credential.
- `NotificationType.DailyAgenda = 19`. Dedup per calendar day via `ExistsRecentAsync` (dedupId = `yyyyMMdd`).
- Config: `Reminders:DailyAgenda:{Enabled,LocalHour}`, `CalendarFeed:{Enabled,PastDays,FutureDays}`,
  `App:{TimeZone,PublicBaseUrl}`.

## Frontend

- **`/calendar/settings`** (`features/calendar/CalendarSettingsPage.tsx`): feed URL + copy + rotate,
  per-provider subscribe instructions, category toggles, daily-agenda toggle, feed enable/disable.
  Entry point: "Poveži kalendar" button in the calendar hero.
- Service + hooks in `calendarService.ts` / `queries.ts`; `NotificationBell` maps `DailyAgenda` → 📅.

## Tests

`IcsWriterTests` (escaping, folding without splitting multibyte, DST-correct 08:00 trigger, stable
UIDs, all-day dates) and `DailyAgendaServiceTests` (sends when obligations exist, per-day dedup,
silent empty day, per-user opt-out, SystemAdmin skipped).

## Owed / not yet done

- Live subscription check in a real Google/Apple calendar (needs a running server + DB + device).
- Faza B (native Google/Microsoft OAuth, plan-gated Standard+) and Faza C (CalDAV / email .ics).

Verified: backend build 0 errors + 13 new unit tests green (252 total); frontend `tsc` + `vite build` pass.
