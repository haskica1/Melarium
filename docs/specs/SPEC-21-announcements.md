# SPEC-21 — Šta je novo ("Announcements")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-08-28) — see `features/announcements.md`. Migracija `20260828132327_AddAnnouncements` **još nije primijenjena na produkciji** |
| **Effort** | M (~1.5–2 dana) |
| **Depends on** | ništa. Prepisuje obrazac SPEC-06 (Edukacija): autor je SystemAdmin, sadržaj je platformski, stanje po korisniku |
| **New secrets / packages** | nema |
| **Breaking** | Ne — čisto aditivno (dvije nove tabele, nijedan postojeći potrošač se ne dira) |

## Goal

Melarium dobija novu funkcionalnost skoro svake sedmice — Sastavljanje društava, AI Asistent,
Kontakt — a korisnik za to sazna slučajno ili nikako. Danas ne postoji nijedan kanal kojim
operater platforme može reći "ovo je novo i ovako se koristi".

Ovaj spec daje SystemAdminu da napiše kratku objavu (naslov + opis), objavi je, i da je **svaki
korisnik vidi kao banner na vrhu stranice**. Klik otvara modal s cijelim tekstom; klik na "x" je
trajno sklanja. Sve objave, i stare i sklonjene, ostaju na stranici **"Šta je novo"**.

## User stories

- Kao Asim, napišem objavu, pogledam je kao nacrt, i objavim je jednim klikom — bez deploya.
- Kao korisnik, kad se prijavim, na vrhu vidim da je nešto novo, u jednoj rečenici.
- Kao korisnik koga to ne zanima, kliknem "x" i banner se više nikad ne pojavi za tu objavu —
  ni na telefonu, ni na laptopu.
- Kao korisnik koji je pročitao objavu, banner mi nestane sam — ne moram ga posebno gasiti.
- Kao korisnik koji se sjetio da je nešto bilo objavljeno prije tri mjeseca, nađem to na
  stranici "Šta je novo".
- Kao Asim, ispravim tipfeler u već objavljenoj objavi bez da se banner vrati ljudima koji su ga
  već sklonili.

## Domain rules

### D1 — Banner prikazuje **samo zadnju objavu** (pročitati prvo)

Banner nikad ne pokazuje red čekanja. Prikazuje **jednu** objavu: najnoviju objavljenu, i to samo
ako je taj korisnik nije već vidio. Kad je vidi — banner je prazan sve dok se ne objavi nova.

Alternativa koja je razmatrana i **odbačena**: "najnovija **neviđena**", koja bi se unazad probijala
kroz sve propuštene objave. Odbačena je jer korisnik koji se vrati nakon tri objave dobija tri
bannera zaredom, a novi korisnik zid od njih.

**Prihvaćena posljedica:** ako se dvije objave objave između dvije korisnikove prijave, **stariju
neće vidjeti u banneru.** Ovo je namjerno, ne propust. Hvata ga D8 (brojač u meniju) i stranica
"Šta je novo", gdje sve i dalje stoji.

### D2 — Jedno stanje: "viđeno", ne dva

Postoji **jedan** marker po (objava, korisnik). Postavlja ga bilo šta od ovoga:

- klik na "x" na banneru,
- zatvaranje modala nakon što je otvoren s bannera.

Nema odvojenog "pročitano" i "odbačeno". Ako je korisnik pročitao cijeli tekst, banner mu nema šta
više reći; "x" je samo prečica za one koje ne zanima — ista posljedica, samo brža. Ako se modal
otvori sa stranice "Šta je novo", marker se postavlja jednako (objava se tamo prikaže kao pročitana).

### D3 — Stanje je u bazi, ne u `localStorage`

Prati `LearningTopicRead`, ne `helpStorage.ts`. Razlog: `localStorage` je po pregledniku, pa bi
korisnik sklonio banner na telefonu i dočekao ga na laptopu — tačno ono zbog čega "x" i postoji.

### D4 — Objava **ne** upisuje ništa u `Notification`

Edukacija na prvu objavu upiše red po **svakom** korisniku (`NotifyManyInAppAsync`). Ovdje se to ne
radi, iz dva razloga:

1. Korisnik bi sklonio banner i i dalje imao nepročitanu stavku u zvonu za istu stvar.
2. Marker-model je jeftiniji: red u bazi nastaje tek kad korisnik nešto uradi, a ne po jedan red za
   svakog korisnika unaprijed, na svaku objavu.

Zvono ostaje za ono što se desilo **korisnikovim košnicama**. Banner je za ono što se promijenilo
**u aplikaciji**. Dvije različite stvari.

### D5 — Bez ciljanja: svi vide sve

Nema filtriranja po paketu ni po ulozi. Korisnik Besplatnog paketa vidi i objavu o funkciji koju
nema — to je namjerno (izričita odluka). SystemAdmin vidi banner kao i svi ostali; to je jedini
način da provjeri kako objava izgleda.

### D6 — Naslov + opis, ništa više

Objava ima **naslov** i **opis u markdownu**. Nema slike, nema video linka, nema CTA dugmeta
"Otvori funkcionalnost". Markdown je izabran umjesto čistog teksta jer `MarkdownArticle.tsx` već
postoji iz SPEC-06, pa liste s tačkama i podebljano ne koštaju ništa — a najava funkcionalnosti je
prirodno lista.

Banner **nema zasebno polje za sažetak, ali ni izvedeni podnaslov**. Prikazuje samo **tip, naslov i
link "Pročitaj više"** — sadržaj ide u modal. Prvi nacrt je u banner stavljao skraćenu prvu liniju
opisa (preko `stripMarkdown.ts`); izbačeno je jer je banner činilo višim, a i dalje nije reklo
dovoljno da se preskoči otvaranje modala. Zato `stripMarkdown.ts` ostaje u `features/learning/` —
ovaj feature ga ne koristi.

### D7 — Tip objave: Novo / Poboljšanje / Ispravka

`AnnouncementType` je obavezan i prikazuje se kao badge u banneru, u modalu i na stranici. Na
stranici "Šta je novo" je ujedno i filter.

### D8 — Brojač u meniju hvata ono što banner propusti

Stavka "Šta je novo" u meniju nosi badge s brojem neviđenih objava — isti obrazac kao
`feedbackNewCount` u `Sidebar.tsx`. Ovo je protuteža D1: korisnik koji je propustio objavu jer je
banner već prešao na noviju, i dalje vidi da ga nešto čeka.

### D9 — Izmjena poslije objave ne vraća banner

`PublishedAt` se postavlja **samo pri prvoj objavi** (isti guard kao `LearningTopic`). Uređivanje
teksta ne dira `AnnouncementRead` redove: ko je sklonio, ostaje sklonjeno. Ispravka tipfelera ne
smije nikoga ponovo gnjaviti.

Skidanje s objave (`IsPublished = false`) sklanja objavu i s bannera i sa stranice, ali `PublishedAt`
ostaje — ponovna objava se vraća na svoje mjesto u hronologiji, ne na vrh.

### D10 — Poredak ide po `PublishedAt`, ne po `CreatedAt`

Nacrt napisan u januaru a objavljen u martu je **martovska** objava. Sortiranje po `CreatedAt` bi ga
zakopalo ispod stvari objavljenih prije njega.

## Backend

### Domain

`Melarium.Domain/Entities/Announcement.cs`

| Polje | Tip | Napomena |
|---|---|---|
| `Title` | `string` | obavezno, max 150 |
| `BodyMarkdown` | `string` | obavezno |
| `Type` | `AnnouncementType` | obavezno |
| `IsPublished` | `bool` | default `false` |
| `PublishedAt` | `DateTime?` | prva objava, guard za D9; ujedno ključ sortiranja (D10) |
| `Reads` | `List<AnnouncementRead>` | |

`Melarium.Domain/Entities/AnnouncementRead.cs` — `AnnouncementId`, `UserId` (+ navigacije).
Jedinstven po `(AnnouncementId, UserId)`, kao `LearningTopicRead`.

`Melarium.Domain/Enums/AnnouncementType.cs`

```
New = 1, Improvement = 2, Fix = 3
```

Bosanske oznake (`Novo` / `Poboljšanje` / `Ispravka`) idu u frontend label mapu, kao
`LearningCategoryLabels` — enum ostaje engleski, po postojećem obrascu.

### Entity (persistence)

- `Configurations/AnnouncementConfiguration.cs` — tabela `Announcements`, indeks na
  `IsPublished` i na `PublishedAt`, kaskada na `Reads`.
- `Configurations/AnnouncementReadConfiguration.cs` — tabela `AnnouncementReads`, **unique**
  `(AnnouncementId, UserId)`, kaskada na `User`.
- `Repositories/AnnouncementRepository.cs` + `IAnnouncementRepository` u
  `Application/Common/Interfaces`:

```
Task<IEnumerable<Announcement>> GetPublishedAsync(AnnouncementType? type = null);
Task<Announcement?> GetLatestPublishedAsync();
Task<Announcement?> GetPublishedByIdAsync(int id);
Task<IEnumerable<Announcement>> GetAllForAdminAsync();
Task<HashSet<int>> GetReadIdsAsync(int userId);
Task<int>  GetUnreadCountAsync(int userId);
Task<bool> HasReadAsync(int announcementId, int userId);
Task AddReadAsync(AnnouncementRead read);
```

- Novi `DbSet`-ovi u `MelariumDbContext` (dozvoljeno po `ignore.md` — samo dodavanje).
- Registracija u `UnitOfWork`.

### Application

`Features/Announcements/` — `IAnnouncementService`, `AnnouncementService`, `DTOs/`, `Validators/`.

| DTO | Za koga |
|---|---|
| `AnnouncementSummaryDto` | stavka liste: `Id, Title, Type, PublishedAt, IsRead` |
| `AnnouncementDetailDto` | + `BodyMarkdown` |
| `AnnouncementListDto` | `{ Items, UnreadCount }` — obrazac `NotificationListDto` |
| `AnnouncementBannerDto` | `{ Announcement (nullable, s tijelom), UnreadCount }` |
| `AdminAnnouncementDto` | + `IsPublished`, `CreatedAt`, `UpdatedAt` |
| `SaveAnnouncementDto` | `Title, BodyMarkdown, Type` |
| `PublishAnnouncementDto` | `IsPublished` |

`MarkReadAsync` je idempotentan (`HasReadAsync` guard + unique indeks iza njega), tačno kao
`LearningTopicService.MarkReadAsync`.

### API

Potrošnja — `Controllers/AnnouncementsController.cs`, `[Route("api/announcements")]`, `[Authorize]`:

| Metod | Ruta | Vraća |
|---|---|---|
| `GET` | `/api/announcements?type=` | `AnnouncementListDto` — objavljene, najnovije prve |
| `GET` | `/api/announcements/banner` | `AnnouncementBannerDto` — zadnja objava + broj neviđenih |
| `GET` | `/api/announcements/{id}` | `AnnouncementDetailDto` |
| `POST` | `/api/announcements/{id}/read` | `204` |

`/banner` nosi **i tijelo objave i brojač**, jer se zove sa svake stranice: banner, modal i badge u
meniju se time namiruju jednim pozivom umjesto tri.

Autorstvo — `Controllers/Admin/AnnouncementsAdminController.cs`,
`[Route("api/admin/announcements")]`, `[Authorize(Roles = Roles.SystemAdmin)]`:
`GET`, `GET /{id}`, `POST`, `PUT /{id}`, `DELETE /{id}`, `PUT /{id}/publish`.

Validacija: `SaveAnnouncementValidator` (FluentValidation, `ValidateAsync` u kontroleru po ADR-010) —
naslov obavezan i ≤ 150, opis obavezan, tip mora biti validan član enuma. Objava praznog opisa se
odbija u servisu, isto kao kod Edukacije.

### Migracija

`dotnet ef migrations add AddAnnouncements` (iz `backend/Melarium.Entity`). Dvije nove tabele,
nijedna postojeća se ne dira.

## Frontend

### Novi fajlovi

| Fajl | Šta radi |
|---|---|
| `core/services/announcementService.ts` | HTTP pozivi |
| `core/services/announcementQueries.ts` | `useAnnouncementBanner`, `useAnnouncements`, `useMarkAnnouncementRead`, + admin hooks |
| `shared/components/AnnouncementBanner.tsx` | Banner + vlastito stanje modala |
| `shared/components/AnnouncementModal.tsx` | `Modal` + `MarkdownMessage` |
| `features/announcements/AnnouncementsPage.tsx` | Stranica "Šta je novo" (`/announcements`) |
| `features/admin/AnnouncementsAdminPage.tsx` | Lista objava + nacrta, prekidač objave |
| `features/admin/AnnouncementFormPage.tsx` | Pisanje/uređivanje + pregled prije objave |

### Izmjene postojećih

| Fajl | Izmjena |
|---|---|
| `Layout.tsx` | `<AnnouncementBanner />` unutar `<main>`, **iznad** `<ErrorBoundary>` — poravnat sa sadržajem stranice i preživi pad stranice |
| `Sidebar.tsx` | Stavka "Šta je novo" (`visible: true`, kao Edukacija) + badge s `unreadCount` |
| `App.tsx` | `/announcements`, `/admin/announcements`, `/admin/announcements/new`, `/admin/announcements/:id/edit` |
| `core/models/index.ts` | `AnnouncementType`, `AnnouncementTypeLabels`, tipovi DTO-a |
| `AdminDashboardPage.tsx` | Ulaz u upravljanje objavama, pored postojećeg za Edukaciju |

### Dvije sitnice oko ponovnog korištenja

**Markdown renderer: `MarkdownMessage`, ne `MarkdownArticle`.** Oba postoje. `MarkdownArticle` je
stilizovan za cijelu stranicu članka (`mt-6` naslovi, razmaci za dugo štivo) i živi u
`features/learning/` — komponenta u `shared/` ne bi trebala uvoziti iz feature foldera.
`MarkdownMessage` je već u `shared/components/`, kompaktan je, i to je tačno razmjer teksta u modalu.

**`stripMarkdown.ts` ostaje gdje jeste.** Nacrt ga je selio u `shared/utils/` zato što je banner
trebao izvedeni podnaslov; kad je podnaslov izbačen (D6), jedini uvoznik je opet
`LearningTopicPage.tsx`, pa selidbe nema.

### Ponašanje bannera

- `useAnnouncementBanner` sa `staleTime` 5 min — objave se mijenjaju rijetko, a upit ide sa svake
  stranice. Bez pollinga (za razliku od zvona i njegovih 30 s).
- Renderuje se samo kad `banner.announcement` nije `null`. Server već izuzima viđene.
- Klik na tijelo bannera → modal. Zatvaranje modala → `POST /read` (D2).
- Klik na "x" → `POST /read` odmah, bez otvaranja modala.
- Optimistično sakrivanje: banner nestaje na klik, ne čeka odgovor servera.

## Edge cases

| Slučaj | Ponašanje |
|---|---|
| Nema nijedne objavljene objave | `/banner` vraća `null` — banner se ne renderuje, badge ne postoji |
| Korisnik je vidio zadnju objavu | Isto — banner prazan dok se ne objavi nova |
| Dvije objave između dvije prijave | Banner pokazuje samo noviju; starija ostaje u brojaču i na stranici (D1) |
| Offline (SPEC-07) | Upit padne, banner se ne renderuje. Ništa ne puca, ništa se ne javlja |
| Dupli `POST /read` | No-op (guard + unique indeks) |
| Objava skinuta dok je modal otvoren | Modal ostaje otvoren do zatvaranja; sljedeći upit je više ne vraća |
| Brisanje korisnika | `AnnouncementReads` kaskadno odlaze s korisnikom |
| Brisanje objave | Kaskadno briše svoje `Reads` |
| Vrlo dug naslov | Banner ga skraćuje s `truncate`, modal prikazuje cijeli |
| Vrlo dug opis | Ne utiče na banner — banner ne prikazuje sadržaj, samo tip, naslov i link |

## Out of scope (v1)

- Slika / screenshot / GIF u objavi (odbijeno za v1, ne zaboravljeno — `IFileStorage` postoji)
- CTA dugme "Otvori funkcionalnost" s deep linkom
- Ciljanje po paketu ili ulozi (D5)
- Email uz objavu — isto obrazloženje kao Edukacija: mail po korisniku po objavi je spam
- AI pomoć pri pisanju objave (postoji za Edukaciju, ovdje nije tražena)
- Zakazana objava (objavi u petak u 9h)
- Verzija izdanja ("v1.4") kao polje

## Phases

**Faza 1 — backend + autorstvo.** Entiteti, migracija, repo, servis, oba kontrolera, admin stranice.
Kraj faze: Asim može napisati i objaviti objavu, i vidjeti je preko API-ja. Korisnik još ništa ne vidi.

**Faza 2 — korisnički dio.** Banner, modal, stranica "Šta je novo", stavka i badge u meniju.

Faze su nezavisno isporučive: faza 1 bez faze 2 ne mijenja ništa nijednom korisniku.

## Acceptance criteria

Provjereno u pregledniku preko privremenog harnessa (obrisan) — ⚠️ označava ono što traži bazu i
prijavljenog korisnika, pa se lokalno **nije** moglo izvršiti (nema Postgresa na ovoj mašini):

- [x] SystemAdmin može napraviti nacrt, urediti ga i obrisati — *kod i rute postoje; ⚠️ nije izvršeno*
- [x] Nacrt se ne vidi nigdje u korisničkom dijelu — svi upiti potrošnje filtriraju `IsPublished`
- [x] Objava praznog opisa se odbija s porukom — `SetPublishedAsync` baca `ValidationException`; ⚠️ nije izvršeno
- [x] Objavljivanje postavlja `PublishedAt` **samo prvi put** — guard `PublishedAt is null`; ⚠️ nije izvršeno
- [x] Banner se pojavi svakom korisniku na **svakoj** stranici nakon objave — u `Layout` iznad `ErrorBoundary`
- [x] Banner prikazuje badge tipa (Novo / Poboljšanje / Ispravka) i naslov
- [x] Klik na banner otvara modal s cijelim opisom u markdownu — liste i podebljano potvrđeni u DOM-u
- [x] Zatvaranje modala trajno sklanja banner za tu objavu
- [x] Klik na "x" na banneru trajno ga sklanja bez otvaranja modala
- [x] Sklonjen banner **ostaje sklonjen u drugom pregledniku** istog korisnika (D3) — stanje je u
      `AnnouncementReads`, ne u `localStorage`; ⚠️ nije izvršeno
- [x] Nakon nove objave banner se ponovo pojavi, i pokazuje **novu** objavu — `GetLatestPublishedAsync`
      sortira po `PublishedAt DESC, Id DESC`; ⚠️ nije izvršeno
- [x] Objava **ne** stvara nijednu stavku u zvonu (D4) — `AnnouncementService` uopšte ne ovisi o
      `INotificationService`
- [x] Stranica "Šta je novo" prikazuje sve objavljene, najnovije prve, s oznakom pročitanog
- [x] Filter po tipu na stranici radi
- [x] Stavka u meniju nosi broj neviđenih objava i nestaje kad ih nema — `badge` se ne renderuje na 0
- [x] Uređivanje objavljene objave ne vraća banner onima koji su ga sklonili — `UpdateAsync` ne dira
      `AnnouncementReads`; ⚠️ nije izvršeno
- [x] Skidanje s objave sklanja i banner i stavku sa stranice — `IsPublished` filter; ⚠️ nije izvršeno
- [x] SystemAdmin vidi banner kao i svi ostali (D5) — nema role gatea ni u `Layout` ni u servisu
- [x] Banner i modal rade u tamnoj temi i na širini telefona — na 375 px nema horizontalnog
      prelijevanja (0 elemenata preko `clientWidth`), modal stane u viewport; tamna tema mijenja
      boje bannera i naslova
- [x] Offline: banner se ne renderuje i ništa ne puca — upit padne, `announcement` ostaje `null`

Dodatno provjereno mimo liste:
- [x] Svi EF Core upiti se prevode (`ToQueryString` protiv `Host=127.0.0.1;Port=1`, bez otvaranja
      konekcije) — brojač neviđenih postaje `NOT EXISTS` korelirani podupit
- [x] `dotnet test Melarium.Application.Tests` — 610/610 prolazi nakon dodavanja člana u `IUnitOfWork`
- [x] `npx tsc --noEmit` i `npm run build` prolaze

## Changed during implementation (2026-08-28)

**Banner je ostao bez sadržaja — samo tip, naslov i „Pročitaj više".** Nacrt je u banner stavljao
skraćenu prvu liniju opisa. Na živom ekranu se pokazalo da to banner čini višim, a i dalje ne kaže
dovoljno da bi neko preskočio otvaranje modala — pa sadržaj sada živi na jednom mjestu, u modalu.
Posljedica: `stripMarkdown.ts` nije premješten u `shared/utils/`, ostaje u `features/learning/`.

**Hover boja je prešla s dugmeta na cijelu karticu.** Bila je `hover:bg-*` na dugmetu s tekstom, pa
je stajala na granici tog dugmeta i ostavljala **neobojenu traku pored i ispod „x"** — vidljiva
tamnija kolona s desne strane. Popravka nije mogla biti `hover:bg-*` na kontejneru, jer je pozadina
kartice gradijent (`background-image`) koji prekriva `background-color`. Sada je to jedan
`absolute inset-0` sloj s `pointer-events-none`, koji se pali preko `group-hover:opacity-100` —
pokriva cijelu karticu i prelazi glatko.

**Otvaranje objave s arhive dobilo je putanju greške koje nije bilo u nacrtu.** Lista ne nosi tijelo
objave, pa klik na karticu dohvata `GET /announcements/{id}`. U prvoj verziji je to bio `await` bez
`catch`: kad poziv padne (offline, greška servera) **ništa se ne bi desilo** — bez poruke, bez
spinnera, stranica izgleda pokvareno. Isti tihi kvar zbog kojeg postoji SPEC-20 D4. Sada kartica ima
spinner dok traje dohvat i toast "Greška pri učitavanju objave." kad padne.

## Open questions

- Slika u objavi je odbijena za v1. Ako se pokaže da tekst ne prenosi funkcionalnost, to je prvi
  sljedeći kandidat — prije CTA dugmeta.
