# SPEC-22 — Moja organizacija ("Organization self-service")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-08-30) — see `features/organization-profile.md` |
| **Effort** | M (~1 dan) |
| **Depends on** | ništa. Dodiruje SPEC-05 (`IFileStorage`), SPEC-09 (paket ostaje kod SystemAdmina), SPEC-16 (aktivnost) |
| **New secrets / packages** | nema |
| **Breaking** | Ne — dvije nullable kolone, sve ostalo aditivno |

## Goal

Korisnik se registruje i **pri registraciji sam napravi organizaciju** (`AuthService.RegisterAsync`),
ali je od tog trenutka ne može ni pogledati ni promijeniti. Ako se prekuca u nazivu, jedini način da
se to ispravi je da Asim to uradi umjesto njega kroz `/admin`. Ovaj spec vraća vlasništvo nad
organizacijom onome ko je njen administrator.

Drugi dio: sistemske tabele **Organizacije** i **Korisnici** nose podatke koje DTO već ima a ekran ne
prikazuje (`createdByName`, `phone`, `createdAt`), i nemaju nijedan filter ni sortiranje — pa se za
"ko nije platio" i "ko se nikad nije prijavio" scrolla rukom.

## Decisions (settled with Asim before implementation, 2026-08-30)

### D1 — Samo naziv, opis i logotip. Ništa više.

Ponuđena su i polja kontakta (e-pošta, telefon, adresa, grad, web) i službeni podaci (JIB/ID broj,
broj registra pčelara) — s obrazloženjem da bi se kasnije mogli štampati u zaglavlju PDF registra
tretmana (SPEC-08). **Odbijeno za v1**: "samo osnovna polja, za sad". Logotip je jedini dodatak koji
je Asim sam tražio.

Posljedica: migracija dodaje **dvije** kolone (`LogoStoragePath`, `LogoContentType`), ne osam. Ako
kontakt polja jednom zatrebaju, dodaju se onda — ne "za svaki slučaj".

### D2 — Zasebna stranica `/organization`, ne sekcija na profilu

Organizacija nije lični podatak. Profil je "ja", ovo je "mi". Stranica ima i mjesta da poraste
(članovi, paket) ako to jednom zatreba.

### D3 — OrgAdmin **smije** mijenjati naziv

Razmatrano je da naziv ostane zaključan za SystemAdmina (stabilnost evidencije i naplate). Odbijeno:
organizaciju je taj čovjek sam napravio pri registraciji, i "uredi organizaciju" u kojoj se glavno
polje ne može urediti djeluje kao nedovršen ekran. SystemAdmin i dalje može sve isto kroz `/admin`.

### D4 — Organizacija se **uvijek** čita iz tokena, nikad iz rute

Nema `/api/organizations/{id}`. Svaka metoda `OrgProfileService`-a razrješava organizaciju iz
`ICurrentUser.OrganizationId`, pa ne postoji id koji se može podmetnuti i ne postoji `IAccessGuard`
provjera koja se može zaboraviti. Podjela prava je na kontroleru: **čita svaki član**, **piše samo
OrganizationAdmin**.

SystemAdmin nema svoju organizaciju → `403`, ne `404`: red postoji za sve ostale, on samo nije ni u
jednoj. Zato `/organization` ni ne postoji u njegovom meniju.

### D5 — Logotip ide kroz `IFileStorage`, kao fotografije pregleda (ADR-027)

Bez javnog URL-a, bez `LogoUrl` kolone. Blob se streama kroz API uz provjeru prava. Cijena je da
`<img src>` ne može nositi Bearer header, pa se slika dohvaća kroz `apiClient` i renderuje iz
object URL-a — isti obrazac koji `InspectionPhotos` već koristi.

`Cache-Control` je **`private, no-cache`**, ne `max-age=86400` kao kod fotografije pregleda: URL
logotipa se nikad ne mijenja, pa bi zamijenjeni logotip cijeli dan pokazivao staru sliku.

### D6 — "Zadnja prijava" se izračunava, ne pamti

`MAX(RefreshToken.CreatedAt)` po korisniku. Token se piše pri prijavi **i** pri svakom osvježavanju
sesije, pa je to zapravo "zadnji put kad je ovaj račun korišten" — što je korisnije od čiste prijave.
Isti izvor koji ADR-034 već koristi za aktivnost organizacije, i isti razlog: tačno je i retroaktivno,
bez nove kolone i bez heartbeata. Vidi ADR-040.

### D7 — Vlasnik organizacije je **OrgAdmin**, ne `CreatedBy`

`Organization.CreatedById` je pogrešan podatak za kolonu "Vlasnik": kod samoregistracije jeste
osnivač, ali kod organizacije koju je napravio SystemAdmin to je **Asim**. Pravilo je: OrgAdmin čiji
je id `CreatedById` (osnivač koji je i dalje admin), inače OrgAdmin s najstarijim računom, inače
`null` — a "bez admina" je stanje koje se u tabeli **treba** vidjeti.

Računa se iz `org.Users`, koje repozitorij ionako `Include`-a → nijedan dodatni upit.

## User stories

- Kao administrator organizacije ispravim naziv koji sam pogriješio pri registraciji, bez da pišem Asimu.
- Kao administrator organizacije postavim logotip svog gazdinstva, i vidim ga uz naziv.
- Kao Asim, u sistemskoj tabeli odmah vidim koga da nazovem za neplaćeni paket, bez otvaranja tabele korisnika.
- Kao Asim, filtriram organizacije na "istekao paket" i tabela mi postane radna lista za naplatu.
- Kao Asim, filtriram korisnike na "nikad se nije prijavio" i vidim račune koje sam napravio a niko ih nije otvorio.
- Kao Asim, sortiram po zadnjoj aktivnosti i najstarije ide na vrh.

## Domain rules

| Pravilo | Gdje se provodi |
|---|---|
| Naziv obavezan, ≤ 200 znakova; opis ≤ 1000 | `UpdateMyOrganizationValidator` (iste granice kao kolone i kao SystemAdmin forma) |
| Prazan opis se sprema kao `null`, ne kao `""` | `OrgProfileService.UpdateMyOrganizationAsync` |
| Logotip ≤ 2 MB | `OrgProfileService.MaxLogoBytes` |
| Format se čita iz **header bajtova**, ne iz `Content-Type` i ne iz ekstenzije | `ImageRules.SniffContentTypeAsync` |
| Zamjena logotipa briše stari blob — **tek nakon** što je novi ključ zapisan | `SetLogoAsync` |
| Brisanje blob-a je best-effort; kvar u storageu ne poništava zapisanu promjenu | `TryDeleteBlobAsync` |
| Red koji pokazuje na nestali blob → `404`, ne `500` | `OpenLogoAsync` |

Promjena naziva **ne ukida sesije**: naziv nije JWT claim (`organizationId` jeste, a on se ne mijenja).
Cached sesija u `localStorage` nosi `organizationName`, pa je stranica osvježi kroz `updateUser` —
inače bi stari naziv stajao ispod avatara do sljedeće prijave.

## API

| Method | Path | Ko | Vraća |
|---|---|---|---|
| GET | `/organizations/my` | svaki član | `MyOrganizationDto` |
| PUT | `/organizations/my` | OrgAdmin | `200 + MyOrganizationDto` |
| POST | `/organizations/my/logo` | OrgAdmin | `200 + MyOrganizationDto`, multipart `file` |
| GET | `/organizations/my/logo` | svaki član | image stream |
| DELETE | `/organizations/my/logo` | OrgAdmin | `200 + MyOrganizationDto` |
| GET | `/admin/organizations/{id}/logo` | SystemAdmin | image stream |

`MyOrganizationDto` namjerno **nije** `AdminOrganizationDto`: paket, napomena o uplati i aktivnost su
podaci SystemAdmin ekrana, ne nešto što tenant gleda o sebi. Brojevi (`userCount`, `apiaryCount`,
`beehiveCount`) su unutra da stranica bude stranica, a ne dva polja.

## Sistemske tabele

### Organizacije

Nove kolone: **logotip uz naziv** i **Vlasnik** (ime + e-pošta + telefon, `+N` kad ih je više,
`mailto:`/`tel:` linkovi). Filteri: **Paket** (svaki paket + "⏰ Istekao paket") i **Aktivnost**
(≤30 d / 30–90 d / >90 d / nikad — isti pragovi koje boji `ActivityCell`, SPEC-16 §0 D1). Sortiranje
klikom na zaglavlje po svakoj koloni.

### Korisnici

Nove kolone: **Kontakt** (e-pošta s oznakom je li potvrđena + telefon), **Zadnja prijava** (bojena
istom 30/90 skalom kao aktivnost organizacije) i **Registrovan**. Kod pčelara se uz organizaciju
prikazuje i broj dodijeljenih košnica. Filteri: **Uloga** i **Status** (nepotvrđena e-pošta / nikad
se nije prijavio / bez prijave 90+ dana).

### Vitals

Peta pločica: **Istekli paketi**. Naplata je ručna i godišnja (SPEC-09 v1) i nema posao koji
podsjeća — ova pločica je podsjetnik.

Kad je filter aktivan, brojka pored naslova sekcije postaje `prikazano / ukupno`, da filter nikad ne
bude nevidljiv.

## Files

### Backend

| Fajl | Uloga |
|---|---|
| `Domain/Entities/Organization.cs` | `LogoStoragePath`, `LogoContentType` (oba nullable) |
| `Entity/Configurations/OrganizationConfiguration.cs` | dužine 500 / 100 |
| `Entity/Migrations/…_AddOrganizationLogo.cs` | dvije nullable kolone |
| `Application/Common/Validation/ImageRules.cs` | **novo** — sniffer JPEG/PNG/WebP iz magic bajtova |
| `Application/Features/OrgProfile/**` | **novi slice** — servis, DTO-ovi, validator |
| `API/Controllers/OrganizationsController.cs` | pet endpointa uz postojeći `my-plan` |
| `Application/Common/Interfaces/IUserRepository.cs` + `Entity/Repositories/UserRepository.cs` | `GetLastLoginAtAsync` |
| `Application/Features/Admin/**` | `AdminOrganizationDto` (vlasnik + `hasLogo`), `AdminUserDto` (`emailVerifiedAt`, `lastLoginAt`), `OpenOrganizationLogoAsync` |

### Frontend

| Fajl | Uloga |
|---|---|
| `features/organization/MyOrganizationPage.tsx` | **nova stranica** |
| `core/services/orgService.ts` + `orgQueries.ts` | pet poziva + hookovi (object URL u cacheu, revoke pri zamjeni) |
| `shared/utils/imageDownscale.ts` | `prepareLogoForUpload` — PNG/WebP koji staje prolazi netaknut |
| `features/admin/AdminDashboardPage.tsx` | kolone, filteri, sortiranje, peta pločica |
| `App.tsx`, `Sidebar.tsx`, `usePermissions.ts`, `Layout.tsx` | ruta `/organization` + stavka u meniju za OrgAdmina |
| `core/help/helpContent.ts` + `helpRoutes.ts` | pomoć za stranicu (SPEC-14) |
| `core/context/AuthContext.tsx` + `authService.ts` | `updateUser` sad prima i `organizationName` |

## Out of scope (v1)

- Kontakt i službeni podaci organizacije (D1) — prvi sljedeći kandidat ako zatrebaju na PDF registru
- Logotip u zaglavlju PDF registra tretmana i na QR naljepnicama
- Logotip u sidebaru umjesto 🐝 (razmatrano, odbijeno kao špekulacija)
- SystemAdmin koji postavlja logotip tuđoj organizaciji
- CSV izvoz sistemskih tabela (ponuđeno, nije izabrano)
- Proširiv red s detaljima u tabelama (ponuđeno, nije izabrano)
- Historija promjena naziva organizacije

## Acceptance criteria

- [x] OrgAdmin vidi "Moja organizacija" u meniju; ApiaryAdmin, pčelar i SystemAdmin je ne vide
- [x] `/organization` mijenja naziv i opis i sprema ih
- [x] Promijenjeni naziv se odmah vidi i u cached sesiji (labela ispod avatara), bez ponovne prijave
- [x] "Spremi promjene" je onemogućeno dok se ništa nije promijenilo
- [x] Prazan naziv se odbija na klijentu i na serveru
- [x] Logotip se postavlja, zamjenjuje i uklanja; zamjena briše stari blob
- [x] Datoteka koja nije slika se odbija po header bajtovima, prije nego išta uđe u storage
- [x] Fajl preko 2 MB se odbija prije zapisa
- [x] Organizacija se razrješava iz tokena — nijedan endpoint ne prima id organizacije
- [x] SystemAdmin na `/organizations/my` dobija `403`
- [x] Sistemska tabela organizacija: logotip, vlasnik (ime/e-pošta/telefon), `+N` za više admina, "bez admina"
- [x] Sistemska tabela korisnika: potvrđena/nepotvrđena e-pošta, telefon, zadnja prijava, registrovan
- [x] Filteri i sortiranje rade u obje tabele; brojka pokazuje `prikazano / ukupno` kad je filter aktivan
- [x] Pločica "Istekli paketi" broji organizacije s isteklim `planValidUntil`
- [x] Stranica i tabele rade u tamnoj i svijetloj temi i na širini telefona (bez horizontalnog scrolla stranice)
- [x] `OrgProfileServiceTests` — 9 testova; cijeli paket 619/619 prolazi

## Verification note

Provjereno bez baze i bez backenda, po `melarium-local-verification-limits`: privremena `/__preview`
ruta koja seeda React Query mock podacima i renderuje **prave** stranice, vođena browser alatima
(sortiranje, oba filtera, dirty-state dugmeta, boje ikona potvrde, 375 px i 1440 px, obje teme),
zatim obrisana. Backend je provjeren `dotnet build` + `dotnet test`. **Migracija nije primijenjena** —
`dotnet ef database update` ide na produkciju pri deployu.
