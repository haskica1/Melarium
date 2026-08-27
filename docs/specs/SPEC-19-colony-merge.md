# SPEC-19 — Sastavljanje društava (Colony merge)

| | |
|---|---|
| **Status** | ✅ Implemented (2026-08-21) — sve tri faze. Vidi §12 za ono što je provjereno, a što ostaje za provjeru na živoj bazi |
| **Effort** | M — jedna nova tabela, dvije kolone na `Beehive`, jedan servis, jedan filter proveden kroz 13 tačaka čitanja, plus AI akcija |
| **Depends on** | ništa novo. Dodiruje SPEC-03 (matice), SPEC-08 (tretmani), SPEC-12 (prehrana), SPEC-17 (AI Asistent), SPEC-09 (limiti paketa) |
| **New secrets / packages** | nema |
| **Breaking** | ne. Jedina promjena postojećeg ponašanja je namjerna: sastavljena košnica nestaje iz svih lista i **prestaje se brojati** u limit paketa |
| **Reserves** | `NotificationType.BeehiveMerged = 26`, `AiActionKind.MergeBeehive = 8`, `QueenStatus.Removed = 5` |
| **Rate-limit policy** | nema novo |

> **Kako je ovaj spec nastao.** Ideja i sve odluke u §1 su Asimove, donesene u razgovoru 2026-08-21,
> protiv koda kakav je tog dana na `main`. Ovaj dokument je **zapis** tih odluka, ne mjesto gdje su
> donesene. Svaka tvrdnja o postojećem kodu je tog dana provjerena direktnim čitanjem — ako prođe
> vrijeme, ponovo provjeriti citirane linije prije implementacije.

---

## 0. Zašto ovo postoji

U pčelarstvu se dva društva redovno spajaju u jedno. Melarium to danas ne zna: jedini način da
košnica nestane iz pčelinjaka je `DELETE /api/beehives/{id}` — a on **kaskadno briše i
`TreatmentEntry` redove**, dakle zakonsku evidenciju lijekova s obavezom čuvanja od 5 godina
(`docs/features/treatments.md`, "Deleting a hive cascades to its treatment entries"). Pčelar koji
sastavi dva društva danas bira između netačnog pčelinjaka i uništene evidencije.

### Šta praksa kaže (istraženo 2026-08-21, izvori na dnu)

**Spaja se društvo, ne košnica.** Košnica je sanduk; on fizički ostaje, samo prazan. Melarium ta dva
pojma poistovjećuje — glosar definiše `Beehive` kao *"an individual hive box"*, a sve (pregledi,
matice, tretmani, prinos) visi na njemu. Ovaj spec **ne razdvaja** ta dva pojma; uvodi samo činjenicu
da je društvo iz jedne košnice prešlo u drugu (§10, odbačeno #1).

**Razlozi za sastavljanje**, redom po učestalosti u izvorima:

| Razlog | Enum |
|---|---|
| Društvo je ostalo bez matice; ili se mlada matica nije vratila s oplodnje | `Queenless` |
| Radilice trutovnjače (lažne matice) — takvo društvo se ne može spasiti dodavanjem matice | `LayingWorkers` |
| Slabo društvo koje ne može prezimjeti (nedovoljno pčela ili zaliha) | `WeakColony` |
| Stara ili loša matica, slabo leglo | `PoorQueen` |
| Ekonomika paše — po Farraru jedno društvo od 60.000 pčela daje ~1,54× više meda nego četiri od po 15.000 | `Consolidation` |
| Smanjenje broja slabih društava koja izazivaju grabež na pčelinjaku | `Robbing` |
| Ostalo (slobodan tekst u napomeni) | `Other` |

**Kada.** Najčešće u jesen (priprema za zimu) i u proljeće; van toga kad je paša neuobičajeno slaba.

**Kako.** Dominantno **preko novinskog papira** (`Newspaper`) — list novina između dva nastavka,
probušen na par mjesta; pčele ga za nekoliko dana pregrizu i miris se izjednači. Alternativa je
**direktno sastavljanje** (`Direct`) uz maskiranje mirisa (razrijeđena rakija, voda s bosiljkom).
Treća vrijednost `Other` postoji jer metoda nije poenta ovog feature-a.

**Tri pravila koja se ponavljaju u svim izvorima i koja ovaj spec modelira:**

1. **Slabije se pripaja jačem.** Jače društvo ostaje na svom mjestu, u svojoj košnici. → *izvorna*
   (pripojena) košnica nestaje, *prijemna* ostaje. Tačno ono što je traženo.
2. **Preživi jedna matica.** Obično se matica slabijeg društva ukloni prije sastavljanja — **ali ne
   uvijek**: ako je jače društvo bezmatak, preživi matica iz slabijeg. Zato se matica bira (D2), a ne
   pretpostavlja.
3. **Prazna košnica se skida s pčelinjaka.**

Dvije stvari koje izvori naglašavaju, a koje **namjerno ne implementiramo kao blokadu** (§10):
bolesno društvo se ne smije sastavljati (problem se prenese na veće društvo), i društvo s lažnim
maticama ubija dodanu oplođenu maticu.

---

## 1. Odluke (Asim, 2026-08-21)

| # | Odluka |
|---|---|
| **D1** | **Arhiva je trajna.** Sastavljena košnica se nikad ne vraća u pogon. Kad se sanduk idući put naseli novim društvom, pravi se **nova košnica** s novim QR kodom. Istorija dva različita društva se nikad ne miješa u jednom zapisu. |
| **D2** | **Matica se bira**, ne pretpostavlja: ostaje matica prijemne / ostaje matica pripojene / nijedna (bezmatak). Ako ostaje matica pripojene, ona se **prebacuje** na prijemnu košnicu. |
| **D3** | **Otvoreni zadaci pripojene košnice se brišu.** Prehrana: košnica se **skida s programa**, a program se rano zatvara s komentarom samo ako je to bila zadnja aktivna košnica na njemu. Tretman: red iz registra se **ne dira**, upisuje se komentar da je učešće prekinuto. |
| **D4** | **Sastavljanje između pčelinjaka je dozvoljeno** — ne samo unutar istog. |
| **D5** | **AI Asistent može sastaviti košnice naredbom** ("sastavi košnicu 5 sa košnicom 3"), uz karticu za potvrdu. |
| **D6** | **Nadređeni dobija notifikaciju**, isto kao kod kreiranja košnice. |
| **D7** | **Poništavanje u roku od 24 sata.** Nakon toga je sastavljanje trajno. Poništavanje mora vratiti **sve**, uključujući obrisane zadatke. |

### Šta je odlučeno bez pitanja (reci ako treba drugačije)

- **Istorija pripojene košnice ostaje na njoj.** Pregledi, vrcanja i tretmani se ne prebacuju na
  prijemnu košnicu — to društvo ih je stvarno proizvelo. Prošli prinos i dalje ulazi u statistiku.
- **Sastavljena košnica se ne broji u limit paketa** (`MaxBeehives`). Društva stvarno više nema.
- **Skeniranje starog QR koda ne pada** — vodi na stranicu koja kaže da je društvo sastavljeno i nudi
  otvaranje prijemne košnice. Nakon D1 stara naljepnica ostaje na praznom sanduku dok je pčelar ne
  zamijeni, pa je to jedini način da skeniranje ne izgleda kao greška.

---

## 2. Model podataka

```
Beehive += MergedIntoBeehiveId  int?       // self-FK; null = košnica je u pogonu
Beehive += MergedAt             DateTime?  // datum sastavljanja (dan, ne trenutak)

BeehiveMerge (nova tabela)
  SourceBeehiveId  int   FK Beehive   // pripojena — ona koja nestaje
  TargetBeehiveId  int   FK Beehive   // prijemna — ona koja ostaje
  MergedAt         DateTime
  Reason           MergeReason
  Method           MergeMethod
  QueenOutcome     MergeQueenOutcome
  Notes            string?
  UndoJournalJson  string?            // §4 — sve što je promijenjeno van ove tabele
  UndoneAt         DateTime?          // null = na snazi; postavljeno = poništeno
  CreatedById      int?  FK User
```

Migracija `AddBeehiveMerge`. Sve aditivno; postojeći redovi dobijaju `MergedIntoBeehiveId = null`,
dakle svi ostaju u pogonu. Obje FK veze na `Beehive` idu s `DeleteBehavior.Restrict` — kaskada bi
značila da brisanje prijemne košnice tiho briše i zapis o sastavljanju.

**Zašto i kolona i tabela.** Isti oblik kao SPEC-10 selidbe: `Apiary.CurrentPastureId` nosi *stanje*,
`ApiaryMove` nosi *događaj*. Ovdje `Beehive.MergedIntoBeehiveId` je stanje koje čita **svaki** upit
nad listom košnica (§5) — bez njega bi svaka lista morala u `JOIN`. `BeehiveMerge` je događaj:
razlog, metoda, matica, ko i kad. Prijemna košnica može kroz godine primiti više društava, i svako
od njih ima svoj red.

**Nove enumeracije** (`Melarium.Domain/Enums/`, engleska imena, bosanske labele kroz `BsLabels`):

```
MergeReason        Queenless=1 · LayingWorkers=2 · WeakColony=3 · PoorQueen=4 · Consolidation=5 · Robbing=6 · Other=7
MergeMethod        Newspaper=1 · Direct=2 · Other=3
MergeQueenOutcome  KeptTarget=1 · KeptSource=2 · None=3
```

**`QueenStatus.Removed = 5` ("Uklonjena").** Matica koja ne preživi sastavljanje se fizički ukloni —
nije ni uginula, ni nestala, a "Zamijenjena" bi lagalo u slučaju `None` (obje uklonjene, društvo
ostaje bezmatak dok se ne doda nova). Enum se čuva kao `int`, pa nova vrijednost **ne traži
migraciju** — samo jedan red u `BsLabels.Label(QueenStatus)` i u frontend mapi. Ako ne želiš novu
vrijednost, alternativa je `Died` uz napomenu; reci prije implementacije.

`UndoneAt` postoji da poništeno sastavljanje ostavi trag umjesto da nestane bez riječi — red se briše
iz prikaza, ne iz baze.

---

## 3. Sastavljanje — jedna transakcija

`IBeehiveMergeService.MergeAsync(CreateBeehiveMergeDto)`. Sve u jednom `SaveChangesAsync()`; ako bilo
šta pukne, ništa se nije desilo.

### 3.1 Provjere prije bilo kakvog upisa

| Provjera | Greška |
|---|---|
| Obje košnice postoje | 404 |
| `sourceId != targetId` | 400 — "Košnica se ne može sastaviti sama sa sobom." |
| Nijedna nije već sastavljena (`MergedIntoBeehiveId == null` na obje) | 400 |
| Obje pripadaju **istoj organizaciji** | 400 |
| `EnsureCanManageApiaryAsync` nad **oba** pčelinjaka (D4 — mogu biti različiti) | 403 |
| `MergedAt` nije u budućnosti (tolerancija +1 dan, kao tretmani) | 400 |
| `QueenOutcome == KeptSource` traži aktivnu maticu na izvornoj košnici | 400 |

**Karenca se ne blokira, nego upozorava.** Ako izvorna košnica ima tretman u karenci, pčele koje
prelaze nose je sa sobom u prijemnu košnicu. Dijalog za potvrdu to ispiše (§7.2); sastavljanje se ne
zabranjuje — to je pčelarska odluka, ne aplikacijska.

### 3.2 Redoslijed upisa

1. **`BeehiveMerge`** red se kreira.
2. **Matice** (D2), po `QueenOutcome`:
   - `KeptTarget` — aktivna matica **izvorne** košnice: `Status = Removed`, `EndDate = MergedAt`.
   - `KeptSource` — aktivna matica **prijemne** košnice: `Status = Removed`, `EndDate = MergedAt`;
     tek **zatim** se aktivna matica izvorne košnice prebaci: `BeehiveId = targetId`. Redoslijed je
     bitan — obrnuto bi na trenutak dalo dvije aktivne matice u istoj košnici, što
     `QueenService.UpdateAsync` izričito odbija (*"The beehive already has an active queen"*).
   - `None` — aktivne matice **obje** košnice: `Status = Removed`, `EndDate = MergedAt`.
   - Svakoj zatvorenoj matici se u `Notes` dopiše rečenica: *"Uklonjena pri sastavljanju društva
     (košnica X → košnica Y, 12.09.2026.)"*.
   - Nema aktivne matice → korak se preskače bez greške (bezmatak je najčešći razlog, §0).
3. **Zadaci** (D3): otvoreni `Todo` redovi te košnice (`BeehiveId == sourceId && !IsCompleted`) se
   brišu. Završeni ostaju — oni su istorija, ne obaveza. Zadaci vezani za *pčelinjak* se ne diraju.
4. **Prehrana** (D3): za svaki aktivni program koji pokriva izvornu košnicu poziva se
   **`DietService.RemoveBeehiveAsync(dietId, sourceId)`** (`DietService.cs:248`) — postavlja
   `DietBeehive.RemovedOn`. Ako je to bila **zadnja** aktivna košnica programa, dodatno
   `CompleteEarlyAsync` s komentarom *"Društvo sastavljeno s košnicom Y."*
   > Ovo je jedina tačka gdje se namjera iz razgovora razlikuje od koda. Od SPEC-12 prehrana je
   > program **na nivou pčelinjaka** koji pokriva skup košnica, a `FeedingEntry` je jedan obrok za
   > cijelu grupu. "Zatvoriti prehranu" za jednu košnicu bi je ugasilo i svim ostalim košnicama.
5. **Tretmani** (D3): `TreatmentEntry` redovi izvorne košnice na tretmanima **u toku** (`EndDate ==
   null`) dobiju dopisan `DoseNote`: *"Prekinuto 12.09.2026. — društvo sastavljeno s košnicom Y."*
   Red se **ne briše** — 5 godina obaveze čuvanja. `DoseNote` je slobodan tekst i **ne štampa se u
   PDF registar** (provjereno: `treatmentPdf.ts:28` štampa samo broj i imena košnica), pa zakonski
   artefakt ostaje netaknut, a trag postoji u detalju tretmana.
6. **Košnica**: `MergedIntoBeehiveId = targetId`, `MergedAt`, `UpdatedAt`.
7. **`UndoJournalJson`** se serijalizuje (§4).
8. **Notifikacija** (D6): `NotificationType.BeehiveMerged = 26` nadređenom, istim putem kao
   `SendBeehiveCreatedNotificationsAsync` (`BeehiveService.cs:280`).

**Šta se ne dira:** pregledi, vrcanja, fotografije, dodijeljeni pčelari (`UserBeehive`), QR kod,
`LabelNumber`, `UniqueId`. Sve ostaje na arhiviranoj košnici.

---

## 4. Poništavanje u roku od 24 sata (D7)

`POST /api/beehive-merges/{id}/undo`. Rok se računa **na serveru**, od `CreatedAt` reda + 24h (ne od
`MergedAt` — datum sastavljanja se smije unijeti unazad). Isteklo → 400 s porukom koliko je vremena
prošlo. Već poništeno → 400.

Poništavanje mora vratiti i ono što je obrisano, a to se ne može rekonstruisati iz stanja baze. Zato
sastavljanje piše **`UndoJournalJson`** — tačan popis svega što je promijenilo van svoje tabele:

```jsonc
{
  "queens":     [{ "id": 41, "beehiveId": 7, "status": "Active", "endDate": null, "notes": "…" }],
  "todos":      [{ "title": "Dodati nastavak", "notes": null, "dueDate": "2026-09-20",
                   "priority": "Medium", "assignedToId": 3, "createdById": 3, "createdAt": "…" }],
  "dietHives":  [{ "dietBeehiveId": 88, "dietId": 12, "completedEarly": false }],
  "treatments": [{ "treatmentEntryId": 55, "doseNote": null }]
}
```

Vraćanje ide obrnutim redom od §3.2: tretmanima se vrati stari `doseNote`, `DietBeehive.RemovedOn`
se **na istom redu** vrati na `null` (novi red bi promijenio `CreatedAt`, a to je "kad je košnica
ušla u program" i ulazi u računicu potrošnje — vidi komentar u `DietBeehive.cs`), zadaci se ponovo
kreiraju iz snimka, maticama se vrati `Status`/`EndDate`/`BeehiveId`/`Notes`, i na kraju
`Beehive.MergedIntoBeehiveId = null`. `BeehiveMerge.UndoneAt` se postavi.

Zadaci se vraćaju s **novim `Id`**-om — to je jedina stvar koju poništavanje ne može vratiti
identično. Nikakav vanjski zapis ne pokazuje na `Todo.Id`, pa to nema posljedicu.

**Zašto snimak, a ne odgođeno brisanje.** Alternativa je bila ne brisati zadatke 24 sata pa ih
pobrisati pozadinskim workerom. To znači novi hosted service koji vječno radi zbog rijetkog događaja,
plus prozor u kojem zadaci postoje ali su nevidljivi. Snimak je jedna kolona i ~30 linija, a
`AiActionPayload.PreviousFields` iz SPEC-17 je isti obrazac u ovom istom kodu.

---

## 5. Nevidljivost — gdje se tačno filtrira

Ovo je jedini dio speca koji dodiruje postojeći kod na više mjesta. Popis je iscrpan i provjeren
2026-08-21; **`GetByUniqueIdAsync` namjerno NIJE na spisku.**

| # | Mjesto | Izmjena |
|---|---|---|
| 1 | `BeehiveRepository.GetByApiaryIdAsync` | `+ b.MergedIntoBeehiveId == null` |
| 2 | `BeehiveRepository.GetByOrganizationAsync` | isto |
| 3 | `BeehiveRepository.CountByOrganizationAsync` | isto → limit paketa prestaje brojati sastavljene |
| 4 | `AccessGuard.cs:126` (SystemAdmin grana, `GetAllAsync()`) | novi `GetAllActiveAsync()` |
| 5 | `AccessGuard.cs:132` (Beekeeper grana, `FindAsync`) | `+ && b.MergedIntoBeehiveId == null` |
| 6 | `CalendarAccessResolver.cs:22` (`GetAllAsync()`) | `GetAllActiveAsync()` |
| 7 | `CalendarAccessResolver.cs:65` (`FindAsync`) | dodati uslov |
| 8 | `StatsService.cs:39` (`GetAllAsync()`) | `GetAllActiveAsync()` |
| 9 | `ApiaryMappingProfile.cs:12` i `:17` | `Beehives.Count(b => b.MergedIntoBeehiveId == null)` |
| 10 | `ApiaryRepository.GetByOrganizationWithCountsAsync` | filtrirati brojanje |
| 11 | `OrganizationRepository.GetBeehiveCountsAsync` | filtrirati (admin tabela) |
| 12 | `BeehiveService.GetQrCodesByApiaryAsync` (`FindAsync`) | dodati uslov — nema smisla štampati naljepnicu za košnicu koje nema |
| 13 | `ApiaryRepository.GetWithBeehivesAsync` | filtrirani `Include` — puni `ApiaryDetailDto.Beehives`, dakle **listu košnica na stranici pčelinjaka**. *Nađeno pri implementaciji 2026-08-21; spec ga je prvobitno propustio.* |
| — | `AlertRuleService.cs:64`, `WeeklySummaryService.cs:118` | ništa — čitaju kroz #1 i #2, pa se riješe same |
| — | `BeehiveService.BackfillLabelNumbersFromNamesAsync` | ništa — jednokratna admin radnja, filter je nebitan |

**Zašto ne EF global query filter.** `HasQueryFilter` na `Beehive` bi ovo riješio jednom linijom i
niko ga ne bi mogao zaboraviti. Odbačen je jer `TreatmentEntry.Beehive`, `HarvestEntry.Beehive` i
`Inspection.Beehive` su **obavezne navigacije** ka filtriranom entitetu: EF bi ih tiho vratio kao
`null`, pa bi registar tretmana ostao bez imena košnice — tačno onaj zakonski zapis zbog kojeg ovaj
feature uopšte ne briše redove. Eksplicitnih 13 tačaka je duže, ali ne laže.

**Pristup arhiviranoj košnici i dalje radi.** `GET /api/beehives/{id}` i `EnsureCanAccessBeehiveAsync`
se **ne** mijenjaju — sastavljena košnica se može otvoriti direktnim linkom, iz arhive, iz istorije
tretmana i iz skeniranja. Nestaje samo iz *lista*.

---

## 6. API

Novi kontroler `BeehiveMergesController`, `api/beehive-merges`, `[Authorize]` na nivou klase.

- `POST /api/beehive-merges` → 201 `BeehiveMergeDto`
  `{ sourceBeehiveId, targetBeehiveId, mergedAt, reason, method, queenOutcome, notes? }`
- `POST /api/beehive-merges/{id}/undo` → 200 `BeehiveMergeDto` (§4)
- `GET /api/beehive-merges/by-beehive/{beehiveId}` → sastavljanja **primljena** na tu košnicu
  (`UndoneAt == null`), najnovije prvo
- `GET /api/beehives/merged?apiaryId=` → arhiva sastavljenih košnica; jedini endpoint koji ih vraća
  u listi
- `GET /api/beehive-merges/preview?sourceBeehiveId=` → brojevi za sažetak posljedica iz §7.2
  (koliko zadataka, koje prehrane, koji tretmani, ima li karence). Čita, ne piše.

`BeehiveDto`/`BeehiveDetailDto` dobijaju `mergedIntoBeehiveId`, `mergedIntoBeehiveName`, `mergedAt`,
te `canUndoUntil` (samo na detalju) — frontend nigdje ne računa rok sam.

`BeehiveScanDto` dobija `mergedIntoBeehiveId` i `mergedIntoBeehiveName` (§1, skeniranje starog koda).

FluentValidation: `CreateBeehiveMergeValidator` — oba id-a > 0 i različiti, `mergedAt` nije u
budućnosti (+1 dan), `notes` ≤ 1000, enumi u opsegu. Validacija u ovom kodu živi u kontrolerima —
AI izvršilac je mora pozvati sam (SPEC-17 §5.2).

---

## 7. Frontend

### 7.1 Gdje se pokreće

Dugme **"Sastavi društvo"** na `BeehiveDetailPage`, u istom redu s "Uredi"/"Obriši". Pokreće se
**s košnice koja nestaje** — tako je jasno šta je predmet radnje, i sprječava najčešću zamjenu smjera.

### 7.2 Dijalog

Jedan modal, ne stranica:

1. **Prijemna košnica** — pretraživi izbornik, grupisan po pčelinjaku (D4). Vlastita košnica i već
   sastavljene košnice nisu u listi.
2. **Datum** — današnji dan ponuđen.
3. **Razlog** i **metoda** — dropdown, bosanske labele iz §2.
4. **Matica** — tri radio opcije, s imenima obje košnice u tekstu:
   *"Ostaje matica košnice 3 (prijemne)"* / *"Ostaje matica košnice 7 (pripojene) — prelazi u košnicu 3"* /
   *"Nijedna — društvo ostaje bez matice"*. Opcija koja traži nepostojeću maticu je onemogućena s
   objašnjenjem.
5. **Napomena** — slobodan tekst.
6. **Sažetak posljedica** prije potvrde, s tačnim brojevima s `/preview` endpointa:
   *"Košnica 7 izlazi iz pčelinjaka Gornji. Briše se 2 otvorena zadatka. Košnica se skida s prehrane
   'Zimska pogača'. Matica (2024, žuta) se zatvara. Prekida se učešće u tretmanu 'Apivar'."*
   Ako je izvorna košnica u karenci, tu ide i upozorenje iz §3.1.

Potvrda je **dvostruka** — isti obrazac kao destruktivne AI akcije (SPEC-17 §7.3), jer i uz
poništavanje iz D7 ovo je radnja koja mijenja pčelinjak.

### 7.3 Gdje se vidi rezultat

- **`BeehiveDetailPage` prijemne košnice** — kartica *"Sastavljena društva"* u sidebaru, ispod
  "Matica": po jedan red za svako primljeno društvo (ime pripojene košnice → link na arhivu, datum,
  razlog). Dok traje rok iz D7, red nosi dugme **"Poništi"** i tekst dokad vrijedi.
- **`BeehiveDetailPage` pripojene košnice** — traka preko vrha stranice: *"Ova košnica je 12.09.2026.
  sastavljena s košnicom 3. Nije više u pčelinjaku."* Sve akcije koje pišu (novi pregled, novi
  zadatak, nova prehrana, uredi) su sakrivene; istorija se čita normalno.
- **`ApiaryDetailPage`** — ispod liste košnica, sklopiva sekcija *"Sastavljene košnice (3)"*. Prazna
  se ne prikazuje. Ovo je jedini put do arhive kroz UI.
- **`ScanPage`** — skeniranje starog koda pokaže poruku i dugme *"Otvori košnicu 3"*.

Modeli u `core/models/index.ts`, servis `core/services/beehiveMergeService.ts`, hook-ovi u
`core/services/queries.ts`. Invalidacija nakon uspjeha: `beehives`, `apiary`, `apiaries`, `todos`,
`diets`, `plan-usage`.

---

## 8. AI Asistent (D5)

`AiActionKind.MergeBeehive = 8`. **`IsDestructive` vraća `true`** za njega
(`AiAssistantService.cs:645`) — traži drugu potvrdu kao update/delete akcije.

Akcija ima **dvije** ciljne košnice, a `AiActionPayload` nosi jednu. Izvorna ide u postojeći
`BeehiveId`/`BeehiveName`; prijemna u dva nova polja na `AiActionFields` (`AiEnvelope.cs:49`):
`TargetBeehiveId`, `TargetBeehiveName`. Oba prolaze kroz `AiTargetResolver` kao i svaka druga
košnica — nerazriješena prijemna košnica daje karticu s `Issue`, ne grešku.

`AiActionExecutor` dobija `MergeBeehiveAsync` i poziva **`IBeehiveMergeService.MergeAsync`** — ne
repozitorij (SPEC-17 §5.1), i sam pokreće `CreateBeehiveMergeValidator` (§5.2). Potvrdni zahtjev je
neprovjeren ulaz kao i svaki drugi (§5.3): obje košnice se ponovo provjere kroz `IAccessGuard`.

Prompt dobija jedan primjer i jedno pravilo: **matica se nikad ne pogađa.** Ako korisnik nije rekao
koja ostaje, model vraća `QueenOutcome = null` i asistent pita — isti razgovorni mehanizam iz Faze B.
Razlog i metoda smiju imati podrazumijevanu vrijednost (`Other`/`Newspaper`); matica ne smije, jer je
to jedina nepovratna odluka u cijeloj radnji.

---

## 9. Faze

| Faza | Sadržaj | Rizik |
|---|---|---|
| **A — Jezgro** | entiteti + migracija + enumi, `BeehiveMergeService` (§3), filter iz §5, API iz §6, dijalog i prikazi iz §7, notifikacija D6 | filter iz §5 dodiruje 13 postojećih tačaka čitanja — **jedini pravi rizik u specu**; test po tački |
| **B — Poništavanje** | `UndoJournalJson`, `/undo`, dugme i rok u UI-ju (§4) | nema — čisto aditivno |
| **C — AI Asistent** | `MergeBeehive` akcija, polja u `AiActionFields`, izvršilac, prompt (§8) | AI pokreće nepovratnu radnju; ide **posljednja**, kad je servis već proživio stvarnu upotrebu |

Faze su nezavisno isporučive i idu ovim redom. **A i B se deployaju zajedno** ako se ide u produkciju
isti dan — A sam znači 24 sata nepovratnih grešaka bez izlaza. Ako se razdvajaju, dijalog iz A mora
jasno pisati da poništavanja nema.

---

## 10. Razmotreno i odbačeno

1. **Razdvojiti `Beehive` (sanduk) od `Colony` (društvo)** — domenski ispravno i pravi model koji ovaj
   feature "traži". Odbačeno: to je prepravka svake tabele koja pokazuje na `BeehiveId` (devet ih je)
   i svake stranice u aplikaciji, zbog jednog feature-a. D1 daje najveći dio koristi bez ijedne
   migracije postojećih podataka.
2. **Meko brisanje (`IsDeleted`) umjesto zasebnog pojma** — jeftinije, ali "obrisana" i "društvo
   pripojeno košnici 3" nisu ista činjenica, a druga je ono što pčelar želi vidjeti.
3. **EF global query filter** — vidi §5; tiho ruši imena košnica u registru tretmana.
4. **Blokirati sastavljanje bolesnog društva** — izvori na to izričito upozoravaju, ali Melarium nema
   pojam dijagnoze; jedini signal bi bio tretman u toku, a tretman ne znači bolest. Umjesto blokade
   ide upozorenje o karenci (§3.1).
5. **Vraćanje košnice u pogon nakon naseljavanja** — razmotreno i odbijeno kroz D1: čuva QR kod, ali
   miješa istoriju dva različita društva u jednom zapisu.
6. **Pozadinski worker za odgođeno brisanje zadataka** — vidi §4.

---

## 11. Van opsega

Podjela društva (obrnuta radnja — jedno u dva); premještanje okvira ili legla između košnica; praćenje
nastavaka i opreme; automatsko prebacivanje dodijeljenih pčelara (`UserBeehive`) na prijemnu košnicu;
prebacivanje istorije pregleda/vrcanja; statistika "koliko je društava izgubljeno po sezoni";
sastavljanje više od dvije košnice u jednom potezu (dvije uzastopne radnje daju isto); brisanje
`UndoJournalJson` nakon isteka roka.

---

## 12. Kriteriji prihvatanja

Legenda: **✅** pokriveno automatskim testom · **🔎** provjereno čitanjem koda, bez testa (razlog niže)
· **⏳** traži živu bazu ili pokrenutu aplikaciju — **nije provjereno lokalno**.

- [x] ✅ Matice po `QueenOutcome`: `KeptTarget` zatvara izvornu; `KeptSource` zatvara prijemnu **i**
      prebacuje izvornu na prijemnu košnicu **bez** greške "already has an active queen"; `None`
      zatvara obje; košnica bez matice ne pravi grešku. *(`BeehiveMergeServiceTests`, 5 testova)*
- [x] ✅ Prehrana: košnica dobije `RemovedOn`; program s još košnica **ostaje aktivan**; program kojem
      je to bila zadnja košnica pređe u `StoppedEarly` s komentarom. *(3 testa)*
- [x] ✅ Otvoreni zadaci te košnice su obrisani; završeni i oni na nivou pčelinjaka su netaknuti.
- [x] ✅ `TreatmentEntry` red preživi sa dopisanim `DoseNote`; završen tretman se ne dira;
      poništavanje vraća prethodni `DoseNote`. *(3 testa)*
- [x] ✅ Poništavanje unutar 24h vrati **sve**: košnicu u pčelinjak, maticu u prethodni status i
      košnicu, zadatke (isti naslov, rok, prioritet, izvršilac), `RemovedOn` na `null` **na istom
      `DietBeehive` redu** (isti `Id` i `CreatedAt`), `DoseNote` na prethodnu vrijednost.
      Nakon 24h → greška; dvaput → greška. *(4 testa)*
- [x] ✅ Odbijeno: ista košnica dvaput, već sastavljena košnica (kao izvor ili cilj), košnice iz dvije
      organizacije, budući datum, `KeptSource` bez matice. Cross-apiary sastavljanje traži
      `EnsureCanManageApiaryAsync` nad **oba** pčelinjaka. *(6 testova)*
- [x] ✅ Sve promjene idu u **jedan** `SaveChangesAsync()`.
- [x] ✅ `AccessGuard` ne vidi sastavljene košnice: SystemAdmin grana zove `GetAllActiveAsync()` (ne
      `GetAllAsync`), Beekeeper grana ima uslov u predikatu. *(`AccessGuardTests`, 2 testa s
      kompajliranjem predikata)*
- [x] ✅ AI: parser čita `merge_beehive` + `targetHive` (string i broj) i **ne popunjava**
      `queenOutcome` kad ga model nije poslao; resolver odbija `"sve košnice"`, više izvornih košnica,
      nepoznatu/istu prijemnu košnicu i izostanak matice, a pri tome zadrži prijemnu košnicu u
      poljima; izvršilac zove `IBeehiveMergeService`, pokreće validator i ne pogađa maticu.
      *(`AiEnvelopeParserTests` 4, `AiTargetResolverTests` 8, `AiActionExecutorTests` 5)*
- [x] 🔎 Preostalih 11 tačaka filtriranja iz §5 (repozitoriji, `ApiaryMappingProfile`,
      `CalendarAccessResolver`, `StatsService`, `GetQrCodesByApiaryAsync`) — **nisu unit-testirane**:
      `Melarium.Application.Tests` referencira samo `Melarium.Application`, pa upiti u
      `Melarium.Entity` nemaju kako biti izvršeni bez novog paketa (EF InMemory/SQLite), a kućno
      pravilo je da se paketi ne dodaju bez pitanja. Svaka izmjena je pojedinačno pročitana.
- [x] 🔎 Sve labele na bosanskom kroz `BsLabels` (`MergeReason`, `MergeMethod`, `MergeQueenOutcome`,
      `QueenStatus.Removed`, `AiActionKind.MergeBeehive`) + odgovarajuće mape u frontendu.
- [x] 🔎 Backend se builda bez grešaka; frontend `tsc --noEmit` i `vite build` prolaze čisto; svih
      **597** testova prolazi (bilo 555 prije ovog speca).
- [ ] ⏳ Migracija `AddBeehiveMerge` primijenjena na bazu (`dotnet ef database update`). Generisana je
      i pročitana — `Up()` nema nijedan `DropColumn`/`DropTable`, samo dvije nullable kolone, jednu
      tabelu i indekse — ali **nije pokrenuta**: lokalno nema Postgresa.
- [ ] ⏳ Sastavljena košnica stvarno nestaje iz liste pčelinjaka, kalendara, statistike, upozorenja,
      sedmičnog sažetka i QR eksporta; `Apiary.beehiveCount` i admin tabela padnu za jedan;
      `plan-usage` oslobodi mjesto.
- [ ] ⏳ `GET /api/beehives/{id}` arhivirane košnice vraća 200 s cijelom istorijom; skeniranje njenog
      QR koda vodi na poruku s linkom na prijemnu košnicu.
- [ ] ⏳ PDF registar tretmana je vizuelno isti kao prije (ime košnice se i dalje štampa).
- [ ] ⏳ Beekeeper kojem je dodijeljena samo sastavljena košnica dobije praznu listu, **ne 403**.
- [ ] ⏳ AI naredba "sastavi košnicu 5 sa košnicom 3" u stvarnom razgovoru: kartica s obje košnice,
      druga potvrda, i potpitanje kad matica nije rečena.
- [x] ✅ Dokumentacija: `features/colony-merge.md` (nov), `api-contracts.md`, `context.md`,
      `glossary.md`, `decisions.md` (**ADR-038** eksplicitni filter umjesto global query filtera,
      **ADR-039** merge piše kroz `_uow` radi atomarnosti), `features/beehives.md`,
      `features/queens.md`, `features/diets.md`, `features/treatments.md`,
      `features/ai-assistant.md`, ovaj spec.

### Odstupanja od speca, svjesna

| # | Spec je rekao | Urađeno | Zašto |
|---|---|---|---|
| 1 | §5: 12 tačaka filtriranja | **13** | `ApiaryRepository.GetWithBeehivesAsync` puni listu košnica na stranici pčelinjaka; spec ga je propustio. Riješeno filtriranim `Include`-om. |
| 2 | §3.2 korak 4: zvati `DietService.RemoveBeehiveAsync` | Isti upis kroz `_uow` | Taj servis interno zove `SaveChangesAsync()`, što ruši pravilo "jedna transakcija" iz §3. Puno obrazloženje: **ADR-039**. |
| 3 | §6: `preview?sourceBeehiveId=` | Dodan i `targetBeehiveId` | Labele za izbor matice moraju znati maticu **prijemne** košnice, a ona se bira u dijalogu. |
| 4 | §2: `QueenStatus.Removed` označen kao "reci ako ne želiš" | Implementiran | Nije bilo prigovora; `Died` bi lagao u slučaju `None`. Jedan red u enumu i dva u labelama — lako se povuče. |

---

## Ključni fajlovi

| Šta | Fajl |
|---|---|
| Presedan "stanje + događaj" (SPEC-10) | `Melarium.Domain/Entities/ApiaryMove.cs`, `Melarium.Entity/Configurations/ApiaryMoveConfiguration.cs` |
| Entitet koji se proširuje | `Melarium.Domain/Entities/Beehive.cs`, `Melarium.Entity/Configurations/BeehiveConfiguration.cs` |
| Sve tačke filtriranja (§5) | `Melarium.Entity/Repositories/BeehiveRepository.cs`, `Melarium.Application/Common/Security/AccessGuard.cs:121`, `Features/Calendar/CalendarAccessResolver.cs:22,65`, `Features/Stats/StatsService.cs:39`, `Features/Apiaries/ApiaryMappingProfile.cs:12,17`, `Melarium.Entity/Repositories/OrganizationRepository.cs` |
| Limit paketa | `Melarium.Application/Common/Security/PlanGuard.cs:41` |
| Zatvaranje matice — postojeći obrazac | `Melarium.Application/Features/Queens/QueenService.cs:57` |
| Skidanje košnice s prehrane / rano zatvaranje | `Melarium.Application/Features/Diets/DietService.cs:248`, `:274` |
| Komentar na tretmanu (`DoseNote`) | `Melarium.Domain/Entities/TreatmentEntry.cs`, `Features/Treatments/TreatmentService.cs:244` |
| Dokaz da `DoseNote` nije u PDF-u | `frontend/src/shared/utils/treatmentPdf.ts:28` |
| Kaskada koju ovaj feature zaobilazi | `Melarium.Application/Features/Beehives/BeehiveService.cs:142` |
| Notifikacija nadređenom — obrazac | `Melarium.Application/Features/Beehives/BeehiveService.cs:280` |
| AI akcija + destruktivnost | `Features/Assistant/AiActionExecutor.cs:61`, `AiAssistantService.cs:645`, `AiEnvelope.cs:49` |
| Presedan JSON snimka prethodnog stanja | `Features/Assistant/AiActionPayload.cs` (`PreviousFields`) |
| Stranice koje se mijenjaju | `frontend/src/features/beehives/BeehiveDetailPage.tsx`, `features/apiaries/ApiaryDetailPage.tsx`, `features/beehives/ScanPage.tsx` |

---

## Izvori (istraženo 2026-08-21)

- [Spajanje pčelinjih zajednica — Gospodarski list](https://gospodarski.hr/rubrike/pcelarstvo-rubrike/spajanje-pcelinjih-zajednica/) — razlozi, obje metode, uklanjanje matice slabijeg društva, skidanje prazne košnice s pčelinjaka
- [Combining Hives (Uniting) — Talking With Bees](https://talkingwithbees.com/beekeeping-how-to-guides/combining-hives-uniting-hives) — postupak s novinama, lažne matice ubijaju dodanu maticu, konsolidacija okvira nakon sedmicu dana
- [Spajanje slabih pčelinjih društava — Agromedia](https://www.agromedia.rs/agro-teme/pcelarstvo/spajanje-slabih-pcelinjih-drustava-pomaze-pcelama-da-prezive-zimu/) — grabež kao razlog, Farrarov odnos 60.000 vs. 4×15.000 pčela
- [Combining Honeybee Colonies — PerfectBee](https://www.perfectbee.com/a-healthy-beehive/inspecting-your-hive/combining-honeybee-colonies) — bezmatak kao najčešći razlog; upozorenje da bolesno društvo prenosi problem
- [Spajanje pčelinjih društava — BH Pčelar](https://bhpcelar.ba/pcelarska-praksa/spajanje-pcelinjih-drustava-u-jesen-se-slabije-drustvo-stavlja-ispod-jaceg-a-u-proljece-se-dodaje-iznad-jaceg-drustva/) — sezonsko pravilo: u jesen slabije ispod jačeg, u proljeće iznad
