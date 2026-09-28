# Feature: Seasonal Notifications (Sezonski prilagođene notifikacije)

> Implemented 2026-09-26. Spec: [`specs/SPEC-29-seasonal-notifications.md`](../specs/SPEC-29-seasonal-notifications.md).
> Decisions: ADR-046 (derived season, the organization's shift), ADR-047 (stored priority, one morning e-mail).

## Why

The SPEC-04 alerts and the SPEC-11 agenda ran the same all year. In winter that meant a weekly "Košnica
bez pregleda" for every single hive, a frost warning every three days, and an agenda recommending
inspections nobody should do. People who get that learn to ignore the bell — including the one
message that mattered.

## The beekeeping year

| Phase (`SeasonPhase`) | Label | Default |
|---|---|---|
| `Winter` = 1 | Zimsko mirovanje | 15.11. – 15.02. |
| `SpringBuildUp` = 2 | Proljetni razvoj | 16.02. – 15.04. |
| `MainSeason` = 3 | Glavna sezona | 16.04. – 31.07. |
| `LateSummer` = 4 | Ljetno-jesenja priprema | 01.08. – 30.09. |
| `Wintering` = 5 | Zazimljavanje | 01.10. – 14.11. |

`ISeasonCalendar` computes the phase from the **local** date and `Organization.SeasonShiftDays`
(−14…+30, edited on `/organization`). Nothing stores the phase. The shift moves the spring boundaries
**later** by N and the autumn ones **earlier** by N; 1 August stays. On +21: winter 25.10.–08.03.,
spring 09.03.–06.05., main season 07.05.–31.07., late summer 01.08.–09.09., wintering 10.09.–24.10.

## One policy

`INotificationPolicy` holds the table every path reads — the alert scan, the weekly summary, the
agenda and ICS feed, the dashboard, and `NotificationService` itself.

| Alert | Winter | Spring | Main | Late summer | Wintering |
|---|---|---|---|---|---|
| `InspectionOverdue` | off | Normal, 30 d | Normal, 21 d | Normal, 30 d | Normal, 45 d |
| `FrostWarning` (48 h) | only < −15 °C, Normal | **Critical** | **Critical** | Normal | first frost only, once |
| `HoneyLevelDrop` | off | Normal | Normal | Normal | Normal |
| `OldQueen` | off | Info, once a year | off | off | off |
| `StripsLeftIn`, `KarencaEnded`, `FeedingOverdue`, `TreatmentRoundOverdue` | always, Normal | ← | ← | ← | ← |
| `PlanExpiring` / `PlanLockPending` | always, Normal / Critical | ← | ← | ← | ← |
| `SeasonPhaseStarted` | Normal, once per phase | ← | ← | ← | ← |

Dedup for `InspectionOverdue` is 7 days in the main season and 14 outside it. The
`Alerts:{Rule}:Enabled` switches still work on top of the table (`StaleInspection` for
`InspectionOverdue`, as before).

**Winter days never count toward "days without inspection".** If a winter lies between the last
inspection and today, the clock starts on the first day of spring. `IsInspectionOverdue(last, date,
shift)` answers the scan; `InspectionBecomesDue(last, from, to, shift)` gives the agenda and the ICS
feed the day the hive *becomes* overdue — one per hive per window, so ICS UIDs stay stable.

## Grouping

Hive rules (`InspectionOverdue`, `HoneyLevelDrop`, `OldQueen`) produce **one notification per
recipient per apiary** with `relatedEntity = Apiary`. Each recipient gets only the hives they answer
for: apiary and organization admins all of them, an assigned beekeeper only theirs. One hive keeps
the old one-sentence wording; several become a header line plus `- K2 (34 dana)` lines, at most 15
listed. Dedup is on the apiary, so a hive that becomes overdue after this week's reminder joins next
week's.

## Priorities and delivery

`Notification.Priority` is stored (`Normal = 0`, `Critical = 1`, `Info = 2`).
`NotificationService.NotifyAsync` decides the channel:

| | E-mail "Sva" | "Samo kritična" | "Isključeno" |
|---|---|---|---|
| Critical | at once | at once | — (bell only) |
| Normal scan alert, agenda | in the 08:00 morning e-mail | — | — |
| Normal from a person (todo, assignment, review answer) | at once | — | — |
| Info | — | — | — |
| Security (password changed, new account, organization handed over) | always | always | always |

The **morning e-mail** is composed by `DailyAgendaService` at 08:00 local (`MorningEmail.Compose`,
structured since ADR-048): "Dobro jutro, {ime}", the day's two numbers, **Traži pažnju** — one card per
Normal alert from `GetNormalSinceAsync(now − 24 h)`, a grouped alert's hives as rows — then **Današnje
obaveze** as a checklist linking each obligation to its page, then **Za čitanje**: the phase notice
(highlighted, with its Edukacija topics) and the AI summary. Subject: "Jutarnji pregled: 4 obaveze,
3 upozorenja". Button: "Otvori Melarium" → `/`. The in-app agenda is unchanged and no longer mails by
itself. See `docs/features/email.md`.

## Settings (`NotificationSettings`)

One row per user, created on first save (defaults = the old behaviour: all e-mail, everything in the
app). `GET|PUT /api/notifications/settings` — own settings only; `EmailMode` validated with
`IsInEnum`. In-app switches: "Obična upozorenja" and "Savjeti" hide those alerts entirely (not
created); Critical has no switch. Profile page, section "Obavještenja".

## Phase started (`SeasonPhaseStarted` = 32)

Sent by the daily scan to **every member** of an organization, once per phase (key =
`year * 10 + phase`, looked back 200 days so a shift edited mid-phase cannot repeat it), and only in
the first `Alerts:Seasons:PhaseNoticeDays` (14) of the phase. Content: the phase's work
(`SeasonalTasks`, in code — ADR-031 reasoning) and up to three published Edukacija topics in
*Sezonski radovi* whose months overlap the phase. In the bell it opens `/learning?category=2`; in the
morning e-mail it carries that link.

## Weekly summary in winter

`SummaryCadenceFor(Winter)` = first Monday of the month, covering the previous month, titled
"Mjesečni pregled", and the Groq system prompt asks for a "mjesečni" report. Otherwise weekly, as
before. The phase is the organization's own (its shift applies).

## Config

```json
"Alerts": {
  "SeasonPhaseStarted": { "Enabled": true },
  "Seasons": {
    "SpringBuildUpStarts": "02-16", "MainSeasonStarts": "04-16", "LateSummerStarts": "08-01",
    "WinteringStarts": "10-01", "WinterStarts": "11-15",
    "InspectionOverdueDays": { "SpringBuildUp": 30, "MainSeason": 21, "LateSummer": 30, "Wintering": 45 },
    "ExtremeColdC": -15,
    "PhaseNoticeDays": 14
  }
},
"Weather": { "CacheMinutes": 60 }
```

`MainSeason` falls back to the old `Alerts:StaleInspectionDays` if its own key is absent.

## Frontend

Bell: 🌱 for `SeasonPhaseStarted`, any message with line breaks rendered in full
(`whitespace-pre-line`), Critical rows red with a "Kritično" tag. Profile: `NotificationSettingsSection`.
`/organization`: "Pomak sezone" with a live preview of the five phases — derived from the phases the
server sent for the saved shift, so no season dates are copied into the client.

## Tests

`SeasonCalendarTests`, `NotificationPolicyTests`, `NotificationServiceTests`,
`CalendarObligationSeasonTests`, and the extended `AlertRuleServiceTests`, `DailyAgendaServiceTests`,
`WeeklySummaryPlanTests`. All run on a fixed clock (`TestSeasons.At`) — a seasonal test that reads the
machine's clock passes in September and fails in December.
