# SPEC-25 — Sezonski i godišnji izvještaj ("Season report export")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-09-12) — see `features/season-report.md`; migracija još nije primijenjena |
| **Effort** | M/L (~2 dana) |
| **Depends on** | ništa novo. Čita SPEC-02 (vrcanja), SPEC-08 (tretmani), SPEC-10 (pašnjaci), SPEC-12 (prehrana); poštuje SPEC-24 (zaključavanje) |
| **New secrets / packages** | `write-excel-file` ^4.1.1 (MIT, frontend, lazy chunk). Bez novih NuGet paketa, bez novih env varijabli |
| **Breaking** | Ne — jedna nullable kolona (`Expense.ApiaryId`), sve ostalo aditivno |

## Goal

PDF se danas generiše samo za **QR naljepnice** i **evidenciju tretmana**. Prinos, troškovi i
statistika žive isključivo na ekranu — pčelar koji podnosi zahtjev za poljoprivrednu subvenciju ili
pravi godišnji pregled mora brojeve prepisivati rukom sa `/stats`.

Ovaj spec dodaje **jedan objedinjeni izvještaj za proizvoljan period**: prinos po pčelinjaku,
troškovi, procijenjeni prihod i sažetak tretmana, izvoziv u PDF i Excel.

Uzgredni ali samostalno koristan dio: **trošak se napokon može vezati za pčelinjak**. Bez toga
izvještaj ne može staviti trošak pored prinosa, jer je `Expense` danas vezan samo za organizaciju.

## Decisions (settled with Asim before implementation, 2026-09-12)

### D1 — `Expense.ApiaryId`, nullable, **na računu** — ne na stavci

`Expense` ima samo `OrganizationId`. Jedini postojeći put do pčelinjaka je
`ExpenseItem.DietId → Diet.ApiaryId`, što pokriva **isključivo prehranu** (SPEC-12 Faza E) — šećer
kupljen "za sve" nema gdje.

Razmatrano i odbijeno: `ExpenseItem.ApiaryId` (tačnije za račun koji pokriva dva pčelinjaka, ali
usporava unos i `ReceiptScanPage` dobija kolonu više), i nasljeđivanje račun→stavka uz mogućnost
odstupanja (dva polja, dvije migracije, pravilo koje treba testirati).

**`NULL` znači „zajednički trošak", ne „nepoznato".** To je i razlog zašto je migracija sigurna: svi
postojeći troškovi ostaju `NULL` i ispravno se prikazuju kao zajednički. Ništa se ne pogađa unazad.

### D2 — Nesklad između `Expense.ApiaryId` i `Diet.ApiaryId` se **odbija** (400)

Nakon D1 postoje dva puta do pčelinjaka i mogu se razići: račun označen za pčelinjak A sa stavkom
vezanom za program prehrane na pčelinjaku B. Takav red bi na dva ekrana pripadao dvama pčelinjacima.

Provjera ide u **postojeći** `ExpenseService.EnsureAttributionValidAsync` (prije: `EnsureItemDietsAttributableAsync`), koji već učitava
`Diet` i njegov `Apiary` radi provjere organizacije — dakle nijedan dodatni upit.

`NULL` se nikad ne kosi ni sa čim: zajednički račun smije nositi stavke prehrane s bilo kojeg
pčelinjaka. Odbija se samo **postavljen** `ApiaryId` koji se razlikuje od `Diet.ApiaryId`.

Alternative odbijene: „eksplicitno polje ima prednost" (tiho ostavlja kontradiktoran red u bazi) i
„Diet popunjava prazno" (izvještaj bi zavisio od pravila koje se ne vidi na ekranu troška).

### D3 — Slobodan raspon `od–do`, uz brze izbore

Jedan mehanizam pokriva mjesečni, kvartalni, godišnji i sezonski izvještaj. Brzi izbori na stranici:
tekuća godina, prethodna godina, Q1–Q4, pojedinačni mjesec, sezona (1.3.–31.10.).

Razlog zašto ne fiksni padajući izbornik kalendarskih perioda: period koji subvencija traži ne mora
se poklopiti s kvartalom, a Q1 je u pčelarstvu ionako prazan.

### D4 — Granice perioda se računaju u **lokalnoj zoni**, ne u UTC

`StatsService` koristi `DateTime.UtcNow.Year`. Za „septembar" to znači da vrcanje u 23:30 30.09. po
lokalnom vremenu upadne u oktobar. `ReportService` koristi `AppTimeZone.Resolve(config)` — mehanizam
koji već postoji i koji koriste Asistent i kalendar (SPEC-11).

Granice su **inkluzivne na oba kraja**: zapis pripada periodu kad njegov datum, **preveden u
lokalnu zonu**, padne u `[from, to]`. Korisnik bira datume, ne trenutke. Upit prema bazi
(`ReportPeriod.UtcBounds`) je namjerno dan širi na svakoj strani i nikad ne odlučuje o pripadnosti.

### D5 — Tretman pripada periodu po `StartDate`

Tretman koji počne 25.09. a završi 05.10. je u septembru. Isto pravilo koje `StatsService` već
koristi za programe prehrane (`d.StartDate.Year`).

Odbijeno „preklapanje" (tretman bi bio i u Q3 i u Q4, pa zbir kvartala više ne bi bio jednak godini —
u dokumentu za subvenciju to izgleda kao greška) i „po datumu završetka" (dva različita pravila u
istoj koloni, jer tretmani u toku nemaju `EndDate`).

### D6 — Prihod je **uvijek** označen kao procjena, uz broj kg bez cijene

`Harvest.PricePerKg` je nullable, a `StatsService` sabira prihod samo preko vrcanja koja cijenu
imaju. 200 kg bez upisane cijene doprinese 0 KM. Na dashboardu je to podnošljivo; u dokumentu koji
ide na subvenciju nije.

Izvještaj zato uz prihod **uvijek** navodi `pricedKg` / `unpricedKg`, a sam broj se zove
„Procijenjeni prihod". Kad `unpricedKg > 0`, PDF ispisuje eksplicitnu napomenu.

### D7 — Valute se grupišu, nikad ne sabiraju

`Expense.Currency` je slobodno polje (default `BAM`). `IExpenseRepository.GetTotalsByDietsAsync` već
vraća iznose **grupisane po valuti**, uz obrazloženje da bi miješanje bilo tiha laž. Izvještaj
nasljeđuje to pravilo: troškovi su lista `(valuta, iznos)`.

Bilansa (prihod − trošak) se računa **samo za BAM**, jer je prihod u BAM po definiciji
(`PricePerKg` je KM/kg). Kad postoje troškovi u drugoj valuti, oni se prikazuju ali ne ulaze u
bilansu, i dokument to kaže.

### D8 — Pristup: `Roles.Managers`, bez plan gate-a

OrganizationAdmin i ApiaryAdmin. **Pčelar ne** — već je read-only na vrcanjima i tretmanima, a
izvještaj sadrži finansije organizacije. SystemAdmin je u `Roles.Managers` na kontroleru, ali nema
svoju organizaciju, pa ga servis odbija s **403** i ruta `/reports` mu nije u meniju — isto pravilo
koje SPEC-22 D4 primjenjuje na `/organizations/my`.

Razmatran i odbijen novi `PlanFeature.ReportExport` (Standard+): prijava na subvenciju je tačno
trenutak kad bi upsell radio, ali izvještaj je i argument zašto aplikaciju uopšte držati — ostaje
dostupan na svim paketima.

Odbijena i varijanta „svi vide, sadržaj po ulozi": dva različita dokumenta koja se zovu isto.

### D9 — Zaglavlje ima prazne linije za adresu i JIB

SPEC-22 D1 je svjesno odbio kontakt i službena polja na organizaciji. Ovaj spec ih **ne vraća**.
PDF umjesto toga ima linije `Adresa: ______` i `JIB / ID broj: ______` koje pčelar popuni rukom.

Kad ta polja jednom dođu (Google Play ionako traži adresu, SPEC-23), popunjavaju se automatski —
linije su predviđene da se zamijene, ne da ostanu.

### D10 — Vlastiti slice, **ne** proširenje `StatsDto`

`Stats` je „tekuća godina, jedan ekran, uvijek cijela organizacija". Izvještaj je „proizvoljan
period, jedan dokument, opciono jedan pčelinjak". To su dva nespojiva ugovora i spajanje bi ih oboje
pokvarilo — `StatsDto` bi dobio polja koja su na dashboardu besmislena, a izvještaj bi vukao
12-mjesečne serije koje mu ne trebaju.

### D11 — PDF i Excel se prave iz **istog** DTO-a

Server računa, klijent samo crta. Da svaki format računa sam, dva dokumenta istog perioda bi se
prije ili kasnije razišla u brojci — a to je dokument koji ide u opštinu.

### D12 — Zaključavanje (SPEC-24) dolazi besplatno

`IAccessGuard.GetAccessibleApiariesAsync()` po defaultu **već** izbacuje zaključane pčelinjake i već
je uloga-skopiran. `ReportService` iz njega izvodi skup pčelinjaka i sve ostalo veže na taj skup —
dakle ovo **nije** osmo mjesto koje filtrira ručno, nego prvo koje ne mora.

### D13 — Kvačice za sekcije i izbor formata (dodano 2026-09-12, nakon prve isporuke)

Prva verzija je uvijek štampala sve sekcije i imala dva dugmeta (PDF / Excel). Asim je tražio izbor
sekcija, pamćenje izbora i eksplicitan izbor formata.

Isti izbor važi za **ekran i oba formata** — pregled je doslovno ono što izlazi iz datoteke. Izbor se
pamti u `localStorage`, i **spaja se preko defaulta** umjesto da se koristi sirov: izbor zapisan
prije nego je neka sekcija postojala inače bi je ostavio trajno isključenom.

**Napomene nisu isključive.** One su ono što drži brojku poštenom (D6, D7); isključiva napomena je
napomena koja će se isključiti.

U Excelu isključena sekcija **gubi list**, ne ostaje prazan — prazan tab čita se kao „nismo imali
podataka", što je druga tvrdnja od „nisam ovo tražio". `Sažetak` uvijek ostaje (nosi bilansu i
napomene). „Po košnici" je podrazumijevano isključeno: 40 košnica potroši cijelu stranu.

## User stories

- Kao pčelar podnosim zahtjev za subvenciju i izvezem godišnji izvještaj u PDF-u umjesto da brojeve prepisujem sa `/stats`.
- Kao pčelar izvezem Excel i proslijedim ga knjigovođi, koji radi u listovima a ne u PDF-u.
- Kao pčelar izvezem izvještaj za jedan pčelinjak jer subvencija ide po lokaciji.
- Kao pčelar upišem da je 40 KM šećera kupljeno za Gornji pčelinjak, i vidim bilansu tog pčelinjaka.
- Kao administrator organizacije izvezem kvartal da vidim gdje je sezona otišla.
- Kao pčelar vidim u dokumentu da 200 kg nema upisanu cijenu, umjesto da prihod tiho bude manji.

## Domain rules

| Pravilo | Gdje se provodi |
|---|---|
| `from` ≤ `to`; raspon ≤ 5 godina | `SeasonReportQueryValidator` |
| Granice u lokalnoj zoni, inkluzivne | `ReportService` preko `AppTimeZone` |
| Pčelinjaci = `GetAccessibleApiariesAsync()` — uloga + zaključavanje | `ReportService` |
| `apiaryId` izvan dosega → 403 | `IAccessGuard.EnsureCanManageApiaryAsync` |
| Vrcanje pripada periodu po `Harvest.Date` | `ReportService` |
| Trošak pripada periodu po `Expense.PurchaseDate` | `ReportService` |
| Tretman pripada periodu po `Treatment.StartDate` (D5) | `ReportService` |
| Trošak s `ApiaryId = NULL` ide u blok „Zajednički" | `ReportService` |
| Trošak vezan za pčelinjak izvan dosega se **ne** prikazuje | `ReportService` |
| Prihod = Σ (kg × `PricePerKg`) samo gdje cijena postoji; `unpricedKg` se broji odvojeno | `ReportService` |
| Troškovi grupisani po valuti; bilansa samo BAM (D7) | `ReportService` |
| `Expense.ApiaryId` mora pripadati vlastitoj organizaciji | `ExpenseService.CreateAsync`/`UpdateAsync` |
| `Expense.ApiaryId` ≠ `Diet.ApiaryId` njegove stavke → 400 (D2) | `ExpenseService.EnsureItemDietsAttributableAsync` |
| Spojene košnice (SPEC-19) ostaju u historijskom prinosu | nasljeđuje se iz `Harvest.Entries` — spajanje ne dira zapisane redove |

## API

| Method | Path | Ko | Vraća |
|---|---|---|---|
| GET | `/reports/season?from=&to=&apiaryId=` | `Roles.Managers` | `SeasonReportDto` |

`from` i `to` su obavezni (`yyyy-MM-dd`), `apiaryId` opcion (izostavljen = svi dostupni pčelinjaci).

```
SeasonReportDto
  header:     organizationName, from, to, generatedAt, apiaryNames[]
  yield:      totalKg, pricedKg, unpricedKg,
              byApiary[], byHoneyType[], byBeehive[], byPasture[]
  expenses:   byCurrency[], byApiary[], sharedByCurrency[], byDiet[]
  balance:    estimatedRevenueBam, totalExpenseBam, netBam, byApiary[]
  treatments: count, byProduct[], hivesTreated, activeKarencaCount
  notes:      unpricedKg, unassignedExpenseCount, nonBamCurrencies[]
```

Dodatno na postojećim ugovorima (aditivno, stari klijenti netaknuti):

- `ExpenseDto.ApiaryId` (`int?`) + `ApiaryName` (`string?`)
- `CreateExpenseDto.ApiaryId`, `UpdateExpenseDto.ApiaryId` (`int?`)

## Format izvoza

**PDF** — jsPDF, klijentski, isti obrazac kao `treatmentPdf.ts`: A4 **portrait** (za razliku od
registra tretmana koji je landscape zbog 13 kolona), DejaVu Sans iz postojećeg lazy `pdfFont.ts`
chunka. Sekcije redom: zaglavlje s praznim linijama (D9) → prinos → troškovi → bilansa → tretmani →
napomene → mjesto za potpis i pečat.

**Excel** — `write-excel-file` (MIT, browser-first, samo pisanje), lazy import kao i font.
Četiri lista: `Prinos`, `Troškovi`, `Tretmani`, `Sažetak`.

Odbijen **SheetJS/`xlsx`**: npm paket je zaglavljen na 0.18.5 iz 2022., distribucija je preseljena na
vlastiti CDN, pa se ne instalira kao normalna zavisnost. Odbijen i `exceljs` (težak, pravljen za Node).
Odbijen i CSV (gubi listove, i BiH Excel + zarez/tačka-zarez + UTF-8 lomi č/ć/š).

**Bez grafikona u v1.** Recharts je SVG i traži canvas capture (još jedan paket); dokument za
subvenciju je tabelaran. Grafikoni ostaju na `/stats`.

## Files

### Backend

| Fajl | Uloga |
|---|---|
| `Domain/Entities/Expense.cs` | `ApiaryId` (`int?`) + navigacija |
| `Entity/Configurations/ExpenseConfiguration.cs` | FK `SetNull`, indeks |
| `Entity/Migrations/…_AddExpenseApiary.cs` | jedna nullable kolona + FK + indeks |
| `Application/Features/Expenses/**` | `ApiaryId` kroz DTO-e, mapiranje, validator; D2 provjera u servisu |
| `Application/Features/Reports/**` | **novi slice** — `ReportService`, `IReportService`, DTO-ovi, `SeasonReportQueryValidator` |
| `Application/Common/Interfaces/IExpenseRepository.cs` + `Entity/Repositories/ExpenseRepository.cs` | `GetByOrganizationInRangeAsync` |
| `Application/DependencyInjection.cs` | registracija `IReportService` |
| `API/Controllers/ReportsController.cs` | **novi kontroler**, jedan endpoint |

### Frontend

| Fajl | Uloga |
|---|---|
| `features/reports/ReportPage.tsx` | **nova stranica** — izbor perioda, pregled, dva dugmeta |
| `core/services/reportService.ts` + `reportQueries.ts` | poziv + hook |
| `core/models/index.ts` | `SeasonReport` i pripadajući tipovi |
| `shared/utils/seasonReportPdf.ts` | **novo** — jsPDF, dijeli `pdfFont.ts` chunk |
| `shared/utils/seasonReportXlsx.ts` | **novo** — `write-excel-file`, lazy |
| `features/expenses/**` | izbor pčelinjaka na formi troška + kolona u listi |
| `App.tsx`, `Sidebar.tsx`, `usePermissions.ts` | ruta `/reports` + stavka u meniju za `Roles.Managers` |
| `core/help/helpContent.ts` + `helpRoutes.ts` | pomoć za stranicu (SPEC-14) |

## Phases

Faze su nezavisno isporučive.

1. **`Expense.ApiaryId`** — entitet, konfiguracija, migracija, DTO-ovi, validator, D2 provjera, polje na formi troška i kolona u listi. *Korisno i bez izvještaja.*
2. **`ReportService` + endpoint** — agregacija, DTO, validator, kontroler, testovi.
3. **Stranica + PDF** — `/reports`, izbor perioda, pregled na ekranu, izvoz u PDF.
4. **Excel** — `write-excel-file`, četiri lista.

## Out of scope (v1)

- Grafikoni u PDF-u
- Cijeli registar tretmana unutar izvještaja (postoji kao zaseban PDF po pčelinjaku/godini, SPEC-08) — ovdje ide samo sažetak
- Poređenje s prethodnim periodom („prošle godine X kg") — dashboard funkcija, ne dokument
- Slanje izvještaja e-mailom
- Zakazani izvještaji (npr. automatski na kraju godine)
- Kontakt i službena polja organizacije (D9) — ostaju prazne linije
- Trošak podijeljen po stavkama između pčelinjaka (D1)
- Amortizacija opreme, radni sati, vrijednost zaliha meda
- Izvoz u formatu koji traže konkretne opštinske aplikacije

## Acceptance criteria

- [x] Trošak se može vezati za pčelinjak ili ostaviti kao zajednički; postojeći troškovi ostaju zajednički
- [x] Račun čiji se pčelinjak kosi s pčelinjakom programa prehrane njegove stavke se odbija s 400
- [x] Zajednički račun (`ApiaryId = NULL`) smije nositi stavke prehrane s bilo kojeg pčelinjaka
- [x] `/reports` je vidljiv OrgAdminu i ApiaryAdminu, nije vidljiv pčelaru
- [x] Pčelar na `GET /api/reports/season` dobija `403`
- [x] Brzi izbori popunjavaju raspon; ručni raspon radi jednako
- [x] `from > to` se odbija na klijentu i na serveru
- [x] Vrcanje u 23:30 posljednjeg dana perioda je **unutar** perioda (lokalna zona, D4)
- [x] Tretman koji prelazi granicu perioda pojavljuje se samo u periodu svog početka
- [x] Zaključani pčelinjaci (SPEC-24) se ne pojavljuju ni u jednom dijelu izvještaja
- [x] Prihod je označen kao procjena; kg bez cijene su iskazani zasebno
- [x] Troškovi u različitim valutama su odvojeni; bilansa je samo BAM i to piše
- [x] PDF ima ispravne č/ć/đ/š/ž i prazne linije za adresu i JIB
- [x] Excel ima četiri lista (`Prinos`/`Troškovi`/`Tretmani`/`Sažetak`), ispravan OOXML zip, dijakritiku i brojčani format na iznosima — *provjereno raspakiravanjem datoteke, nije otvarano u Microsoft Excelu*
- [x] PDF i Excel istog perioda daju iste brojeve
- [x] Prazan period daje dokument s nulama i porukom, ne grešku
- [x] Kvačice biraju sekcije; isti izbor važi za ekran, PDF i Excel
- [x] Isključivanje „Prinosa" povlači podtabele, uključivanje ih vraća
- [x] Isključena sekcija u Excelu gubi cijeli list; `Sažetak` uvijek ostaje
- [x] Bez ijedne sekcije dugme za izvoz je onemogućeno uz poruku
- [x] Izbor sekcija i formata preživi osvježavanje stranice
- [x] Stranica radi u tamnoj i svijetloj temi i na širini telefona
- [x] `ReportServiceTests` — agregacija, granice perioda, valute, zaključavanje; cijeli paket prolazi
