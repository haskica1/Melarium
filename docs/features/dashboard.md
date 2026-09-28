# Feature: Dashboard (Početna)

> Implemented 2026-09-26 with SPEC-29 — see [`specs/SPEC-29-seasonal-notifications.md`](../specs/SPEC-29-seasonal-notifications.md) D10–D13.

## What it is

`/` is the start page for every role except SystemAdmin, who keeps `/admin` (`SmartRedirect` renders
the dashboard rather than redirecting, so `/` stays the address of home). "Početna" is the first menu
item; "Pčelinjaci" stays its own item. Before this, `/` redirected to the apiary list.

## Blocks

| Block | Source |
|---|---|
| Pozdrav + sezonski koraci | five steps of the year, current one with progress; phase and the date the next one starts — no task list (D12) |
| Brojke sezone | hives, inspections this month, honey this year vs. last year to the same day, open todos |
| Traži pažnju | overdue hives, honey dropping, strips, late treatment or feeding rounds — computed live by the same policy as the alerts, grouped per apiary; frost joins from the weather request |
| Obaveze | today + 7 days from `ICalendarObligationService` (the agenda/ICS source); recommended inspections folded to one line per apiary and day |
| Vrijeme | 3 days per apiary with coordinates; frost marked, red when the season makes it Critical |
| Otvoreni zadaci | first five, late first, including those with no due date |
| Stanje košnica | donut "u roku / kasni / nikad" with `inTime/total` in the middle, bars per apiary; in winter "zimsko mirovanje" instead |
| Programi u toku | feeding and multi-round treatments as rings "4/8"; strips (remove-by date), karenca (harvest-from date) |
| Prinos po mjesecima | this year vs. last, plain bars (recharts would add its chunk to the first page everyone loads) |
| Paket | OrganizationAdmin only — rings used/limit, same wording as `/plans` |
| Brze akcije | Novi pregled (hive picker → `/inspections/new?beehiveId=`), Skeniraj QR and Glasovni unos (open the layout's own scanner and assistant via `layoutEvents`), Novi zadatak (managers) |
| Aktuelno u Edukaciji | one published topic for this month, unread first |

A user with no apiaries gets one card instead of empty blocks: create the first apiary (managers) or
"no hives assigned yet" (everyone else).

## Backend

`GET /api/dashboard` → `DashboardDto`; `GET /api/dashboard/weather` → `ApiaryWeatherDto[]`, its own
request so a slow forecast never holds up the page. Both resolve the organization from the token;
SystemAdmin gets **403**. Scope comes from `IAccessGuard.GetAccessibleApiariesAsync/BeehivesAsync` —
role-scoped and without locked rows — so the dashboard is not another hand-filtered aggregate
(ADR-043's pattern, not ADR-042's list). A Beekeeper's yield counts only their own hives; managers
count the whole apiary minus locked hives. Inspection data is read as three columns
(`GetLastDatesAsync`, `GetLevelsSinceAsync`), never whole rows.

Forecasts are cached for 60 minutes per ~1 km cell (`WeatherForecastCache`), shared with the alert
scan, the weekly summary and the apiary page — the dashboard is the landing page, and Open-Meteo's
free tier is not meant for a request per apiary per app open. The inspection's live temperature is
not cached.

## Frontend

`features/dashboard/`: `DashboardPage` plus one component per block; `shared/components/ProgressRing`
(single ring or segmented donut, number in the centre, `role="img"` with a label);
`shared/utils/weather.ts` (WMO code → icon/label, moved from the apiary page);
`shared/utils/layoutEvents.ts`. Mobile order: greeting, Traži pažnju, Obaveze, Vrijeme, numbers, the
rest; on wide screens the numbers move to the top (`lg:order-first`). Help: new `'/'` entry, which also
replaced `/apiaries` as the first auto-open page.

## Tests

`DashboardServiceTests` — SystemAdmin 403, one attention item per apiary with the status chart
filled, winter "resting", Beekeeper yield scoping, frost Critical in April and silent in January.
