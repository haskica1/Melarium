# SPEC-16 — Aktivnost i status organizacije (Organization activity & status)

| | |
|---|---|
| **Status** | 🔨 Djelimično — kolona "Zadnja aktivnost" isporučena 2026-08-17 **drugom metodom** (vidi ↓); ostatak Faze A, te B i C, i dalje planirani |
| **Effort** | M (~2–3 days) — jedna kolona aktivnosti + heartbeat, jedan izračunati status, jedan ručni prekidač, popravka brisanja |
| **Depends on** | ništa novo; koristi SPEC-09 (`PlanHelper`), ADR-021 (queue + worker), ADR-027 (`IFileStorage`), `ISessionRevoker` |
| **New secrets / packages** | nema. Samo config: `Activity:*` |
| **Breaking** | ne. Jedina promjena ponašanja je namjerna: korisnik deaktivirane organizacije se ne može prijaviti |
| **Ništa se ne briše automatski** | nema workera koji briše, nema odbrojavanja, nema e-maila korisniku. Sistem samo **označi**; briše čovjek. |

> ### ⚠️ Izmjena 2026-08-17 — §3 je zamijenjen, ne implementiran
>
> Kolona **"Zadnja aktivnost"** (i uz nju **broj košnica**) je isporučena, ali **bez ijedne stvari
> iz §3**: nema `LastActivityAt` kolone, nema `IActivityTracker`-a, nema `ActivityTrackingWorker`-a,
> nema `ActivityTrackingMiddleware`-a, nema `Activity:*` konfiguracije, nema migracije.
>
> Vrijednost se **računa pri čitanju** iz samih podataka: `MAX(UpdatedAt ?? CreatedAt)` po
> organizaciji preko četrnaest tabela koje organizacija posjeduje, plus `RefreshToken.CreatedAt`
> (prijava i rotacija sesije — signal zbog kojeg je §3 uopšte htio heartbeat).
>
> **Zašto:** §3.3 sam navodi slabost pohranjene kolone — kreće prazna i prvih 90 dana ne govori
> ništa o prošlosti. Izračun iz podataka opisuje **cijelu postojeću historiju od prvog dana**, a za
> tabelu čija je svrha odlučivanje o *starim* organizacijama to je sama funkcija, ne detalj.
> Puno obrazloženje: **ADR-034**. Opis implementacije: `docs/features/org-activity.md`.
>
> **Šta iz ovog speca i dalje NIJE napravljeno:** `IsActive` + ručni prekidač i blokada prijave
> (§6, Faza B), `FirstPaidAt` (§4), izračunati `OrgStatus` badge Aktivna/Uspavana/Za brisanje (§4),
> radne liste za naplatu (§5), `IOrgPurgeService` i popravka brisanja (§7, Faza C).
> §0, §4–§7 i §10–§13 ostaju važeći za taj ostatak.
>
> **Ko nastavi Fazu A — ne vraćati `LastActivityAt` kolonu.** Izračunati status iz §4 treba čitati
> `lastActivityAt` iz DTO-a, koji već postoji.

## 0. Odluke (Asim, 2026-07-31)

| # | Odluka |
|---|---|
| D1 | **Prag je 90 dana.** Korištena u zadnjih 90 dana = Aktivna. Preko 90 dana bez korištenja = kandidat za brisanje. |
| D2 | **Sistem ne briše ništa.** Samo prikazuje status u admin tabeli organizacija. Brisanje je uvijek ručna radnja poslije provjere. |
| D3 | **Organizacija koja plaća nikad ne dobije oznaku "Za brisanje"** — dobije "Uspavana". Dvije različite stvari u istoj tabeli: klijent kojeg treba nazvati vs. napušten probni nalog. |
| D4 | **SystemAdmin može organizaciju postaviti na "Neaktivna"** — tada se niko iz te organizacije ne može prijaviti. |
| D5 | **Deaktivacija je trenutna** — blokira se i prijava i refresh, a postojeće sesije se odmah poništavaju (`ISessionRevoker`, isti mehanizam kao promjena lozinke). |
| D6 | **Brisanje organizacije iz admin UI-ja se popravlja** da stvarno radi (korisnici + svi podaci + fajlovi iz bucketa), uz upisivanje tačnog imena kao potvrdu. |

---

## 1. Šta ovo jeste, a šta nije

**Jeste:** kolona u admin tabeli organizacija koja odgovara na pitanje *"koristi li se ova organizacija?"* — što danas nijedan dio sistema ne zna, plus prekidač kojim SystemAdmin može zaključati organizaciju.

**Nije:** automatsko brisanje, odbrojavanje, upozorenja korisniku, retention politika koja se sama izvršava. Sve to je bilo u prvoj verziji ovog speca i **namjerno je izbačeno** (D2) — sistem koji sam briše tuđe podatke traži pravni okvir, dry-run i tri sigurnosne brave; sistem koji samo označi ne traži ništa od toga, a odgovara na isto pitanje.

### Zašto "aktivna" nisu dvije stvari nego tri

Danas postoji samo **komercijalni** status (`Plan` + `PlanValidUntil`, efektivni paket preko `PlanHelper` — SPEC-09). Ne postoji **nijedna** kolona koja bilježi korištenje: nema `LastLoginAt`, `LastSeenAt`, ničega (provjereno kroz cijeli backend). Ovaj spec dodaje drugu osu (korištenje) i treću, ručnu (zaključana / nije zaključana):

| | Aktivna | Neaktivna (ručno) |
|---|---|---|
| **korištena < 90 dana** | normalno stanje | zaključana premda se koristila — npr. nije platila |
| **> 90 dana bez korištenja, plaća** | **Uspavana** — nazvati prije obnove | zaključana i mirna |
| **> 90 dana bez korištenja, ne plaća** | **Za brisanje** — kandidat za tvoju provjeru | zaključana, i dalje kandidat |

Zato je "Neaktivna" (ručna) **odvojena riječ** od "Uspavana"/"Za brisanje" (izračunato). Da su obje zvale isto, tabela bi mogla reći "neaktivna" za organizaciju koja radi svaki dan a nije platila, i za onu koja se ne koristi godinu dana — dva potpuno različita poteza s tvoje strane.

---

## 2. Model podataka

```
Organization += LastActivityAt   DateTime?    // zadnji znak korištenja; null = nikad viđeno
Organization += IsActive         bool         // default true; false = prijava blokirana
Organization += DeactivatedAt    DateTime?    // kad je isključena (bez ovoga "isključena davno" nema traga)
Organization += FirstPaidAt      DateTime?    // je li ikad bila klijent — vidi §4
```

Migracija `AddOrganizationActivityStatus`. Sve aditivno; `IsActive` dobija `HasDefaultValue(true)` pa postojeći redovi ostaju uključeni.

**Razlog za `IsActive` kao kolonu a ne status enum u bazi:** status koji se prikazuje je izračunat (§4), a jedina stvar koju čovjek stvarno *postavlja* je ovaj prekidač. Enum u bazi bi značio da neko mora održavati "Uspavana"→"Aktivna" prelaze kad se korisnik vrati — a taj prelaz se mora dogoditi sam.

**Zašto nema `User.LastSeenAt`:** tražena je informacija o **organizaciji**, ne o pojedincu. Per-user "zadnji put viđen" je zasebna (i osjetljivija) funkcija — admin organizacije koji gleda kad mu se pčelari prijavljuju. Namjerno izostavljeno (§12).

**Razlog za `FirstPaidAt` (jedina kolona koju nisi tražio):** bez nje se "nikad nije platila" i "platila pa prestala" ne razlikuju, pa bivši klijent koji je pauzirao sezonu dobije oznaku "Za brisanje". Kolona je nullable, puni se na jednom mjestu (§4) i ako je ne želiš — izbaci je, a pravilo statusa onda gleda samo trenutni efektivni paket. Ništa drugo ne ovisi o njoj.

---

## 3. Mjerenje aktivnosti

### 3.1 Pravilo: heartbeat nikad ne usporava zahtjev

Ista pravila po kojima radi `EmailNotificationWorker` (ADR-021 — *SMTP nikad ne blokira zahtjev*): upis aktivnosti ide u red, worker ga isprazni, a nijedan zahtjev ne čeka na to i nijedan GET ne postaje upis.

```
IActivityTracker              (Application/Common/Interfaces)
  Track(int organizationId)                      // fire-and-forget

ActivityTrackingWorker        (Infrastructure/Activity, BackgroundService)
  prazni Channel, spaja duplikate po organizationId (zadnji pobjeđuje),
  flush svakih Activity:FlushSeconds (60), svaki ciklus u svom DI scope-u
```

Tri izvora, sva tri gađaju istu kolonu:

| Signal | Gdje | Zašto tu |
|---|---|---|
| prijava | `AuthService.LoginAsync` (`AuthService.cs:48`) | očigledno |
| rotacija refresh tokena | `AuthService.RefreshAsync` (`AuthService.cs:154`) | prijava sama je loš signal — refresh token traje 14 dana, pa se korisnik koji aplikaciju koristi svaki dan prijavljuje dvaput godišnje. Rotacija se dešava najkasnije svakih 30 min dok je aplikacija u upotrebi. |
| bilo koji upis | `ActivityTrackingMiddleware` (`Melarium.API/Middleware/`) — 2xx odgovor na non-GET zahtjev prijavljenog korisnika koji ima `organizationId` claim | jedno mjesto koje hvata svaki upis u aplikaciji. Nijedan servis ne mora znati da ova funkcija postoji. Isti oblik kao `SecurityHeadersMiddleware`. |

**Throttle.** Worker upisuje samo ako je pohranjena vrijednost starija od `Activity:ThrottleHours` (6). Gornja granica: 4 UPDATE-a po organizaciji dnevno, bez obzira na promet. Dvadeset pregleda unesenih zaredom = jedan upis.

**Ortodoksno.** Worker učitava entitet kroz repozitorij i zove `IUnitOfWork.SaveChangesAsync()` kao i sve ostalo — bez `ExecuteUpdate`, bez raw SQL-a, bez zaobilaženja UoW-a (pravila projekta). Uz nekoliko redova po ciklusu cijena je nebitna.

**Greška je tiha.** Izgubljeni heartbeat nije greška vrijedna loga iznad `Debug` — najgori ishod je da organizacija izgleda mirnija nego što jeste, a čovjek ionako provjerava prije brisanja. Pun kanal odbacuje **najstariji** unos, nikad ne blokira pozivaoca.

### 3.2 Poznata slabost, svjesno prihvaćena

Zaboravljen otvoren tab drži organizaciju "aktivnom" zauvijek: frontend anketira notifikacije svakih 30 s, što nakon isteka access tokena povuče refresh, a refresh je heartbeat. Alternativa je brojati **samo** upise, ali onda organizacija koju neko svakodnevno *čita* (kalendar, prognoza, evidencije) poslije 90 dana ispadne "Za brisanje".

Biramo grešku u sigurnijem smjeru: **radije napuštena organizacija koja izgleda živa (ništa se ne izgubi — ti je jednostavno ne obrišeš) nego živa koja izgleda napuštena** (čovjek vjeruje oznaci i obriše tuđe podatke). Ako ikad zasmeta, ograničavanje heartbeata na non-GET je izmjena od jedne linije.

### 3.3 Postojeći redovi

Migracija ostavlja `LastActivityAt = null`, a null znači **"nikad zabilježeno"**, ne "neaktivno od pamtivijeka": helper pada na `Organization.CreatedAt`. Posljedica koju treba znati unaprijed — organizacija kreirana prije godinu dana i još neviđena odmah nakon deploya prikazuje "Za brisanje". To je tačno stanje stvari (ništa je ne dokazuje živom), ali **prvih 90 dana nakon deploya tabelu treba čitati s tom rezervom**, jer kolona tek počinje skupljati podatke. Nakon toga svaka oznaka je stvarno mjerena.

---

## 4. Izračunati status

`Domain/Common/OrgStatusHelper.cs` — čista funkcija, bez I/O, po uzoru na `PlanHelper`:

```csharp
OrgStatus Compute(
    bool isActive, DateTime? lastActivityAt, DateTime createdAt,
    PlanType effectivePlan, DateTime? firstPaidAt,
    int inactiveAfterDays, DateTime utcNow)
```

```
OrgStatus            BsLabel        Pravilo (dani od lastActivityAt ?? createdAt)
  Active      = 1    "Aktivna"      < 90
  Dormant     = 2    "Uspavana"     ≥ 90  I  (efektivni paket ≠ Free  ILI  firstPaidAt != null)
  ForDeletion = 3    "Za brisanje"  ≥ 90  I  nikad nije platila
  Inactive    = 4    "Neaktivna"    isActive = false — prekriva sve ostalo
```

- **Izračunat, nikad pohranjen** (ADR-028: `PlanHelper`, `TreatmentStatusHelper`). Zato povratak korisnika sam vraća status na "Aktivna" — nema joba koji išta prebacuje i nema stanja koje može zaostati.
- Datumi se porede, ne trenuci.
- **DTO nosi oba**: `status` (spojeno, za prikaz) i `activityStatus` (izračunato, ignoriše `IsActive`). Bez ovoga bi deaktiviranje organizacije **sakrilo** taj red iz liste "Za brisanje" — deaktivirao bi je da je zaključaš, i time izgubio iz vida red koji si htio obrisati. Filter "Za brisanje" gleda `activityStatus`.

`FirstPaidAt` se puni na tačno jednom mjestu: `AdminService.UpdateOrganizationPlanAsync` (`AdminService.cs:71`), kad je `dto.Plan` jedan od `Standard | Pro | Max` a `FirstPaidAt` još null. Probni period postavlja `Pro` kroz `RegisterAsync`, drugim putem — pa se probni nalozi nikad ne označe kao plaćeni. Migracija ga zaštitnički popunjava (`FirstPaidAt = CreatedAt`) za svaku organizaciju koja **trenutno** ima ne-Free paket a `PlanNotes` joj nije `"Probni period"`.

Testovi `OrgStatusHelperTests`: granice 89/90 dana, `lastActivityAt = null` → fallback na `CreatedAt`, plaćena → nikad `ForDeletion`, bivši klijent → `Dormant`, `isActive = false` → `Inactive` bez obzira na sve.

---

## 5. Naplata — radne liste nad podacima koji već postoje

Ovo je druga polovina ideje (*"zbog naplaćivanja"*) i najjeftiniji dio cijelog speca: **nijedna nova kolona, nijedan novi endpoint.** Polja paketa postoje od SPEC-09 (`Plan`, `PlanValidUntil`, `PlanNotes`) i admin lista ih već vraća. Nedostajale su dvije stvari: same liste, i kolona aktivnosti koja kaže **hoće li se ta obnova uopšte desiti**.

| Lista | Filter | Čemu služi |
|---|---|---|
| **Ističe uskoro** | efektivni paket ≠ Free i `PlanValidUntil` unutar 30 dana, sortirano rastuće | Koga fakturisati. Naplata je u v1 ručna i godišnja (SPEC-09) — danas ti ništa ne kaže kome ističe dok ne prođeš tabelu redom. |
| **Probni period** | `PlanNotes = "Probni period"` i `PlanValidUntil` u budućnosti, sortirano rastuće | Trenutak konverzije, 30 dana od registracije. |
| **Istekli** | pohranjeni paket ≠ Free, a efektivni = Free | Plaćali pa nisu obnovili — izgubljen prihod, lista za povrat. |

### Zašto ovo pripada baš ovom specu

Same po sebi, te tri liste su filter nad SPEC-09 podacima i mogle su nastati bilo kad. Ono što ih čini upotrebljivim je **presjek s aktivnošću**, koji do sad nije postojao:

- Klijent kojem paket ističe za 20 dana a aplikaciju nije otvorio 4 mjeseca **neće obnoviti** — to je poziv koji se vodi drugačije (ili se ne vodi).
- Probni period koji je stvarno korišten je jedini koji vrijedi zvati; onaj u kojem je uneseno nula pregleda je hladan kontakt.

Zato svaki red u ovim listama prikazuje i **"Zadnja aktivnost"** — kolonu koju uvodi §3. To je cijeli razlog zašto su naplata i aktivnost jedan spec a ne dva.

**"Rizik odustajanja" nema svoju listu** jer već postoji: to je čip **Uspavana** (§4) — po definiciji organizacija koja plaća a ne koristi se. Dupla lista za isti red nema smisla.

**Ne dupliramo `PlanExpiring`** (NotificationType 18, SPEC-09): taj alarm upozorava **korisnika** da mu paket ističe. Ove liste su za **tebe**, kroz admin tabelu — druga publika, drugi kanal, nijedna nova notifikacija.

### Implementacija

Filteri su **klijentski**, nad listom koju `GET /api/admin/organizations` ionako vraća u cijelosti (`AdminDashboardPage.tsx` već tako radi pretragu). Backend dodaje **jedno polje u DTO**: `effectivePlan`, izračunat kroz `PlanHelper.Effective` — da frontend ne bi ponovo pisao logiku isteka koja po ADR-028 živi na jednom mjestu.

Na dashboardu jedna `VitalCard`: **"Ističe u 30 dana"**, pored postojećih.

---

## 6. Ručni prekidač — "Neaktivna" blokira prijavu

### 6.1 Provjera na dva mjesta

| Putanja | Ponašanje | Zašto tako |
|---|---|---|
| `AuthService.LoginAsync` | nakon uspješne provjere lozinke: `user.Organization is { IsActive: false }` → `ForbiddenAccessException("Vaša organizacija je deaktivirana. Kontaktirajte podršku.")` → **403** | Provjera ide **poslije** lozinke, pa se ne može koristiti da se ispita koja je organizacija zaključana. 403 (a ne 401) jer 401 na frontendu ulazi u mašineriju za refresh i odjavu (`apiClient.ts`, zamrznuto u `ignore.md`) — 403 nosi poruku ravno u formu za prijavu. |
| `AuthService.RefreshAsync` | ista provjera → `UnauthorizedException` → **401** | Ovdje 401 **jeste** ispravan: interceptor tada odjavi korisnika i vrati ga na `/login`, gdje sljedeći pokušaj dobije 403 s razlogom. Postojeće ponašanje se ne dira. |

Obje putanje već učitavaju organizaciju (`GetByEmailAsync` / `GetByPhoneAsync` / `GetByIdWithOrganizationAsync` sve rade `.Include(u => u.Organization)`), pa provjera ne košta nijedan dodatni upit.

SystemAdmin nema organizaciju (SPEC-09) → nikad ne može zaključati sam sebe. `RegisterAsync` kreira novu organizaciju, uvijek aktivnu.

### 6.2 Deaktivacija je trenutna (D5)

`PUT /api/admin/organizations/{id}/status` s `{ isActive: false, note? }`:

1. `IsActive = false`, `DeactivatedAt = UtcNow`, `note` se upisuje u postojeći `PlanNotes` (ručno knjigovodstvo — SPEC-09 već koristi to polje; nema nove kolone).
2. Za svakog korisnika organizacije: `ISessionRevoker.RevokeAllAsync(userId)`.
3. Jedan `SaveChangesAsync()` — interface izričito kaže da transakciju drži pozivalac.

Bez koraka 2 korisnik s otvorenom sesijom radi još do 30 minuta (access token), a to je pola sata rada koji treba objasniti. Ponovno uključivanje (`isActive: true`) briše `DeactivatedAt`; korisnici se prijavljuju normalno, ništa se ne obnavlja automatski.

---

## 7. Popravka brisanja organizacije (D6)

### 7.1 Zašto brisanje danas ne radi

`AdminService.DeleteOrganizationAsync` (`AdminService.cs:88`) **odbija** organizaciju koja ima ijednog korisnika, a `Organization → Users` je `Restrict` — pa realna organizacija nikad nije obrisiva iz UI-ja. Ispod toga su tri zamke, sve provjerene u `Melarium.Entity/Configurations/`:

1. **`Todo.AssignedToId` je `NoAction`** (`TodoConfiguration.cs:37-41`), a komentar iznad njega kaže da servis mora očistiti dodjelu prije brisanja korisnika — **nijedan servis to ne radi**. `AdminService.DeleteUserAsync` (`:432`) briše korisnika direktno, pa brisanje korisnika kojem je dodijeljen todo puca na FK **već danas**, neovisno o ovom specu.
2. **`Todo.ApiaryId` je `NoAction`** (`:45-49`); todo-e na nivou pčelinjaka briše `ApiaryService.DeleteAsync`, a kaskada `Organization → Apiary` taj kod ne pokreće. Direktno brisanje organizacije puca na njima.
3. **Fotografije su blobovi izvan baze.** `InspectionPhoto` redovi nestanu s košnicom i s njima `StoragePath` ključevi — objekti ostaju u bucketu zauvijek, naplativi i više nedohvatljivi. Ključevi se moraju pokupiti **prije** brisanja, a blobovi obrisati **poslije** commita.

### 7.2 Redoslijed

`POST /api/admin/organizations/{id}/delete`, tijelo `{ "confirmName": "<tačno ime organizacije>" }`; neslaganje = 400.

> POST a ne `DELETE`: potvrda ide u tijelo, ne u URL. Ime organizacije često sadrži nečije prezime, a lični podaci ne idu u query string (završe u logovima proxyja).

```
 1. Pokupi ključeve blobova: InspectionPhoto.StoragePath svih fotografija svih košnica
                             + Feedback.ScreenshotPath za feedback redove korisnika te organizacije
 2. Transakcija:
      a. Todo.AssignedToId = null za todo-e dodijeljene korisnicima te organizacije
      b. obriši Todo-e čiji je ApiaryId jedan od pčelinjaka te organizacije
      c. obriši korisnike  (kaskada: RefreshToken, UserToken, CalendarSettings, UserBeehive,
                            Notification, AdvisorConversation → AdvisorMessage, LearningTopicRead)
      d. obriši organizaciju (kaskada: Apiary → Beehive → Inspection → InspectionPhoto,
                              Diet → FeedingEntry, Queen, Harvest, Treatment, ApiaryMove,
                              te Pasture i Expense → ExpenseItem)
    → jedan IUnitOfWork.SaveChangesAsync()
 3. Poslije commita: obriši blobove (idempotentno, log-and-continue — zaostali blob ne vrijedi
    poništavanja završenog brisanja)
```

`Feedback` redovi preživljavaju po dizajnu — FK-ovi su im `SetNull` (`FeedbackConfiguration.cs:47-57`), pa se sami anonimiziraju i ostaju naš operativni zapis. **Njihovi screenshotovi ne preživljavaju**: slika tuđih evidencija je upravo ono što je brisanje trebalo ukloniti. Tekst ostaje, slika ne.

Isti servis (`IOrgPurgeService`) je ujedno i odgovor na zahtjev korisnika *"obrišite moje podatke"* (ZZLP / GDPR) — ista radnja, isti put, bez posebnog koda.

### 7.3 Popravka usput

Korak 2a rješava i zatečeni bug iz §7.1 #1, ali samo za putanju brisanja organizacije. **Istu liniju treba dodati i u `DeleteUserAsync`** — inače brisanje pojedinačnog korisnika i dalje puca. To je jedan `foreach` i pripada ovom poslu jer je isti uzrok.

### 7.4 Zapis o brisanju (opciono — reci ako ne treba)

Jedna tabela bez FK-a prema organizaciji, da nešto preživi brisanje: `OrganizationDeletionLog { OrganizationId, OrganizationName, DeletedByUserId?, LastActivityAt?, UserCount, ApiaryCount, BeehiveCount, InspectionCount, BlobCount, DeletedAt }`. **Bez e-mail adresa i imena ljudi** — dnevnik brisanja koji sam čuva lične podatke poništava brisanje.

Vrijednost je manja nego kod automatskog brisanja (ti znaš šta si obrisao), ali je jedini trag poslije nepovratne radnje. Ako ne želiš tabelu, ostaje log linija — reci i izbacujem.

---

## 8. API

| Metoda | Putanja | Auth | Napomene |
|---|---|---|---|
| GET | `/api/admin/organizations` | SystemAdmin | postojeći; DTO dobija `lastActivityAt`, `daysInactive`, `status` + `statusName`, `activityStatus`, `isActive`, `deactivatedAt`, `hasEverPaid`, `effectivePlan` (§5). Liste za naplatu su klijentski filteri nad ovim odgovorom — **bez zasebnog endpointa** |
| PUT | `/api/admin/organizations/{id}/status` | SystemAdmin | `{ isActive, note? }` → ažurirana organizacija; poništava sesije pri isključivanju |
| POST | `/api/admin/organizations/{id}/delete` | SystemAdmin | `{ confirmName }`; 400 na neslaganje, 200 na uspjeh |

Nije tenant-scoped → **bez `IAccessGuard`**, ravni SystemAdmin split kao svaka druga `api/admin/*` ruta (ADR-030). Validacija kroz FluentValidation u kontroleru (ADR-018).

Obični korisnik ne dobija nijedan novi endpoint. Njegov jedini dodir s ovom funkcijom je poruka pri prijavi ako mu je organizacija zaključana.

---

## 9. Frontend

- `AdminDashboardPage.tsx` — red organizacije dobija **badge statusa** (Aktivna zelena / Uspavana žuta / Za brisanje crvena / Neaktivna siva) i kolonu **"Zadnja aktivnost"** (`prije 4 dana` / `nikad`), plus filter čipove u dvije grupe:
  - *stanje*: **Sve · Uspavane · Za brisanje · Neaktivne**
  - *naplata* (§5): **Ističe uskoro · Probni period · Istekli** — svaki red uz paket prikazuje i datum isteka i zadnju aktivnost, jer se odluka donosi na osnovu oba
- Dvije `VitalCard` kartice uz postojeće: **"Ističe u 30 dana"** i **"Za brisanje"**.
- Prekidač **"Aktivna / Neaktivna"** u redu tabele i na `OrganizationFormPage`, uz modal potvrde koji kaže šta se dešava: *"Korisnici organizacije {naziv} bit će odmah odjavljeni i neće se moći prijaviti dok je ne vratite na aktivnu."*
- Dugme **"Obriši"** otvara modal koji traži da upišeš tačno ime organizacije, i ispod njega ispisuje šta se briše (broj korisnika, pčelinjaka, košnica, pregleda).
- Modeli + hookovi u `adminQueries.ts`; labele preko frontend mape koja preslikava `BsLabels.Label(OrgStatus)`.
- Poruka o deaktivaciji na `LoginPage` dolazi ravno iz 403 odgovora — bez posebnog rukovanja, `detail` se već prikazuje.

---

## 10. Config

```json
"Activity": {
  "InactiveAfterDays": 90,
  "ThrottleHours": 6,
  "FlushSeconds": 60
}
```

Odsutan ključ = navedena vrijednost. `InactiveAfterDays` mijenja samo prag prikaza — ništa se ne dešava kad se pređe.

---

## 11. Faze

| Faza | Sadržaj | Rizik |
|---|---|---|
| **A — Mjerenje i naplata** | 4 kolone + migracija + backfill, `IActivityTracker` + worker + middleware + dvije auth tačke, `OrgStatusHelper` + testovi, polja u DTO-u (uklj. `effectivePlan`), badge + kolona aktivnosti + **liste za naplatu** (§5) u admin tabeli | nema — aditivno, nijedno postojeće ponašanje se ne mijenja |
| **B — Prekidač** | `PUT …/status`, blokada prijave i refresha, poništavanje sesija, prekidač i modal u UI-ju | jedina promjena ponašanja u cijelom specu; **provjeriti na staging okruženju prije produkcije** |
| **C — Brisanje** | `IOrgPurgeService` (redoslijed iz §7.2), `POST …/delete` + modal s imenom, brisanje blobova, popravka `DeleteUserAsync`, opciono §7.4 | nepovratno, ali **isključivo ručno pokrenuto** |

Faza A sama odgovara na pitanje iz ideje. B i C su alati koje poslije toga koristiš.

---

## 12. Van opsega

Automatsko brisanje, odbrojavanje i upozorenja korisniku (D2 — izbačeno namjerno, ne zaboravljeno); per-user "zadnji put viđen" vidljiv adminu organizacije; samouslužno brisanje vlastite organizacije; ledger uplata (`PlanNotes` ostaje zapis dok ne stigne Paddle, SPEC-09 faza 2); analitika korištenja mimo jedne kolone; automatsko zaključavanje organizacije kad joj istekne paket — SPEC-09 izričito kaže da istek nikad ne zaključava podatke i ovaj spec to ne mijenja (`IsActive` je ručna odluka, ne posljedica isteka).

---

## 13. Kriteriji prihvatanja

- [ ] `Organization.LastActivityAt` se pomjera na prijavi, na rotaciji refresh tokena i na svakom 2xx non-GET zahtjevu člana organizacije; **GET nikad ne upisuje**; throttle drži najviše jedan upis po `ThrottleHours` (test + live provjera da 20 uzastopnih upisa daju jedan UPDATE).
- [ ] Heartbeat ne obara i ne usporava zahtjev: sa zaustavljenim workerom svi endpointi rade normalno (pun kanal odbacuje najstariji unos).
- [ ] `OrgStatusHelperTests`: granice 89/90 dana, null → fallback na `CreatedAt`, plaćena organizacija nikad `ForDeletion`, bivši klijent → `Dormant`, `isActive = false` → `Inactive` uvijek.
- [ ] `FirstPaidAt` postavlja `UpdateOrganizationPlanAsync` za Standard/Pro/Max, a **ne** postavlja ga registracijski probni period; migracija izuzima postojeće ne-Free organizacije koje nisu `"Probni period"`.
- [ ] Admin tabela prikazuje badge, zadnju aktivnost i filtere; filter "Za brisanje" **prikazuje i deaktivirane organizacije** koje ispunjavaju uslov (gleda `activityStatus`, ne `status`).
- [ ] Liste za naplatu (§5): "Ističe uskoro" vraća samo plaćene s istekom unutar 30 dana sortirano po datumu, "Probni period" samo `PlanNotes = "Probni period"` s istekom u budućnosti, "Istekli" samo one kojima je pohranjeni paket ≠ Free a efektivni = Free; svaki red prikazuje datum isteka **i** zadnju aktivnost. `effectivePlan` dolazi sa servera kroz `PlanHelper` — frontend nigdje ne računa istek sam.
- [ ] `PUT …/status` s `isActive: false`: korisnik s otvorenom sesijom je odjavljen odmah (refresh → 401 → odjava), nova prijava → **403** s porukom na bosanskom; SystemAdmin nije pogođen; vraćanje na `true` vraća prijavu (live-provjereno).
- [ ] `POST …/delete` odbija pogrešan `confirmName` s 400; na tačan briše organizaciju sa korisnicima, pčelinjacima, košnicama, pregledima + fotografijama, todo-ima (dodijeljenim **i** na nivou pčelinjaka), prehranama, vrcanjima, tretmanima, pašnjacima i troškovima — **bez ijedne FK greške** — te briše blobove iz storagea; `Feedback` redovi ostaju anonimizirani bez screenshotova (`OrgPurgeServiceTests` + jedan live prolaz na probnoj organizaciji).
- [ ] `AdminService.DeleteUserAsync` više ne puca kad korisnik ima dodijeljen todo (zatečeni bug iz §7.1).
- [ ] Sve labele na bosanskom (`BsLabels.Label(OrgStatus)` + frontend mapa). Dokumentacija ažurirana: `features/org-activity-status.md`, `api-contracts.md`, `context.md`, `decisions.md` (ADR-032: heartbeat izvan request putanje + izračunat status + ručna deaktivacija poništava sesije), ovaj spec → ✅.

## Ključni fajlovi

| Šta | Fajl |
|---|---|
| Presedan izračunatog stanja | `backend/Melarium.Domain/Common/PlanHelper.cs` |
| Entitet + EF konfiguracija | `backend/Melarium.Domain/Entities/Organization.cs`, `Melarium.Entity/Configurations/OrganizationConfiguration.cs` |
| Ručna aktivacija paketa (`FirstPaidAt`) | `backend/Melarium.Application/Features/Admin/AdminService.cs:71` |
| Brisanje organizacije / korisnika (oba se mijenjaju) | `AdminService.cs:88`, `AdminService.cs:432` |
| Auth tačke + provjera `IsActive` | `backend/Melarium.Application/Features/Auth/AuthService.cs:48`, `:154` |
| Poništavanje sesija | `backend/Melarium.Application/Common/Security/ISessionRevoker.cs` |
| Presedan queue + worker (ADR-021) | `backend/Melarium.Infrastructure/Email/EmailNotificationWorker.cs` |
| Oblik middlewarea | `backend/Melarium.API/Middleware/SecurityHeadersMiddleware.cs` |
| FK zamke | `Melarium.Entity/Configurations/TodoConfiguration.cs:37,45`, `OrganizationConfiguration.cs:34-43`, `FeedbackConfiguration.cs:47-57` |
| Brisanje blobova | `backend/Melarium.Application/Common/Interfaces/IFileStorage.cs` |
| Zamrznuto 401 ponašanje (ne dirati) | `frontend/src/core/services/apiClient.ts` + `docs/ignore.md` |
| Admin UI | `frontend/src/features/admin/AdminDashboardPage.tsx`, `OrganizationFormPage.tsx`, `adminQueries.ts` |
