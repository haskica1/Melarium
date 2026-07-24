# SPEC-11 — Integracija s vanjskim kalendarom (Calendar Sync)

| | |
|---|---|
| **Status** | 🔨 Faza A implementirana (backend + frontend, 2026-07-13) — backend build 0 err + 13 novih unit testova zeleno (252 ukupno); frontend `tsc` + `vite build` prolaze. Faza B/C planirane. Owed: live pretplata u Google/Apple (traži pokrenut server + DB + uređaj) |
| **Obim posla** | L (Faza A ~2–3 dana; Faza B ~4–6 dana; Faza C opciono) |
| **Zavisi od** | ničega tvrdo; ponovo koristi in-app kalendar (`CalendarService`), notifikacijsku infrastrukturu (SPEC-04), plan-gating (SPEC-09), i izvedene rokove iz SPEC-04/08 |
| **Novi secreti** | Faza A: nijedan. Faza B: `Google__ClientId/Secret`, `Microsoft__ClientId/Secret`, `Calendar__TokenEncryptionKey` (svi env var) |

## Cilj

Obaveze koje aplikacija već zna — hranjenja (`FeedingEntry`), todo obaveze s rokom, i izvedeni
rokovi (preporučeni pregled, vađenje traka, istek karence) — treba da se **pojave u korisnikovom
vlastitom kalendaru** (Google, Apple/iCloud, Outlook, Thunderbird, bilo koji CalDAV klijent) i da ga
**podsjete u 8 sati ujutru** na dan obaveze. Primjer koji vodi dizajn: prehrana od 10 dana svaki 2.
dan već generiše 5 `FeedingEntry` — svih 5 se mora vidjeti u vanjskom kalendaru i okinuti podsjetnik
u 8h svakog od tih dana.

**Ključna projektna odluka:** "vidljivost u kalendaru" i "podsjetnik u 8h" su dva odvojena mehanizma.
Vidljivost rješava sinhronizacija kalendara; pouzdan podsjetnik rješava **naša postojeća
notifikacija** (zvono + email), jer vanjski kalendari nejednako poštuju alarme (Google ignoriše
`VALARM` na ICS pretplati). Oba se isporučuju u Fazi A.

### Odluke iz konsultacije (definišu obim)

1. **Provajderi:** ICS feed (univerzalno) prvo → Faza A; nativni Google/Microsoft OAuth → Faza B.
2. **Podsjetnik u 8h:** **oboje** — app-notifikacija (pouzdana) + alarm u kalendaru (best-effort u
   Fazi A, pun u Fazi B).
3. **Naplata:** ICS feed **besplatan** za sve; nativni OAuth sync **Standard+** (`PlanFeature.CalendarSync`).
4. **Obim eventa:** **sve obaveze** — hranjenja + todo + izvedeni rokovi (pregled, trake, karenca).

## Faze (nezavisno isporučive)

- **Faza A (v1):** ICS feed (pretplata na tajni URL) + dnevna app-notifikacija u 8h. Pokriva sve
  provajdere odjednom, bez OAuth-a i bez Google verifikacije. **Ovo je puni odgovor na traženi
  scenarij.**
- **Faza B (v2):** Nativni Google Calendar + Microsoft Graph OAuth sync u zaseban "Melarium 🐝"
  kalendar; realtime na izmjene, alarmi koje kalendar poštuje; plan-gated (Standard+).
- **Faza C (opciono):** CalDAV (Apple/Fastmail/Nextcloud, app-specific password) i/ili email `.ics`
  pozivnice kao fallback za provajdere van A/B.

---

## Preduslov: vremenska zona (obje faze)

Cijela aplikacija radi u UTC-u (`ScanHourUtc`, `FeedingEntry.ScheduledDate` je `.Date`). "8h lokalno"
uz ljetno/zimsko računanje vremena (CET↔CEST) traži poznavanje zone.

- Uvodi se config `App:TimeZone` (default `Europe/Sarajevo`) i `App:PublicBaseUrl`
  (npr. `https://app.beehive.ba`) — potreban za feed URL i deep-linkove u `DESCRIPTION`.
- Lokalno 08:00 → UTC se računa preko `TimeZoneInfo.FindSystemTimeZoneById(App:TimeZone)` +
  `TimeZoneInfo.ConvertTimeToUtc` **na dan obaveze** (DST-ispravno; 08:00 CEST = 06:00 UTC ljeti,
  08:00 CET = 07:00 UTC zimi).
- Per-korisnička zona je **van opsega** (v1 pretpostavlja jednu app-zonu; BiH je jedna zona). Šav za
  kasnije: kolona `CalendarSettings.TimeZone`.

---

## Zajednička osnova: agregacija obaveza

Feed, dnevni podsjetnik i (Faza B) nativni sync moraju gledati **isti skup obaveza**. Zato se logika
prikupljanja izdvaja u jedan izvor istine.

- `CalendarService.GetCalendarEventsAsync()` trenutno čita pristup iz `ICurrentUser` i vraća samo
  `Todos` + `FeedingEntries`. Refaktoriše se tako da jezgro prima **eksplicitan korisnički kontekst**
  (`userId`, `role`, `organizationId?`, `apiaryId?`) umjesto da čita iz `ICurrentUser` — jer feed
  endpoint radi **anonimno** (autentikacija je token, ne JWT), pa `ICurrentUser` nije popunjen.
- Nova metoda `Task<IReadOnlyList<CalendarObligation>> GatherAsync(CalendarUserContext ctx, DateRange range)`
  vraća ujednačen model:

```csharp
public sealed record CalendarObligation(
    ObligationKind Kind,        // Feeding | Todo | InspectionDue | StripRemoval | KarencaEnd
    string StableKey,           // "feeding-1234" | "todo-88" | "strips-12" | … (za UID/idempotenciju)
    DateOnly Date,              // dan obaveze (lokalno)
    string Title,               // bosanski, sa emoji prefiksom
    string? Description,        // program/pčelinjak/doza + deep-link
    string? Location,           // ime pčelinjaka (+ koordinate za "otvori u mapama")
    int? BeehiveId, int? ApiaryId,
    bool IsSoft);               // izvedeni/pomjerivi rok (npr. preporučeni pregled)
```

Izvori (sve što `IsSoft=false` ima fiksan datum; `IsSoft=true` se preračunava svaki put):

| Kind | Izvor | Datum | Napomena |
|---|---|---|---|
| `Feeding` | `FeedingEntry` (Pending), diet ≠ StoppedEarly/Completed | `ScheduledDate` | jezgro; već u kalendaru |
| `Todo` | `Todo` s `DueDate`, `!IsCompleted` | `DueDate` | jezgro; već u kalendaru |
| `StripRemoval` | tretman `Method=Trake`, bez `EndDate` (SPEC-08) | `StartDate + Alerts:StripRemovalDays` (42) | reuse logike iz `AlertRuleService` (SPEC-04 pravilo #5) |
| `KarencaEnd` | tretman s karencom (SPEC-08) | kraj karence | reuse SPEC-04 pravilo #6 |
| `InspectionDue` | košnica bez pregleda ≥ praga | `(zadnji pregled ⟂ CreatedAt) + Alerts:StaleInspectionDays` (21) | `IsSoft=true`; reuse SPEC-04 pravilo #1 |

Pristup po roli (SystemAdmin/OrgAdmin/ApiaryAdmin/Beekeeper) ostaje identičan postojećoj
`CalendarService` rezoluciji — feed i podsjetnik vide **tačno ono što korisnik vidi u in-app
kalendaru**. Postojeći JSON endpoint `GET /api/calendar` nastavlja raditi (mapira `GatherAsync` na
zatečene `CalendarTodoDto`/`CalendarFeedingEntryDto`; izvedeni rokovi se dodaju i u in-app kalendar
radi konzistentnosti).

Svaki `Kind` je **prekidač po korisniku** (`CalendarSettings`, dolje) i globalno preko configa.

---

## FAZA A.1 — ICS feed (pretplata)

### Domenska pravila

- **Po korisniku, read-only, jednosmjerno** (app → kalendar). Feed odražava trenutni skup obaveza pri
  svakom dohvatu; nema pohranjenog stanja eventa.
- Autentikacija je **tajni token u URL-u** (model "tajne adrese" kao Google-ov privatni iCal URL).
  Token je visoke entropije (32B, base64url), **pohranjen heširan** (presedan: refresh tokeni se
  čuvaju heširani) — dohvat hešira dolazni token i traži korisnika.
- Feed je **anoniman endpoint** (kao javni QR-scan lookup) — izuzet iz `[Authorize]`; token je jedini
  ključ. HTTPS obavezno; bez PII-ja preko imena košnica/obaveza.
- **Rotacija/gašenje:** korisnik može rotirati (stari URL prestaje raditi → 404) ili ugasiti feed.

### Model / migracija

Novi entitet `CalendarSettings` (1:1 s korisnikom) — nosi feed token i prekidače (i budući per-user
timezone/hour):

```
CalendarSettings : BaseEntity
  UserId (FK, unique)
  FeedTokenHash string?          // null = feed nije nikad generisan
  FeedEnabled bool (default true)
  SyncFeedings / SyncTodos / SyncTreatments / SyncInspections  bool (default true)
  DailyAgendaEnabled bool (default true)
```

Migracija `AddCalendarSettings`. (Alternativa — kolone na `User` — odbačena da se ne bubri auth
agregat i da prekidači imaju svoj dom.)

### Endpointi

| Metoda | Putanja | Auth | Napomene |
|---|---|---|---|
| GET | `/api/calendar/feed-url` | JWT | vraća `{ url, enabled }`; generiše token pri prvom pozivu |
| POST | `/api/calendar/feed-url/rotate` | JWT | poništava stari, izdaje novi token |
| PUT | `/api/calendar/settings` | JWT | prekidači kategorija + `feedEnabled` + `dailyAgendaEnabled` |
| GET | `/api/calendar/feed/{token}.ics` | **anonimno** | `text/calendar; charset=utf-8`; token → korisnik → `GatherAsync` → ICS |

Kontroler ostaje tanak; ICS se generiše u `ICalendarFeedService` (Application). Feed pokriva prozor
**[danas − 7 dana, danas + 120 dana]** (prošlost radi konteksta, budućnost radi planiranja).

### ICS format (bez novog NuGet paketa)

RFC 5545 se generiše **ručno** (mali writer + unit test) — u skladu s pravilom "ne dodavati pakete
bez pitanja". `Ical.Net` je alternativa ako se kasnije zatreba RRULE složenost.

Po obavezi jedan `VEVENT`:

```
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Melarium//Calendar Feed//BS
CALSCALE:GREGORIAN
METHOD:PUBLISH
X-WR-CALNAME:Melarium — obaveze
X-WR-TIMEZONE:Europe/Sarajevo
REFRESH-INTERVAL;VALUE=DURATION:PT12H
X-PUBLISHED-TTL:PT12H
BEGIN:VEVENT
UID:feeding-1234@beehive.ba
DTSTAMP:20260713T060000Z
DTSTART;VALUE=DATE:20260715
DTEND;VALUE=DATE:20260716
SUMMARY:🍯 Prihrana: Šećerni sirup — Košnica 12
DESCRIPTION:Program: Zimska prihrana\nPčelinjak: Vlašić\nOtvori: https://app.beehive.ba/diets/55
LOCATION:Pčelinjak Vlašić
CATEGORIES:Melarium,Prihrana
STATUS:CONFIRMED
SEQUENCE:0
BEGIN:VALARM
ACTION:DISPLAY
DESCRIPTION:Prihrana danas — Košnica 12
TRIGGER;VALUE=DATE-TIME:20260715T060000Z
END:VALARM
END:VEVENT
END:VCALENDAR
```

Detalji koji moraju biti tačni (idu u writer + testove):

- **`UID` je stabilan i deterministički** (`{kind}-{sourceId}@{host}`) — izmjena obaveze mijenja isti
  event umjesto da pravi duplikat.
- **All-day event** (`DTSTART;VALUE=DATE`, `DTEND` = sljedeći dan) da lijepo stoji na vrhu dana; +
  **`VALARM` s apsolutnim `TRIGGER` u UTC-u** izračunatim iz **08:00 app-zone tog dana** (DST-ispravno).
  Apsolutni trigger je pouzdaniji od relativnog na all-day eventima kroz klijente.
- **`VALARM` je best-effort:** Apple/Outlook/Thunderbird ga poštuju; **Google ga ignoriše na
  pretplaćenom kalendaru** → zato Faza A.2 (app-notifikacija) nosi pouzdan podsjetnik.
- **Escaping** (`\` `;` `,` → escaped; novi red → `\n`), **CRLF** završeci linija, **folding na 75
  okteta**. UTF-8 (emoji u SUMMARY je ok).
- **Završene/otkazane obaveze se izostavljaju** (feed pokazuje samo otvorene) — diet StoppedEarly i
  Completed feedings već otpadaju u agregaciji.
- `SEQUENCE`/`LAST-MODIFIED` iz `UpdatedAt` izvora radi korektnog osvježavanja.

### Ograničenje osvježavanja (iskreno korisniku)

Klijenti kontrolišu ritam povlačenja pretplate: **Google osvježava i do ~24h**, Apple je podesiv
(15 min–dnevno), Outlook slično. Zato feed **nije realtime** — nova prehrana se može pojaviti tek za
nekoliko sati. `REFRESH-INTERVAL`/`X-PUBLISHED-TTL` su savjet, ne garancija. Realtime stiže tek s
Fazom B. UI to jasno komunicira ("kalendar se osvježava periodično; podsjetnik u 8h stiže i kroz
aplikaciju").

---

## FAZA A.2 — Dnevni podsjetnik u 8h (pouzdani, kroz app)

Popunjava postojeću rupu: nema alarma "danas imaš hranjenje". Radi neovisno o vanjskom kalendaru.

- **`DailyAgendaWorker : BackgroundService`** (Infrastructure) — struktura kao `AlertScanWorker`:
  budi se u **08:00 `App:TimeZone`** (računa sljedeći okidač DST-ispravno preko `TimeZoneInfo`), otvori
  DI scope, pozove Application servis, nikad ne umire na grešci (log + sljedeći dan). Konfig:
  `Reminders:DailyAgenda:Enabled` (default true), `Reminders:DailyAgenda:LocalHour` (default 8).
- **`IDailyAgendaService.RunAsync`** (Application, unit-testabilan bez timera): za svakog korisnika s
  `DailyAgendaEnabled` skupi današnje obaveze preko `GatherAsync(ctx, danas..danas)`; ako ih ima,
  pošalje **jednu konsolidovanu notifikaciju** (bez spama od N poruka).
- **Isporuka** preko postojećeg `INotificationService.NotifyAsync` → in-app zvono + email queue.
  Novi tip `NotificationType.DailyAgenda = 19` (18 je zauzeo SPEC-09).
- **Dedup** preko postojećeg `INotificationRepository.ExistsRecentAsync(userId, DailyAgenda, …, ~20h)`
  — ponovni run istog dana ne šalje duplo (bez nove dedup tabele; presedan SPEC-04).
- **Sadržaj (bosanski):** *"Dobro jutro! Danas (15.07.) imaš 3 obaveze: 🍯 Prihrana — Košnica 12;
  ✅ Todo: Zamijeni satne osnove — Košnica 3; 💊 Izvadi trake — Košnica 7."* + link na `/calendar`.
- **Recipienti** su per-korisnik (svako svoj skup po pristupu) — ista rezolucija kao feed.

---

## FAZA B — Nativni OAuth sync (Google + Microsoft)

Realtime, alarmi koje kalendar poštuje, zaseban kalendar. **Plan-gated: Standard+.**

### Provajderi i opsezi (scopes)

- **Google Calendar API** — OAuth 2.0. Preporučeni scope `https://www.googleapis.com/auth/calendar.app.created`
  (kreira/upravlja **samo kalendarom koji je app napravila** — najmanje privilegija; "sensitive"
  scope). Alternativa `…/auth/calendar.events` ako se app-created pokaže ograničavajućim.
- **Microsoft Graph** — OAuth 2.0 (Azure AD app, multi-tenant + lični nalozi), scope `Calendars.ReadWrite`.
- **Zaseban kalendar "Melarium 🐝"** se kreira pri povezivanju (ne diramo primarni; korisnik ga gasi/boji
  kao cjelinu, a odspajanje ga čisto briše).

### Entiteti / migracija (`AddCalendarConnections`)

```
CalendarConnection : BaseEntity
  UserId (FK)
  Provider  (Google | Microsoft)
  AccessTokenEnc / RefreshTokenEnc  string     // šifrovano at-rest
  TokenExpiresAt DateTime
  ExternalCalendarId string                    // id "Melarium" kalendara kod provajdera
  Status (Active | NeedsReauth | Revoked)
  LastSyncAt DateTime?

ExternalCalendarEvent : BaseEntity             // mapiranje idempotencije
  CalendarConnectionId (FK)
  StableKey string                             // = CalendarObligation.StableKey
  ExternalEventId string
  SyncedHash string                            // hash Title+Date+Desc; preskače no-op update
  ETag string?
  // UNIQUE (CalendarConnectionId, StableKey)
```

### OAuth flow (endpointi)

| Metoda | Putanja | Napomene |
|---|---|---|
| GET | `/api/calendar/connect/{provider}` | JWT; `EnsureFeatureAsync(orgId, CalendarSync)` → redirect na consent (state = CSRF + userId) |
| GET | `/api/calendar/oauth/{provider}/callback` | razmjena `code`→tokeni, kreira "Melarium" kalendar, snimi `CalendarConnection`, initial full sync |
| GET | `/api/calendar/connections` | JWT; status po provajderu (Active/NeedsReauth) |
| DELETE | `/api/calendar/connections/{id}` | odspoji: opozovi token kod provajdera, obriši vanjski kalendar (best-effort), obriši red |

### Sync engine

- **Na izmjenu izvora** (diet create/update/delete, todo, tretman, pregled) → obligacija se preračuna i
  **enqueue-a sync job** (Channel + `CalendarSyncWorker`, presedan `EmailNotificationWorker`). Off the
  request path, best-effort, retry.
- Za svakog **povezanog korisnika koji ima pristup** toj obavezi: create/update/delete vanjskog eventa
  s **reminder override na 08:00 lokalno** (`reminders.overrides` kod Google-a; `reminderMinutesBeforeStart`/
  vremenska logika kod Graph-a). Ovdje je alarm **pun**, ne best-effort.
- **Idempotencija** preko `ExternalCalendarEvent` (StableKey→ExternalEventId + SyncedHash): update samo
  ako se hash promijenio; delete kad obaveza nestane.
- **Noćna rekoncilijacija** (u `DailyAgendaWorker` ili zaseban tick): refresh isteklih tokena, popravak
  drifta, `Status=NeedsReauth` na trajni 401 → notifikacija korisniku "ponovo poveži kalendar".
- **HttpClient** direktno (kao Groq klijenti u projektu) da izbjegnemo teške SDK-ove; odluka SDK vs raw
  HTTP se potvrđuje na početku Faze B.

### Sigurnost tokena

- `AccessTokenEnc`/`RefreshTokenEnc` šifrovani at-rest simetrično (`Calendar:TokenEncryptionKey`, env
  var). Client id/secret provajdera su env var (`appsettings.json` prazni placeholderi — kućno pravilo).
- Odspajanje opoziva token kod provajdera; feed token (Faza A) je nezavisan.

### Plan gating

- `PlanFeature.CalendarSync = 5` (dodaje se u enum). `PlanGuard`: `CalendarSync => effective >= PlanType.Standard`;
  poruka *"Sinhronizacija s Google/Outlook kalendarom je dio plaćenih paketa — nadogradite na Standard."*
- Gate **samo na povezivanju/sync-u**; ICS feed i dnevni podsjetnik ostaju besplatni. Downgrade ne
  briše vanjske evente odmah — sync se pauzira (`Status`), postojeći eventi ostaju (kao SPEC-09: ništa
  se ne zaključava). Feed i dalje radi.

---

## FAZA C — CalDAV / email `.ics` (opciono, po potrebi)

- **CalDAV** (Apple iCloud, Fastmail, Nextcloud): povezivanje app-specific passwordom (korisnik ga
  generiše), `PUT`/`DELETE` `VEVENT`-a na kolekciju. Pokriva iCloud bez OAuth-a (iCloud nema REST API).
  Isti sync engine/mapiranje kao Faza B, drugi transport.
- **Email `.ics` pozivnice** (`METHOD:REQUEST`, `ORGANIZER`/`ATTENDEE`): reuse postojećeg SMTP-a; klijent
  ponudi "Dodaj u kalendar". Univerzalno, ali zatrpava inbox → samo kao izričit opt-in fallback.
- **iCloud korisnici su ionako pokriveni ICS pretplatom (Faza A)** — Faza C je za one koji traže pravi
  push u iCloud.

---

## Config (`appsettings.json`)

```json
"App": {
  "TimeZone": "Europe/Sarajevo",
  "PublicBaseUrl": "https://app.beehive.ba"
},
"Reminders": {
  "DailyAgenda": { "Enabled": true, "LocalHour": 8 }
},
"CalendarFeed": { "Enabled": true, "PastDays": 7, "FutureDays": 120 },
"Calendar": {
  "Google":    { "ClientId": "", "ClientSecret": "" },
  "Microsoft": { "ClientId": "", "ClientSecret": "" },
  "TokenEncryptionKey": ""
}
```

Čita se `IConfiguration` indekserom + ručni parse (bez `Configuration.Binder` zavisnosti, kao SPEC-04).
Odsutan Google/Microsoft ključ → nativni sync se tiho ne nudi (kao SMTP/Groq gating), feed i podsjetnik
rade normalno.

## Frontend

- **Stranica `/settings/calendar`** (`features/calendar/CalendarSettingsPage.tsx`):
  - **ICS pretplata:** feed URL + dugme *Kopiraj*, akordeon uputa po provajderu (Google "Dodaj preko
    URL-a", Apple "Nova pretplata na kalendar", Outlook "Pretplati se s weba"), + *Rotiraj*/*Ugasi*.
    Jasna napomena o periodičnom osvježavanju.
  - **Prekidači kategorija** (hranjenja/todo/tretmani/pregledi) + *Dnevni podsjetnik u 8h*.
  - **Faza B:** dugmad *Poveži Google* / *Poveži Microsoft*, status veze, *Odspoji*. Gejtano planom —
    ako org nije Standard+, prikaži upsell hint umjesto aktivnog dugmeta (postojeći `myPlan` obrazac,
    402→`UpsellModal` kao osigurač).
- **Ulazna tačka:** link "Kalendar & podsjetnici" u padajućem meniju profila i/ili pored `/calendar`.
- **`NotificationBell`** dobija ikonu za `DailyAgenda` (☀️/📅); tekst se renderuje kao i ostale.
- **Modeli/servis/hookovi:** `calendarSettingsService.ts` + hookovi u `queries.ts` (kućno pravilo).

## Sigurnost i privatnost

- Feed token: visoka entropija, heširan u bazi, rotabilan, HTTPS-only, opoziv gašenjem. URL je "tajna
  adresa" — dokumentovati korisniku da ga ne dijeli.
- OAuth: najuži scope (app-created kalendar), tokeni šifrovani at-rest, opoziv pri odspajanju.
- Nema PII-ja u eventima van imena košnica/pčelinjaka i teksta obaveza. Koordinate pčelinjaka u
  `LOCATION` samo ako postoje (opt-in korist "otvori u mapama").
- Feed/agenda poštuju **isti pristup po roli** kao in-app kalendar — korisnik nikad ne vidi tuđe košnice.

## Rubni slučajevi

- Feeding označen završenim → nestaje iz feeda i iz sljedeće agende; (Faza B) briše se vanjski event.
- Diet stopped early / obrisan → pending entries otpadaju (već u `CalendarService`); feed se regeneriše;
  (Faza B) vanjski eventi se brišu.
- Rotacija feed tokena → stari URL 404; korisnik ponovo pretplati novi.
- Korisnik izgubi pristup košnici → njene obaveze otpadaju iz feeda/agende pri sljedećem dohvatu/runu.
- DST granica: 08:00 lokalno ostaje 08:00 i ljeti i zimi (UTC trigger se preračunava po danu).
- Korisnik bez ijedne obaveze danas → agenda se ne šalje; feed je validan prazan `VCALENDAR`.
- `IsSoft` rok (preporučeni pregled) se pomjera kad se unese pregled → self-koriguje se (feed/agenda
  računaju svaki put).
- (Faza B) trajni 401 s provajdera → `Status=NeedsReauth` + notifikacija; sync stane, feed i dalje radi.
- (Faza B) downgrade ispod Standarda → sync se pauzira, postojeći vanjski eventi ostaju (ništa se ne
  zaključava, kao SPEC-09).
- Emoji/`,`/`;`/novi red u imenu obaveze → ICS escaping (test pokriva).

## Van opsega

- **Dvosmjerni sync** (čitanje izmjena iz vanjskog kalendara nazad u app; označavanje "urađeno" iz
  kalendara) — nesrazmjerno skupo (webhooks, konflikti). Jednosmjerno u obje faze.
- **Per-korisnička vremenska zona** i per-korisnički sat podsjetnika (v1 = jedna app-zona, 08:00; šav
  ostavljen na `CalendarSettings`).
- **RRULE (ponavljajući eventi)** — svaki `FeedingEntry` je zaseban `VEVENT` (jednostavnije, tačan
  status po danu).
- **Push realtime za ICS** (nemoguće — klijent kontroliše ritam); realtime tek Faza B.
- Faza C (CalDAV/email) dok se ne pokaže potreba.

## Kriteriji prihvatanja

**Faza A.1 — ICS feed**
- [x] `GET /api/calendar/feed-url` (JWT) generiše token pri prvom pozivu i vraća feed URL (grађen iz
      `Request` hosta, ne iz configa — uvijek tačan javni host); `POST …/feed-url/rotate` poništava stari.
- [x] `GET /api/calendar/feed/{token}.ics` je anoniman (`[AllowAnonymous]`), vraća `text/calendar`;
      writer emituje validan RFC 5545 (CRLF, folding 75, escaping, all-day + `VALARM` u 08:00 lokalno).
      **Owed:** live pretplata u Google/Apple (traži pokrenut server + DB + uređaj).
- [x] Prekidači kategorija (`GET`/`PUT /api/calendar/settings`) izostave odgovarajuće `Kind`-ove
      (`CalendarCategories` proslijeđen u `GatherAsync`).
- [x] Završeni/stopped/nedostupni izvori se ne pojavljuju; prazan feed je validan `VCALENDAR`.
- [x] `IcsWriterTests` (8) — UID stabilnost, escaping, folding bez cijepanja multibyte, DST-ispravan
      UTC trigger (06:00Z ljeti / 07:00Z zimi). Zeleno. Frontend `tsc` + `vite build` prolaze.

**Faza A.2 — Dnevni podsjetnik**
- [x] `DailyAgendaWorker` se budi u 08:00 `App:TimeZone` (DST-ispravno, guard za spring-forward gap);
      `DailyAgendaService` šalje jednu konsolidovanu `DailyAgenda` (19) po korisniku (zvono + email
      preko `NotifyAsync`), dedup preko `ExistsRecentAsync` s `yyyyMMdd` dedupId.
- [x] Korisnik bez obaveza danas ne dobije ništa; `DailyAgendaEnabled=false` i SystemAdmin se preskaču.
- [x] `DailyAgendaServiceTests` (5) — šalje kad ima obaveza, dedup suzbija, prazan dan šuti, opt-out i
      SystemAdmin preskočeni. Zeleno.

> **Odstupanja od plana (implementacijske odluke):** (1) feed token se čuva **plaintext** (opaque 256-bit,
> unique index), ne heširan — jer URL mora biti ponovo prikaziv korisniku ("secret address" model, kao
> Google-ov privatni iCal URL); nisko-osjetljiv, rotabilan. (2) Feed URL se gradi iz `Request` hosta, pa
> `App:PublicBaseUrl` služi samo za deep-linkove u `DESCRIPTION` i UID host. (3) `CalendarService`
> refaktorisan da dijeli `ICalendarAccessResolver` s feedom/agendom (bez promjene ponašanja `/events`).

**Faza B — Nativni OAuth (kad se radi)**
- [ ] Google i Microsoft connect/callback kreiraju "Melarium" kalendar i urade initial sync; izmjena
      diet-a se za < 1 min odrazi (create/update/delete) s alarmom u 08:00; idempotentno (bez duplikata).
- [ ] `PlanFeature.CalendarSync` gejta connect/sync na Standard+ (402 + upsell); ICS/agenda ostaju
      besplatni; downgrade pauzira sync bez brisanja postojećih eventa.
- [ ] Token refresh radi; trajni 401 → `NeedsReauth` + notifikacija; odspajanje opoziva token i briše
      "Melarium" kalendar; tokeni šifrovani at-rest.

**Dokumentacija (na kraju svake faze)**
- [ ] `docs/features/calendar-sync.md`, `api-contracts.md`, `context.md`, `decisions.md` (ADR:
      jednosmjerno + ICS-first + timezone), README index → status faze, ovaj spec → ✅.

> **Napomene van koda (Faza B, prije javnog lansiranja):** Google OAuth **verifikacija** za "sensitive"
> scope (brand + demo video; neverifikovana app = cap 100 korisnika + upozoravajući ekran) — planirati
> unaprijed. Microsoft **publisher verification**. Politika privatnosti mora pokriti pohranu OAuth
> tokena i pristup kalendaru. Feed token dokumentovati korisniku kao tajnu adresu.
