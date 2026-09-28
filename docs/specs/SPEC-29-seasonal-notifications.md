# SPEC-29 — Sezonske notifikacije i početna stranica ("Seasonal notifications & dashboard")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-09-26) — see `features/seasonal-notifications.md`, `features/dashboard.md` |
| **Effort** | L (~2 dana) |
| **Depends on** | SPEC-04 (pravila upozorenja), SPEC-11 (agenda, ICS), SPEC-22 (organizacija), SPEC-24 (zaključavanje) |
| **New secrets / packages** | nema |
| **Breaking** | Ponašanje: svako obavještenje više ne ide automatski e-mailom (vidi D6). Šema: aditivna — dvije kolone s defaultom 0 i jedna nova tabela |

## Cilj

Pametna upozorenja (SPEC-04) i dnevna agenda (SPEC-11) radila su isto cijele godine. Zimi, kad se
košnice ne otvaraju, pčelar je svake sedmice dobijao „Košnica bez pregleda" za **svaku košnicu
posebno** (u aplikaciji i e-mailom), svaka 3 dana „Najavljen mraz" iako je mraz zimi normalan, a
agenda mu je nudila „preporučeni pregled". Takva buka navikava čovjeka da ignoriše i važna
upozorenja.

Ovaj spec uvodi **sezonski kalendar** pčelarske godine, **jednu politiku** po tipu i fazi koju koriste
svi putevi, **grupisanje po pčelinjaku**, **prioritete** s jednim jutarnjim e-mailom i **korisnička
podešavanja**. U istom radu nastala je **početna stranica (dashboard)** — Asim ju je predložio na
pitanje gdje prikazati fazu i odlučio da ide u ovaj feature.

Rezultat je manje obavještenja, ali svako ima razlog.

## Odluke (dogovorene s Asimom prije koda, 2026-09-25/26)

Prompt je bio propisan; otvorena pitanja su riješena u tri kruga pitanja, ostalo je predloženo i
nije osporeno. Spec je pisan posljednji, kao zapis dogovorenog.

### D1 — Pomak sezone: proljeće kasni, jesen rani

Jedan broj po organizaciji, −14…+30 dana. Proljetne granice (16.02., 16.04.) se pomjeraju **+N**,
jesenje (01.10., 15.11.) **−N**, a 01.08. ostaje. Na visini proljeće kasni **i** zima dolazi ranije.

Odbijeno: „sve granice isto" (doslovno iz prompta) — na +21 bi zima počela 06.12., *kasnije* nego u
nizini ispod. Razmatrana i dva odvojena broja (proljeće, jesen); odbijeno kao višak za v1.

### D2 — Jedan jutarnji e-mail

U 08:00 jedan e-mail po korisniku: obična upozorenja iz jutarnjeg skeniranja + današnje obaveze
(agenda). Kritična stižu odmah, u trenutku skeniranja. Odbijeno: dva e-maila (zbirni odmah nakon
skeniranja + agenda u 08:00) — dva jutarnja e-maila su upravo buka koju spec gasi.

### D3 — E-mail režim važi za sve; zbirni e-mail i prekidači samo za upozorenja

- **Sva** (default): kritična odmah; upozorenja i agenda u jutarnjem e-mailu; ono što pokrene čovjek
  (novi zadatak, dodjela, odgovor na prijedlog) i dalje odmah, kao do sada.
- **Samo kritična**: samo Critical.
- **Isključeno**: ništa — kritična ostaju u aplikaciji.
- **Sigurnosna** (`PasswordChanged`, `AccountCreated`, `OrganizationOwnershipTransferred`) idu
  **uvijek**: napadač koji preuzme račun ne smije moći ugasiti jedinu poruku koja bi vlasnika upozorila.

Prekidači „Obična upozorenja" i „Savjeti" u aplikaciji važe samo za upozorenja iz skeniranja;
kritična nemaju prekidač (prikazan je uključen i zaključan). Odbijeno: „sve po jednoj tabeli" (i
novi zadatak bi čekao jutro) i „samo upozorenja" (e-mail isključen a zadaci i dalje stižu).

### D4 — Tabela politike (predložena, prihvaćena)

| Upozorenje | Zima | Proljetni razvoj | Glavna sezona | Ljetno-jesenja | Zazimljavanje |
|---|---|---|---|---|---|
| Košnica bez pregleda | — | obično, 30 d | obično, 21 d | obično, 30 d | obično, 45 d |
| Mraz < 0 °C (48 h) | samo < −15 °C, obično | **kritično** | **kritično** | obično | samo prvi, jednom |
| Opada nivo meda | — | obično | obično | obično | obično |
| Stara matica | — | savjet, jednom | — | — | — |

Trake, karenca, kašnjenje prehrane i runde tretmana: **uvijek**, obično — rad koji je pčelar sam
pokrenuo niko drugi neće podsjetiti. Paket ističe: obično; zaključavanje za 2 dana: kritično.
Dedup „bez pregleda": 7 dana u glavnoj sezoni, 14 van nje. Početak faze: obično, jednom po fazi.

### D5 — Zimski dani se ne broje

Sat „bez pregleda" počinje ponovo prvog dana proljeća. Inače bi 16.02. **svaka** košnica odjednom bila
„nepregledana 120 dana" — upravo buka koju spec gasi, i to u februaru kad se košnice ne otvaraju.
Isto važi za preporučeni pregled u agendi i ICS-u.

### D6 — Grupisanje po pčelinjaku, po primaocu

Jedno obavještenje po (primalac, tip, pčelinjak); poruka nabraja **samo košnice tog primaoca** —
administratori sve, pčelar samo dodijeljene. Dedup je po pčelinjaku, pa košnica koja postane
zakašnjela nakon podsjetnika ulazi u sljedeći, a ne u drugi ove sedmice.

### D7 — Početak faze samo u prvih 14 dana faze

`SeasonPhaseStarted` ide svim članovima organizacije, jednom po fazi, i samo ako je faza počela
najviše 14 dana ranije. Deploy 25.09. zato ne šalje zakašnjelo „počinje ljetno-jesenja priprema"
(od 01.08.), nego 01.10. stiže „zazimljavanje". Klik vodi u Edukacija → Sezonski radovi.

### D8 — Zimi mjesečni AI pregled

Prvog ponedjeljka u mjesecu, pokriva cijeli protekli mjesec, naslov „Mjesečni pregled".

### D9 — Brojevi (sudari sa stashovima)

`NotificationType.SeasonPhaseStarted = 32` (29/30 zauzeti na `main`, 31 = `AchievementExpiring` u
SPEC-27 stashu). ADR-046/047 (045 je u SPEC-27 stashu). SPEC-29 (27 i 28 su u stashovima).

### Dashboard (D10–D13)

- **D10 — Nova početna.** `/` je dashboard za sve osim SystemAdmina (on ostaje na `/admin`);
  „Pčelinjaci" ostaje posebna stavka menija.
- **D11 — Blokovi:** pozdrav + sezonski koraci; brojke sezone; traži pažnju; obaveze danas + 7 dana;
  vrijeme po pčelinjaku; otvoreni zadaci; paket (samo vlasnik); stanje košnica; programi u toku;
  prinos po mjesecima; brze akcije; aktuelno u Edukaciji.
- **D12 — Sezonska kartica prikazuje samo fazu i datum sljedeće** (bez liste radova — ta stiže kao
  obavještenje). Asim je odbio i „Prvi koraci" na dashboardu; kartica ostaje na „Pčelinjaci".
- **D13 — Okrugli grafikoni s brojem u sredini** („34/42", „4/8", „42/100") gdje postoji prirodni omjer.

Predloženo i **odgođeno**: „Matice po starosti" (međunarodne boje oznaka) — može se dodati bez
promjene modela.

## Dizajn

### Sezonski kalendar — izveden, ne pohranjen

`ISeasonCalendar` (`Application/Common/Seasons`) računa fazu iz **lokalnog datuma** (`App:TimeZone`)
i `Organization.SeasonShiftDays`. Ništa ne pamti fazu; prvog dana proljeća ne mora se okrenuti
nijedna zastavica (precedent: efektivni paket, karenca). Granice su u `Alerts:Seasons` kao `MM-dd`;
konfiguracija koja bi na nekom kraju raspona pomaka tekla unazad zamjenjuje se defaultima **u
cjelini**. Vidi ADR-046.

### Jedna politika

`INotificationPolicy` je tabela `(tip, faza) → (radi li, prag, dedup, prioritet)` plus kanali
(`ShouldEmailNow`, `ShowInApp`) i sat pregleda (`IsInspectionOverdue`, `InspectionBecomesDue`).
Koriste je `AlertRuleService`, `WeeklySummaryService`, `CalendarObligationService` (agenda + ICS),
`NotificationService` i dashboard. Uslovi pravila (trake, runde, hranjenje, med) su u
`AlertConditions`, zajednički skeniranju i dashboardu.

### Isporuka

`Notification.Priority` se **pohranjuje** (isti tip je kritičan u aprilu, običan u avgustu).
`NotificationService.NotifyAsync` sam odlučuje: prikaz u aplikaciji po prekidačima, e-mail odmah po
politici. Jutarnji e-mail sastavlja `DailyAgendaService` iz `GetNormalSinceAsync(now − 24 h)` +
današnjih obaveza (`MorningEmail.Compose`). Vidi ADR-047.

### Dashboard

`GET /api/dashboard` (jedan poziv) i `GET /api/dashboard/weather` (zaseban, da spora prognoza ne
drži stranicu). Opseg dolazi iz `IAccessGuard.GetAccessible*` — već po ulozi i bez zaključanih
(ADR-043 obrazac, nije osmi ručno filtrirani agregat). „Traži pažnju" se računa **uživo** istom
politikom, pa se prazni čim je posao urađen. Prognoza se kešira 60 min na serveru
(`WeatherForecastCache`, i za skeniranje i stranicu pčelinjaka); trenutna temperatura pregleda ostaje živi poziv.

## Model i migracija

`20260925223735_AddSeasonalNotifications` — `Organizations.SeasonShiftDays int NOT NULL DEFAULT 0`,
`Notifications.Priority int NOT NULL DEFAULT 0` (0 = Normal, pa postojeći redovi ne trebaju
backfill), tabela `NotificationSettings` (jedan red po korisniku, lijeno, kaskadno brisanje).

## API

| Metoda | Putanja | Ko |
|---|---|---|
| GET | `/api/dashboard` | svi osim SystemAdmina (403) |
| GET | `/api/dashboard/weather` | isto |
| GET / PUT | `/api/notifications/settings` | vlastita podešavanja |
| PUT | `/api/organizations/my` | + opcionalno `seasonShiftDays` (null = ne mijenja) |
| GET | `/api/organizations/my` | + `seasonShiftDays`, `seasonPhases` |
| GET | `/api/notifications` | + `priority` na svakoj stavci |

## Van obima (namjerno)

Push notifikacije (SPEC-23), podešavanja tip × kanal za svaki tip pojedinačno (SPEC-28 L-01 je
predlagao; ovo je grublja verzija koju je Asim izabrao), per-korisnička vremenska zona, matice po
starosti na dashboardu, prevod novih tekstova (i18n je samo u stashu).

## Prihvatni kriteriji

- [x] Zimi nema `InspectionOverdue` ni preporučenog pregleda u agendi/ICS-u — `AlertRuleServiceTests`, `CalendarObligationSeasonTests`
- [x] Trake i karenca zimi i dalje stižu — `InWinter_StripsAndKarenca_StillArrive`
- [x] Mraz zimi ne stiže, u aprilu stiže kao Critical; ekstremna hladnoća zimi stiže
- [x] Pomak organizacije pomjera granicu faze — `SeasonCalendarTests`, `OrganizationShift_MovesTheBoundary_ForTheSameDay`
- [x] Pet košnica bez pregleda na jednom pčelinjaku = jedno obavještenje; pčelar vidi samo svoje
- [x] Obična upozorenja iz jednog skeniranja = jedan e-mail po korisniku — `DailyAgendaServiceTests`
- [x] `SeasonPhaseStarted` se ne ponavlja u istoj fazi i ne šalje sedmicama kasno
- [x] Granice faza u lokalnoj zoni (23:30 UTC 14.11. = zima)
- [x] Provjera uživo na lokalnoj bazi: API (19 scenarija), pravo skeniranje + jutarnji e-mail kroz harness, dashboard/profil/organizacija/zvono u pregledniku, 375 px i obje teme
- [ ] Produkcija: migracija, i prvi stvarni jutarnji e-mail preko Resenda (lokalno SMTP nije podešen)
