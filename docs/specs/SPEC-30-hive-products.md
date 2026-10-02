# SPEC-30 — Prinosi: med i ostali pčelinji proizvodi

| | |
|---|---|
| **Status** | ✅ Implemented (2026-10-01) — see `features/harvests.md`, ADR-049. Migracija `AddProductsToHarvests` još nije primijenjena na produkciji |
| **Effort** | L (~3 dana: zasebni modul pa spajanje s vrcanjima) |
| **Depends on** | SPEC-02 (vrcanja), SPEC-09 (paketi), SPEC-24 (zaključavanje), SPEC-25 (izvještaj) |
| **New secrets / packages** | nema |
| **Breaking** | Šema: mijenja postojeće kolone `Harvests` i `HarvestEntries` (odobren izuzetak, vidi dolje) i puni novu kolonu iz postojećih podataka. API: kompatibilan unazad — stari klijent i dalje vidi i upisuje samo med. Ponašanje: meni „Vrcanja" → „Prinosi"; „Razlika" u sezonskom izvještaju uključuje prihod od proizvoda |

## Cilj

Vrcanja (SPEC-02) su bilježila samo med; vosak, propolis i polen su tamo izričito ostavljeni van obima.
Pčelar koji skuplja propolis s mreža, polen iz hvatača ili topi saće u vosak nije imao gdje to upisati,
a ti proizvodi nisu bili ni u statistici ni u sezonskom izvještaju — iako kod mnogih pčelara čine
značajan dio zarade i traže se na prijavama za poticaje.

Ovaj spec pretvara vrcanje u **prinos**: jedan zapis za bilo koji pčelinji proizvod, upisan po
košnicama, ukupno za pčelinjak ili za cijelu organizaciju, s opcionom cijenom — i vidljiv na košnici,
pčelinjaku, u statistici i u sezonskom izvještaju (ekran, PDF, Excel).

## Kako se došlo do spojenog modela

1. **2026-09-30** — Asim je donio propisan prompt za zaseban modul („Evidencija ostalih pčelinjih
   proizvoda": vlastite tabele, `/api/hive-products`, zaseban meni). Implementiran je i provjeren uživo.
2. **2026-10-01** — Asim je pitao treba li med biti jedan od proizvoda, pa kako bi izgledalo spajanje.
   Njegova ideja: *vrcanje je prikupljanje pčelinjih proizvoda* — s košnice se skupi 20 kg meda ili
   5 kg polena, a unos postoji po košnici, po pčelinjaku i za cijelu organizaciju. Analiza je pokazala da
   zasebni modul već ima isti oblik kao vrcanje (zapis + redovi po košnici), pa bi dva modula značila dvije
   stranice, dvije forme i dvije kartice za isti posao.
3. Zasebni modul je **obrisan prije ikakvog commita** (tabele `HiveProductHarvests`/`HiveProductEntries`,
   migracija `AddHiveProducts`, `/api/hive-products`, `features/hiveProducts/`). Nikad nije bio na
   produkciji, pa nema podataka za prenos.

## Odluke

Ideja je Asimova; izbori su riješeni s njim kroz pitanja, a ovaj dokument je napisan posljednji, kao
zapis dogovorenog.

### Prvi krug (2026-09-30) — i dalje važe

- **D1 — Zapis za cijelu organizaciju je dozvoljen.** Vosak se najčešće topi zajedno sa svih
  pčelinjaka. Zapis bez pčelinjaka (`ApiaryId = NULL`) je **zajednički** — isto značenje kao zajednički
  trošak iz SPEC-25, nikad „nepoznato". Odbijeno: pčelinjak obavezan (prijedlog iz prompta).
- **D2 — Standard, Pro, Max** za proizvode osim meda: `PlanFeature.HiveProducts = 6` (5 zauzimaju
  Nagrade iz SPEC-27, koje su u stashu).
- **D3 — Free je samo za čitanje (i nakon downgrade-a).** Paket se provjerava samo na unosu i izmjeni.
  Organizacija na Free paketu i dalje vidi svoje proizvode svuda i smije ih obrisati — izvještaj za
  prošlu sezonu ne smije tiho izgubiti dio prihoda. Odbijeno: „sve skriveno, kao Nagrade".
- **D4 — Vrste, preciznost od 1 mg.** Pet iz prompta + **Perga** i **Apitoksin**. Apitoksin se mjeri u
  miligramima, pa je količina `numeric(12,6)` kg.
- **D5 — Radi se na main-u**, uprkos konfliktima sa `stash@{0}` (vidi „Za deploy").

### Spajanje (2026-10-01)

- **S1 — Naziv „Prinosi".** Meni, stranica, sekcija pčelinjaka i kartica košnice. Riječ „vrcanje"
  ostaje tamo gdje je riječ o medu (vital „Vrcanja", „Ukupno vrcano" u izvještaju).
- **S2 — Nivo cijele organizacije za sve proizvode, uključujući med.** Med pretočen iz nastavaka s
  više pčelinjaka upisuje se jednom, za organizaciju.
- **S3 — Jedan nivo po zapisu.** Nema miješanog unosa („dio po košnicama, ostatak ukupno"): ko ima
  oboje, pravi dva zapisa. Inače bi zbir po košnicama i ukupni broj značili različite stvari u istom zapisu.
- **S4 — Med u saću je zasebna vrsta i ne broji se u med.** Prodaje se drugačije i ima drugu cijenu;
  pribrojen medu bi iskrivio prinos i prosjek po košnici.
- **S5 — Med ostaje na svim paketima.** Bio je besplatan od SPEC-02 i spajanje to ne smije promijeniti;
  plan se provjerava samo kad je vrsta nešto drugo.
- **S6 — Dva odobrena izuzetka od pravila projekta** (vidi „Frozen-area exceptions").

### Izvještaji i statistika (2026-10-03)

Asim je pitao treba li izmijeniti izvještaje sada kad su prinosi spojeni. Brojke su već bile tačne;
problem je bio što izvještaj i dalje izgleda kao dva modula: „Prinos" u izvještaju je značio samo med
dok „Prinosi" u meniju znače sve, a nigdje nije bilo jednog pregleda sezone.

- **R1 — Jedna sekcija „Prinosi" u izvještaju** (ekran, PDF, Excel): pregled svih proizvoda s prihodom
  (med prvi; „Ukupno prihod" je jedini zbir i jednak je prihodu u Bilansi), pa med detaljno kao do
  sada, pa ostali proizvodi. Excel list „Prinosi" zamjenjuje „Prinos" i „Ostali proizvodi".
- **R2 — Isto na stranici Statistika:** jedna sekcija „Prinosi — sezona": pločica za svaki proizvod,
  pa grafikoni meda, pa ostali proizvodi.
- **R3 — Ostali proizvodi dobijaju raspodjelu po pašnjaku i po košnici**, kao med — u izvještaju i u
  Statistici. Odbijeno: samo po pčelinjaku (moja preporuka).
- Bez osporavanja: kvačice „Prinosi" s istim podkvačicama koje važe za oba (ključ `yield` ostaje, pa
  sačuvani izbor preživljava); kartica „Med" umjesto „Prinos"; napomene spojene po proizvodu; kartica
  na početnoj „Prinos meda po mjesecima"; Bilansa bez izmjena.

### Prijedlozi prihvaćeni bez osporavanja

- **P1 — Prihod od proizvoda ulazi u Bilansu izvještaja.** `EstimatedRevenueBam` ostaje samo med;
  dodaje se `ProductRevenueBam`, a „Razlika" = med + proizvodi − troškovi.
- **P2 — Nikad zbir kg preko vrsta.** 200 g mliječi i 20 kg voska ne daju broj; na zajedničkoj osi
  grafikona mliječ nestane. Jedini zajednički broj je prihod (KM).
- **P3 — Pregled na stranici `/reports`**, ne samo u PDF/Excel (SPEC-25: ekran = dokument).
- **P4 — Jedinice po proizvodu u UI-u:** propolis, mliječ i apitoksin u gramima; mliječ i apitoksin
  cijena po gramu. Baza je uvijek kg i KM/kg.
- **P5 — Neblokirajuće upozorenje o karenci** na formi, za svaku vrstu (polen i mliječ se jedu
  direktno) — osim na nivou organizacije, koji nema košnice.
- **P6 — „Zadnja aktivnost" organizacije (ADR-034)** čita `Harvest.OrganizationId`.
- **P7 — Kartica košnice** pokazuje med kao i prije, a ostale proizvode kao čipove ispod — samo kad ih ima.
- **P8 — Pomoć (SPEC-14)** za `/harvests` rute prepisana za prinose.

## Model

```
Harvest : BaseEntity                       // jedan zapis prinosa
  OrganizationId  int  (FK, cascade)       // NOVO — popunjeno iz pčelinjaka postojećih zapisa
  ApiaryId        int? (FK, cascade)       // bilo int — NULL = cijela organizacija
  Date            DateTime
  ProductType     HiveProductType          // NOVO — default Honey (1)
  HoneyType       HoneyType?               // bilo obavezno — samo za med
  PricePerKg      numeric(8,2)?            // nepromijenjeno; uvijek KM/kg (do 999.999,99)
  BulkKg          numeric(12,6)?           // NOVO — količina bez raspodjele po košnicama
  Notes, CreatedById, Entries              // nepromijenjeno

HarvestEntry
  QuantityKg      numeric(12,6)            // bilo numeric(6,2) — sada do 1 mg
  FramesExtracted int?                     // samo za med; za ostale vrste se odbacuje
```

**Tri nivoa, tačno jedan po zapisu:**

| Nivo | `ApiaryId` | Količina | Ko piše |
|---|---|---|---|
| Po košnicama | postavljen | redovi `HarvestEntry` | upravitelji pčelinjaka |
| Ukupno za pčelinjak | postavljen | `BulkKg` | upravitelji pčelinjaka |
| Cijela organizacija | `NULL` | `BulkKg` | **samo vlasnik organizacije** |

Izvedeno, nikad čuvano (`Domain/Common/HarvestTotals`): `TotalKg = BulkKg ?? Σ Entries`,
`EstimatedRevenue = TotalKg × PricePerKg` ili `null`.

`HiveProductType`: `Honey=1, CombHoney=2, Wax=3, Propolis=4, Pollen=5, RoyalJelly=6, BeeBread=7,
BeeVenom=8, Other=99` (`BsLabels`: Med, Med u saću, Vosak, Propolis, Polen, Matična mliječ, Perga,
Apitoksin, Ostalo).

### Med i ostalo — kompajler tjera izbor

`HarvestKind { Honey, OtherProducts, All }` je **obavezan** argument svake agregatne metode
`IHarvestRepository`. Prije spajanja je osam mjesta sabiralo „sva vrcanja" kao med (statistika,
izvještaj, dashboard, sedmični sažetak, AI Asistent, kartica košnice, liste, seeder); nakon spajanja bi
ista linija tiho pribrojila vosak medu. Sa obaveznim argumentom svako mjesto mora reći šta sabira — a
`All` služi samo za liste, nikad za zbir.

### Kompatibilnost unazad

Instalirani PWA klijenti starijeg builda moraju i dalje raditi:

- `GET /api/harvests` bez novih parametara vraća **samo med** (sa `allProducts=true` ili `productType`
  sve ostalo).
- `POST` bez `productType` je med; `PUT` bez `productType` zadržava vrstu zapisa.
- `HoneyTypeName` je prazan string (ne `null`) za proizvode koji nisu med.

## Backend

**Validacija** (`CreateHarvestValidator`, `UpdateHarvestValidator`, `HarvestEntryValidator`,
granice u `HarvestLimits`): tačno jedno od `BulkKg`/`Entries`; zapis organizacije bez `Entries`; vrsta
meda obavezna za med (i kad `productType` izostane); bez duplih košnica; količina 1 mg – 200 kg po
košnici, do 100.000 kg ukupno; cijena 0 – 999.999,99 KM/kg; datum ne u budućnosti (+1 dan);
napomena ≤ 500; okviri 0–200. Strana košnica i raspodjela zapisa organizacije → 400 iz servisa, s
razlogom pod `errors.detail` (ključ koji frontend interceptor čita).

**Endpointi** `/api/harvests`: GET lista (`apiaryId`, `beehiveId`, `year`, `allProducts`,
`productType`), GET `{id}`, POST, PUT (pčelinjak nepromjenjiv; zamjena redova; prelazak ukupno ↔ po
košnicama), DELETE, GET `hive/{beehiveId}/yield` (samo med, kao prije) i novi
GET `hive/{beehiveId}/summary` (svi proizvodi po godini). Detalji u `api-contracts.md`.

**Pristup:** matrica vrcanja + zapis organizacije (piše OrgAdmin, čita ApiaryAdmin, pčelar ga ne vidi).
Zaključani pčelinjaci (SPEC-24) se ručno izbacuju iz liste organizacije **i** iz pčelarove liste —
pčelarova putanja vrcanja to prije spajanja nije radila.

**Potrošači** (svi s eksplicitnim `HarvestKind`):

| Mjesto | Šta sabira |
|---|---|
| `StatsService` | med (+ zapisi organizacije, red „Zajedničko"); proizvodi zasebno po vrsti |
| `ReportService` | med (+ zajednički); proizvodi zasebno; Bilansa sabira samo prihod |
| `DashboardService` (kartica prinosa) | med; „ukupno za pčelinjak" broji se upraviteljima, zapis organizacije samo vlasniku, pčelaru nijedan |
| `WeeklySummaryService` | med (+ zajednički) |
| `AiAssistantService` | med po godinama za košnicu |
| `HarvestService.GetHiveYieldAsync` | med (stari endpoint) |
| `ReportDataSeeder` | med |

**Statistika:** `harvestsByProduct[]` — svi proizvodi, med prvi. Med uključuje zapise ukupno/za
organizaciju (red „Zajedničko" po pčelinjaku i po pašnjaku); „najbolje košnice" samo iz redova po
košnici. Ostali proizvodi: `hiveProductsByApiary[]`, `hiveProductsByPasture[]`,
`hiveProductsByBeehive[]` (tekuća godina; po košnici bez zaključanih košnica).

**Izvještaj:** `harvests { byProduct[], estimatedRevenueBam }`; `products { recordCount, byApiary[],
byPasture[], byBeehive[] }`; `balance.productRevenueBam` i `balance.byApiary[].productRevenueBam`;
napomene `unpriced[]` i `notPerHive[]` (po proizvodu, i med) i `sharedHarvestCount`.

**Zajedničko za oba:** `Application/Common/PastureBuckets` (pašnjak po pravilu iz SPEC-10, „Matična
lokacija", zapisi organizacije kao „Zajedničko") i `Application/Common/NaturalComparer` (redovi po
košnici po imenu, K2 prije K10 — bez kulture, jer server može raditi bez ICU podataka).

## Frontend

- `features/harvests/HarvestsPage.tsx` — „Prinosi": filter proizvoda („Svi proizvodi" + vrste) i
  godine; vitali za med kad je filter Med, inače vitali proizvoda (bez zbira kg); čipovi po vrsti;
  grupe po pčelinjaku, „Zajedničko — cijela organizacija" prva.
- `features/harvests/HarvestFormPage.tsx` — „Gdje je prikupljeno" (Po košnicama / Ukupno za
  pčelinjak / Cijela organizacija — treće samo vlasniku), pčelinjak, proizvod, vrsta meda i kolona
  okvira samo za med, cijena u jedinici proizvoda, konverzija upisanog pri promjeni vrste, upozorenje
  o karenci.
- `features/harvests/ApiaryHarvestsSection.tsx` — sekcija „Prinosi" na pčelinjaku (svi proizvodi).
- `features/beehives/HiveYieldCard.tsx` — „Prinos": kg meda za sezonu + čipovi ostalih proizvoda.
- `features/harvests/ProductChips.tsx`, `ProductsReadOnlyNotice.tsx`.
- `features/stats/HarvestStats.tsx` — sekcija „Prinosi" u Statistici: pločice, podnaslovi, matrice
  ostalih proizvoda (po košnici prvih deset, ostalo na „Prikaži sve").
- `features/reports/ReportPage.tsx` — sekcija „Prinosi" (pregled, „Med", „Ostali proizvodi");
  zaglavlja tabela se na telefonu smiju prelomiti, pa pregled stane bez klizanja.
- `shared/utils/hiveProductUnits.ts` — jedinice, konverzije i formatiranje: **jedino mjesto**, koriste
  ga ekrani, PDF i Excel.
- `ReportPage`, `useReportPreferences`, `seasonReportPdf.ts`, `seasonReportXlsx.ts`, `Sidebar`,
  `PlansPage`, `helpContent`/`helpRoutes`, `planService` (`isFeatureLocked(…, 'hiveProducts')`).

## Rubni slučajevi

- Brisanje košnice briše njene redove; zapis bez preostalih redova pokazuje 0 kg (kao i prije).
- Brisanje pčelinjaka briše njegove zapise — **ne** pretvara ih u zapise organizacije.
- Zapis „ukupno za pčelinjak" ili „za organizaciju" pčelar ne vidi, nije na kartici košnice i nije u
  tabelama „po košnici" — izvještaj to kaže napomenom (`notPerHive`), po proizvodu.
- Na Free paketu: med se upisuje i mijenja normalno; postojeći vosak se ne može urediti ni pretvoriti
  u med, a med se ne može pretvoriti u vosak (provjeravaju se i stara i nova vrsta). Brisanje radi.
- Spojena košnica (SPEC-19) se ne nudi u formi; uređivanje starog zapisa s njom ispušta taj red.

## Van obima (ovaj krug)

Rang-lista najboljih košnica po proizvodu (tabela po košnici je spisak po imenu, ne rang), brzi unos
„vosak s poklopaca" uz vrcanje, proizvodi na početnoj stranici, u AI Asistentu i u sedmičnom sažetku,
zalihe/prodaja/kupci, prevod (i18n je u `stash@{2}`).

## Frozen-area exceptions (`ignore.md`)

Oba izuzetka je Asim izričito odobrio 2026-10-01, uz plan spajanja.

| Pravilo | Šta se mijenja | Zašto je dozvoljeno |
|---|---|---|
| „Do not change existing entity configurations, column types" | `HarvestConfiguration` (`ApiaryId` i `HoneyType` nullable, nove kolone, FK na organizaciju), `HarvestEntryConfiguration` (`QuantityKg` `numeric(6,2)` → `numeric(12,6)`) | Odobreno; sve promjene su proširenja — nijedna postojeća vrijednost se ne gubi ni ne zaokružuje |
| „Never write raw SQL" (`CLAUDE.md`) | jedan `UPDATE` u migraciji puni `OrganizationId` iz pčelinjaka | Odobreno; isti presedan kao SPEC-12 — pravilo vrijedi za repozitorije i upite, ne za migracije podataka |
| „Never edit existing migration files" | nije prekršeno | Dodata je **nova** migracija |
| „Never rename/remove interface properties" (`core/models/index.ts`) | nije prekršeno | `Harvest.apiaryId` i `honeyType` su prošireni na `null`; ništa nije preimenovano ni uklonjeno |

## Za deploy

- **Migracija `20261001213201_AddProductsToHarvests`** — primjenjuje je `MigrateAsync()` pri
  restartu API-ja (`deploy/deploy.sh`). Redoslijed: `HoneyType` i `ApiaryId` nullable → `BulkKg` →
  `OrganizationId` nullable → `UPDATE` iz pčelinjaka → `OrganizationId NOT NULL` → `ProductType`
  (default 1 = med) → `QuantityKg` proširen → indeks + FK. Dodat kao NOT NULL odmah, `OrganizationId`
  bi dobio 0 i FK bi odbio svaki postojeći red — zato je migracija ručno uređena.
  **Prije deploya probati na kopiji produkcijske baze na VPS-u.** Primijenjena na lokalnu dev bazu
  2026-10-01; `Down` prvo briše zapise koje stara šema ne može držati (samo za razvoj).
- **API i frontend idu zajedno.** Stari frontend radi s novim API-jem (vidi kompatibilnost); novi
  frontend sa starim API-jem ne.
- **`stash@{0}` (SPEC-27, Nagrade)** pri vraćanju daje konflikte u `HarvestService.cs` (stash omotava
  `GetAllAsync` da označi nagrađen med — potpis se promijenio), `HarvestDto.cs` (`HasAward`),
  `HarvestsPage.tsx` i `ApiaryHarvestsSection.tsx` (značka uz čip vrste meda), `OrganizationRepository.cs`,
  `BsLabels.cs`, `PlanFeature.cs`/`PlanGuard.cs` (`Achievements = 5` uz `HiveProducts = 6`), `App.tsx`,
  `helpContent.ts`, `Sidebar.tsx`, `core/models/index.ts` i u **snapshotu migracija**. Pored konflikata,
  `AchievementService.ApplyAsync` provjerava organizaciju vrcanja **preko pčelinjaka** — za zapis
  organizacije (`ApiaryId = NULL`) to vraća „Odabrano vrcanje ne postoji." Treba ga prebaciti na
  `harvest.OrganizationId`, i odlučiti smije li se nagrada vezati za proizvod koji nije med (npr. med u
  saću na sajmu).
- **`stash@{2}` (i18n)** dira iste ekrane prinosa — nove tekstove treba prevesti kad se vrati.

## Acceptance criteria

- [x] Jedan zapis za sve proizvode; tri nivoa, tačno jedan po zapisu; zapis organizacije piše samo
      OrgAdmin (ApiaryAdmin → 403), pčelar ga ne vidi.
- [x] Med na svakom paketu; ostali proizvodi Free → 402 na unosu i izmjeni, čitanje i brisanje rade.
- [x] Stari klijent: GET bez parametara = samo med; POST bez vrste = med; PUT bez vrste zadržava vrstu.
- [x] Nijedan zbir meda ne uključuje druge proizvode (`HarvestKind` obavezan); med u saću nije med.
- [x] Med upisan ukupno ulazi u sve zbirove, ali ne u tabelu po košnici — i to piše u napomeni.
- [x] Nigdje zbir kg preko vrsta; jedinice iz jednog modula; apitoksin do 1 mg.
- [x] Statistika, izvještaj (ekran, PDF, Excel) i Bilansa uključuju proizvode; zapisi organizacije u
      ukupnoj bilansi, ne u redu pčelinjaka.
- [x] Zaključani pčelinjak ne ulazi ni u listu organizacije ni u pčelarovu listu.
- [x] Izvještaj i Statistika: jedna sekcija „Prinosi" — pregled svih proizvoda (prihod jedini zbir,
      jednak Bilansi), med detaljno, ostali proizvodi po pčelinjaku, pašnjaku i košnici.
- [x] Docs: `features/harvests.md`, `api-contracts.md`, `context.md`, `season-report.md`,
      `glossary.md`, `org-activity.md`, `plans-billing.md`, ADR-049, ovaj spec → ✅.

**Provjereno:** 871/871 testova (`HarvestServiceTests` prepisan, novi `HarvestValidatorTests` i
`StatsServiceTests`, `ReportServiceTests` i `DashboardServiceTests` dopunjeni); 54/55 API scenarija
uživo na lokalnoj bazi s migracijom (backfill, med na Free, sva tri nivoa, validacija, uloge, pčelar,
kartica košnice, statistika, izvještaj, izmjene i paket — jedini „pad": organizacija na početku nije
bila na Free paketu, pa je vosak prošao s 201; ponovljeno na Free, vosak, polen za organizaciju i med
u saću → 402 i ništa se ne snima); stranice u pregledniku (lista,
forma po košnicama i za organizaciju, uređivanje, košnica, izvještaj); PDF i Excel izvezeni, Excel
pročitan (5 listova, zbirovi i napomene). Nije provjereno: svijetla tema, iPhone, otvaranje u Excelu.

**Provjereno (izvještaji i statistika, 2026-10-03):** 875/875 testova (novi: pregled s medom prvim i
prihodom jednakim Bilansi, proizvodi po pašnjaku s „Matičnom lokacijom" i „Zajedničko", po košnici
samo iz redova po košnicama i po imenu, zaključana košnica izvan tabele u Statistici); uživo na
lokalnoj bazi s probnim podacima na sva tri nivoa: ekran izvještaja (zbirovi se slažu: prihod
5.687,25 = med + proizvodi = Bilansa), PDF pročitan stranicu po stranicu (zaglavlja matrica se lome
u dva reda), Excel pročitan (4 lista; ćelije matrica u pravim kolonama), Statistika, početna, 375 px
bez klizanja stranice. Nije provjereno: svijetla tema, iPhone, otvaranje u Excelu.
