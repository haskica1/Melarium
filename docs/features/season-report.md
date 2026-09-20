# Sezonski i godišnji izvještaj (SPEC-25)

> Objedinjeni izvještaj pčelarenja za proizvoljan period — prinos, troškovi, procijenjeni prihod i
> sažetak tretmana — izvoziv u PDF i Excel. Namijenjen prijavi na poljoprivredne subvencije.
> Spec: `specs/SPEC-25-season-report.md`.

## Šta je bilo prije

PDF se generisao samo za **QR naljepnice** (`qrPdf.ts`) i **evidenciju tretmana** (`treatmentPdf.ts`).
Prinos, troškovi i statistika živjeli su isključivo na `/stats` — pčelar je brojeve prepisivao rukom.

## Stranica

`/reports`, stavka **Izvještaji** u meniju. Vidljiva OrganizationAdminu i ApiaryAdminu
(`usePermissions.canSeeReports`); **pčelar je ne vidi**, jer je već read-only na vrcanjima i
tretmanima a izvještaj sadrži finansije organizacije. SystemAdmin je nema — nema svoju organizaciju.

Period se bira slobodnim rasponom `od–do`, uz brze izbore: tekuća i prethodna godina, sezona
(1.3.–31.10.), Q1–Q4 i pojedinačni mjesec. Jedan mehanizam pokriva mjesečni, kvartalni, godišnji i
sezonski izvještaj — period koji traži subvencija ne mora se poklopiti s kalendarskim kvartalom.

Stranica prikazuje isti sadržaj koji izlazi u dokument, pa se ništa ne pojavljuje tek u izvozu.

### Izbor sekcija i formata

Kvačice biraju **šta ulazi u dokument**: Prinos (s podtabelama po pčelinjaku / vrsti meda / košnici /
pašnjaku), Troškovi, Bilansa, Tretmani — u kartici s filterima, uz brojač „Odabrano: X od 4".

Izvoz je **glavna akcija stranice i stoji u hero-u, desno**: `<select>` PDF/Excel + `btn-primary`
„Izvezi" — isti raspored koji Vrcanja imaju s izborom godine i dugmetom „Dodaj vrcanje".

Kvačice koriste **isti markup i klase** kao izbor košnica na formama prehrane i tretmana
(`accent-honey-500`). Prva verzija ih je imala bez `accent-*`, pa su se renderovale kao
browser-default plave — jedina kontrola u aplikaciji koja nije bila u boji kuće.

Četiri pravila koja izbor nosi:

- **Isti izbor važi za ekran i za oba formata.** Pregled na stranici je doslovno ono što izlazi iz
  datoteke; da izbor važi samo za izvoz, kvačice ne bi imale vidljiv efekat dok se fajl ne otvori.
- **Napomene se ne mogu isključiti.** One su ono što drži brojku poštenom (procjena prihoda, kg bez
  cijene, valute izvan bilanse); isključiva napomena je napomena koja će se isključiti.
- **„Po košnici" je podrazumijevano isključeno** — jedan red po košnici, a gazdinstvo sa 40 košnica
  na to potroši cijelu stranu.
- **Isključivanje „Prinosa" povlači i njegove podtabele**, a ponovno uključivanje ih vraća — inače bi
  se moglo desiti da je sekcija uključena a sve u njoj isključeno, pa se ispiše prazan naslov.

U Excelu isključena sekcija **gubi cijeli list**, ne ostaje prazan: prazan tab u radnoj knjizi čita
se kao „nismo imali podataka", a to je druga tvrdnja od „nisam ovo ni tražio". List `Sažetak` uvijek
ostaje jer nosi bilansu i napomene.

Ako se isključi sve, dugme za izvoz je onemogućeno — dokument bi bio zaglavlje i linija za potpis.

**Izbor se pamti** u `localStorage` (`melarium-report-sections`, `melarium-report-format`) —
isti izvještaj se podnosi svake sezone, a ponovno štikliranje istih kućica je upravo ono što su
kvačice trebale ukloniti. Pohranjeni izbor se **spaja preko defaulta**, ne koristi se sirov: izbor
zapisan prije nego je neka sekcija postojala inače bi tu sekciju ostavio trajno isključenom.

## API

`GET /api/reports/season?from=&to=&apiaryId=` → `SeasonReportDto`. `Roles.Managers`.

Server računa sve, klijent samo crta — **PDF i Excel se prave iz istog DTO-a**, tako da dva dokumenta
za isti period ne mogu dati različit broj.

## Pravila koja izvještaj nosi

| Pravilo | Zašto |
|---|---|
| Granice perioda se računaju u **lokalnoj zoni** (`AppTimeZone`), ne u UTC | vrcanje u 23:30 posljednjeg dana perioda inače upadne u naredni mjesec (`ReportPeriod`) |
| Tretman pripada periodu po **`StartDate`** | inače zbir kvartala nije jednak godini, što u dokumentu izgleda kao greška |
| Prihod je **uvijek** označen kao procjena, uz `unpricedKg` | `Harvest.PricePerKg` je nullable; kg bez cijene bi tiho smanjili prihod |
| Valute se **grupišu**, nikad ne sabiraju; bilansa je samo BAM | prihod je u KM po konstrukciji (`PricePerKg` je KM/kg) |
| Zajednički troškovi (`ApiaryId = null`) se ne razmazuju po pčelinjacima | prikazani su kao vlastiti blok i ulaze samo u ukupnu bilansu |
| Zaključani pčelinjaci (SPEC-24) ne ulaze ni u jedan dio izvještaja | `IAccessGuard.GetAccessibleApiariesAsync()` ih već izbacuje — sve ostalo je vezano na taj skup |
| Trošak vezan za pčelinjak izvan dosega se ne prikazuje; zajednički uvijek | pripada organizaciji, ne pčelinjaku |
| Košnice spojene u drugo društvo (SPEC-19) ostaju imenovane u historijskom prinosu | spajanje ne dira zapisane `HarvestEntry` redove; imena se dopunjuju iz `GetMergedByApiaryIdAsync` |

## Trošak po pčelinjaku

Novo polje **`Expense.ApiaryId`** (nullable). `NULL` znači **zajednički trošak**, ne „nepoznato" —
zato je migracija sigurna: svi troškovi od prije ostaju `NULL` i ispravno se prikazuju kao zajednički.

Atribucija je **na računu**, ne na stavci. Račun koji pokriva dva pčelinjaka unosi se dvaput ili
ostaje zajednički.

**Nesklad se odbija (400):** račun označen za pčelinjak A ne smije nositi stavku vezanu za program
prehrane na pčelinjaku B — takav red bi na dva ekrana pripadao dvama pčelinjacima. Provjera je u
`ExpenseService.EnsureAttributionValidAsync`, koje ionako već učitava `Diet` i njegov `Apiary`, pa ne
košta nijedan dodatni upit. Zajednički račun (`NULL`) se ne kosi ni sa čim.

## Format izvoza

**PDF** — `shared/utils/seasonReportPdf.ts`, jsPDF, A4 **portrait** (registar tretmana je landscape
zbog 13 kolona). Dijeli lazy `pdfFont.ts` chunk s registrom tretmana — ugrađeni DejaVu Sans je ono
što drži č/ć/đ/š/ž ispravnim u dokumentu koji ide u opštinu.

Zaglavlje ima **prazne linije za adresu i JIB**. Organizacija ta polja nema (SPEC-22 D1 ih je
svjesno odbio), a prijava na subvenciju ih traži — linije su predviđene da se popune automatski kad
ta polja jednom dođu.

**Excel** — `shared/utils/seasonReportXlsx.ts`, `write-excel-file` (MIT), lazy. Do četiri lista:
`Prinos`, `Troškovi`, `Tretmani`, `Sažetak` — isključena sekcija ne dobija list (vidi gore).

> **Zašto ne SheetJS:** npm paket `xlsx` je zaglavljen na 0.18.5 iz 2022. — distribucija je
> preseljena na vlastiti CDN, pa se ne može pinati kao obična zavisnost.

**Bez grafikona.** Recharts je SVG i tražio bi canvas capture (još jedan paket); dokument za
subvenciju je tabelaran. Grafikoni ostaju na `/stats`.

**Tretmani su sažetak, ne registar.** Puni registar postoji kao zaseban PDF po pčelinjaku i godini
(SPEC-08); druga kopija je druga stvar koja se može razići s prvom.

## Zašto vlastiti slice, a ne `StatsService`

`Stats` odgovara na „tekuća godina, cijela organizacija, jedan ekran". Izvještaj odgovara na
„proizvoljan period, opciono jedan pčelinjak, jedan dokument". Spajanje bi `StatsDto` napunilo
poljima koja su na dashboardu besmislena, a izvještaj bi vukao 12-mjesečne serije koje mu ne trebaju.

## Fajlovi

**Backend:** `Domain/Common/ReportPeriod.cs` (čisto pravilo pripadnosti periodu),
`Application/Features/Reports/**`, `API/Controllers/ReportsController.cs`,
`Expenses/**` (`ApiaryId`), `ExpenseRepository.GetByOrganizationInRangeAsync`.

**Frontend:** `features/reports/ReportPage.tsx` + `useReportPreferences.ts` (izbor sekcija i formata,
pamćenje), `core/services/reportService.ts` + `reportQueries.ts`, `shared/utils/seasonReportPdf.ts` +
`seasonReportXlsx.ts` (oba primaju `ReportSections`), `features/expenses/**` (izbor pčelinjaka + čip
u listi).

**Testovi:** `ReportPeriodTests` (5), `ReportServiceTests` (9), `ExpenseServiceTests` (+6).

## Testni podaci

`Entity/Seed/ReportDataSeeder.cs` — **Development only**, isti režim kao demo nalozi, i no-op čim
postoji ijedno vrcanje. `InitialCreate` sije organizacije, pčelinjake, košnice i preglede ali
nijedno vrcanje, trošak ni tretman, pa bi `/reports` na svježoj bazi bio sam nule.

Redovi nisu ukras — svaki postoji da provjeri jedno pravilo koje se lako pogriješi a ne vidi se kad
se pogriješi:

| Red | Šta dokazuje |
|---|---|
| Vrcanje **bez cijene** (livadski) | `unpricedKg` i napomena o procjeni prihoda (D6) |
| Vrcanje **30.09. u 23:30 lokalno** (zapisano `21:30Z`) | granice perioda u lokalnoj zoni (D4) — mora biti u septembru, ne oktobru |
| Tretman **25.09.–05.10.** (Apiguard) | pripadnost po `StartDate` (D5) — samo u Q3, ne i u Q4 |
| Račun **bez pčelinjaka** (centrifuga, odijela) | zajednički trošak (D1) — ne razmazuje se po pčelinjacima |
| Račun u **EUR** (matice) | valute se ne sabiraju, bilansa je samo KM (D7) |
| Stavka vezana za **program prehrane** | sekcija „Prehrana po programu" |
| **Pašnjak + selidba** (Vlašić, 15.05.) | sekcija „Po pašnjaku" i `PastureAttribution` |
| Vrcanje **druge organizacije** (Mountain Bees) | međutenantsko odvajanje — ne smije se pojaviti |
| Podaci za **prošlu godinu** | brzi izbor „prethodna godina" nije prazan |

Godine se računaju iz `DateTime.UtcNow.Year`, ne fiksiraju — seeder ostaje koristan i sljedeće
sezone. Pčelinjaci i košnice se razrješavaju iz baze, ne hardkodiraju, isto kao u `SeedUsersAsync`.

Sarajevska ljetna razlika (+2 h) je hardkodirana u **jednom** redu, jer `AppTimeZone` živi u
Application sloju koji Entity ne referencira. Komentar u kodu to kaže; ako aplikacija ikad prestane
biti u toj zoni, taj red prestaje dokazivati svoju poentu i ništa drugo se ne lomi.

## Poznato ograničenje

`doc.save()` i `toFile()` preuzimaju datoteku kroz browser. U Capacitor webview-u (SPEC-23) to ne
radi kako treba i trebaće Filesystem/Share plugin — kod je pisan tako da se ta zamjena svede na dvije
funkcije, `downloadSeasonReportPdf` i `downloadSeasonReportXlsx`.
