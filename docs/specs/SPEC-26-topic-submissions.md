# SPEC-26 — Objavi temu ("User-proposed learning topics")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-09-20) — see `features/learning.md` |
| **Effort** | M (~1 dan) |
| **Depends on** | SPEC-06 (Edukacija — ista tabela, isti publish put). Obrazac obavještavanja preuzet iz SPEC-13 |
| **New secrets / packages** | nema |
| **Breaking** | Ne — šest nullable/defaultnih kolona na `LearningTopics`, sve postojeće teme ostaju `ReviewStatus = None` |

## Goal

Edukaciju do sada piše isključivo SystemAdmin. Iskustvo je, međutim, kod pčelara: neko ko trideset
godina radi s bagremovom pašom ima tekst koji niko na platformi ne može napisati umjesto njega — a
nema nijedan način da ga ponudi.

Ovaj spec daje **svakom prijavljenom korisniku** da napiše temu i pošalje je na odobrenje.
SystemAdmin je pregleda, odobri ili odbije uz razlog. Odobrena tema se pojavljuje u Edukaciji kao i
svaka druga, s potpisom autora.

## Decisions (settled with Asim before implementation, 2026-09-20)

### D1 — Temu šalje **svaki prijavljeni korisnik**

Razmatrano je da se pravo slanja ograniči na OrganizationAdmine (manje moderacije) ili na
OrganizationAdmine + ApiaryAdmine. Odbijeno: znanje nije vezano za ulogu u aplikaciji — Beekeeper
koji nema nijednu košnicu u svom vlasništvu i dalje može biti najiskusniji čovjek na platformi.
Isti izbor kao kod SPEC-13 (povratne informacije šalje svako).

Cijena je moderacija. Nosi je ograničenje broja zahtjeva (`learning-submit`, 5/min po IP) i to što
nijedan prijedlog nije vidljiv dok ga neko ne pogleda.

### D2 — Ista tabela: `LearningTopic` + status recenzije + autor

Alternativa je bila zasebna tabela prijedloga koja se kod odobrenja **kopira** u novi
`LearningTopic`. Odbijeno:

- Odobrenje bi bilo *insert*, a ne promjena stanja — dva reda za isti tekst, i kasnija izmjena
  objavljenog članka ne bi imala veze s prijedlogom iz kojeg je nastao.
- Sva mehanika objave (validacija sadržaja, `PublishedAt` kao čuvar "obavijesti samo jednom",
  broadcast svim korisnicima) već postoji na `LearningTopic` i radila bi se drugi put.
- Admin doradi tekst prije objave postojećom formom, bez ijednog novog ekrana.

**Ključno svojstvo koje ovo čini sigurnim:** `IsPublished` ostaje **jedini** filter vidljivosti.
Prijedlog je `IsPublished = false` od trenutka nastanka, pa `GetPublishedAsync` /
`GetPublishedByIdAsync` — a time i cijela Edukacija — ne mogu ga vidjeti ni greškom. `ReviewStatus`
ne učestvuje ni u jednom upitu za čitanje; on samo opisuje kroz šta je red prošao.

### D3 — Odbijanje uz razlog, autor dorađuje i šalje ponovo

Alternative su bile "odbijeno konačno" (autor mora pisati novu temu) i "admin samo obriše
prijedlog". Odbijeno oboje: prvi put odbijen tekst je obično tekst kojem fali jedna stvar, a ne
pogrešan tekst; tražiti da se prepiše ispočetka znači da se neće ni poslati drugi put.

Razlog je **obavezan** (min. 10 znakova) i ide autoru doslovno — obavijest bez razloga je odbijanje
bez uputstva šta popraviti.

Posljedica je da je **`Rejected` prolazno stanje, ne završno**: spremanje izmjene na odbijenoj temi
je ponovno slanje — status se vraća na `Pending`, razlog se briše, `ReviewedAt`/`ReviewedById` se
poništavaju (stara presuda više ne važi za ovaj tekst), i admini dobiju novu obavijest. Izmjena teme
koja **već** čeka odobrenje je samo izmjena: status se ne mijenja i admini se ne obavještavaju
ponovo.

### D4 — Objavljena tema nosi **ime i prezime autora**

Razmatrano je da se autor čuva samo u bazi (sve izgleda kao sadržaj platforme) i da se uz ime piše i
organizacija. Izabran je potpis imenom: to je jedina nagrada koju ovaj sistem nudi za napisan članak,
a organizacija nije napisala tekst — čovjek jeste.

`AuthorName` je `null` za sve što je napisao SystemAdmin, pa platformski sadržaj nema potpis i ne
mora ga imati.

### D5 — Odobrena tema se autoru zaključava

Nakon odobrenja tema je sadržaj platforme: autor je ne može ni urediti (`422`) ni povući (`422`).
Suprotno bi značilo da objavljen i pročitan članak može nestati ili se prepisati bez ikakve
provjere. Povlačenje neodobrenog prijedloga (`Pending` ili `Rejected`) je dozvoljeno i briše red.

### D6 — Bez AI nacrta na korisničkoj strani

`generate-draft` ostaje SystemAdmin-only. Razlog nije cijena nego svrha: ovaj spec postoji da bi u
Edukaciju ušlo **iskustvo s terena**. Dugme koje generiše članak iz naslova daje tačno suprotno —
tekst koji je model mogao napisati i bez pčelara. Umjesto toga donji prag od **200 znakova** drži
jednoredne prijedloge van reda za pregled.

## Model

`LearningTopic` dobija šest kolona (migracija `AddLearningTopicSubmissions`):

| Polje | Napomena |
|---|---|
| `AuthorId?` → `Users` | `ON DELETE SET NULL` — obrisan račun ne odnosi objavljen članak, gubi se samo potpis |
| `ReviewStatus` | `TopicReviewStatus`: `None` (admin napisao) · `Pending` · `Approved` · `Rejected`. Default `0`, pa sve postojeće teme ostaju `None` bez backfilla |
| `SubmittedAt?` | Vrijeme zadnjeg slanja — mijenja se i kod ponovnog slanja |
| `ReviewedAt?`, `ReviewedById?` → `Users` | `ON DELETE SET NULL` |
| `RejectionReason?` (500) | Briše se kod ponovnog slanja |

Indeksi: `ReviewStatus` (red za pregled i badge), `AuthorId` ("moje teme").

## API

**Korisnik (`/api/learning-topics/submissions`, svaka prijavljena uloga):**

| Metoda | Put | Napomena |
|---|---|---|
| GET | `/submissions` | Samo vlastiti prijedlozi, najnoviji prvi |
| GET | `/submissions/{id}` | Tuđi id → **404**, ne 403 (403 bi potvrdio da red postoji) |
| POST | `/submissions` | Rate limit `learning-submit` (5/min po IP) → `201` |
| PUT | `/submissions/{id}` | Odbijena → ponovo `Pending`; odobrena → `422` |
| DELETE | `/submissions/{id}` | Povlačenje; odobrena → `422` |

**SystemAdmin (`/api/admin/learning-topics`):**

| Metoda | Put | Napomena |
|---|---|---|
| GET | `/submissions/summary` | `{ pendingCount }` za badge |
| PUT | `/{id}/approve` | Odobrava **i objavljuje** u jednom koraku; ne-`Pending` → `422` |
| PUT | `/{id}/reject` | `{ reason }` (10–500) |

## Obavještavanja

| Trenutak | Kome | Kanal | Tip |
|---|---|---|---|
| Poslano / ponovo poslano | svi SystemAdmini | samo zvono (`NotifyManyInAppAsync`) | `LearningTopicSubmitted = 29` |
| Odobreno / odbijeno | autor | zvono **i** e-pošta (`NotifyAsync`) | `LearningTopicReviewed = 30` |
| Prva objava | svi korisnici **osim autora** | samo zvono | `LearningTopicPublished = 17` (postojeći) |

Za razliku od SPEC-13, **nema e-pošte operateru** i nema nove konfiguracije: prijedlog teme nije
incident koji mora izaći iz aplikacije. Nijedno obavještavanje ne smije oboriti akciju — red je već
sačuvan, pa je greška u slanju logovana, ne vraćena korisniku.

**Autor je izuzet iz broadcasta prve objave** jer je sekundu ranije dobio ličnu poruku o istoj temi;
"Objavljena je nova tema: …" o vlastitom članku je druga obavijest o istom događaju.

## Frontend

- `LearningPage` hero: **Predloži temu** + **Moje teme**. Prazno stanje više ne kaže samo "teme
  objavljuje administrator" — nudi i dugme.
- `TopicSubmissionFormPage` (`/learning/predlozi`, `/learning/moje-teme/:id/uredi`) — ista polja kao
  admin forma bez AI panela; brojač do 200 znakova; okvir iznad forme objašnjava šta se dešava
  poslije slanja, a na odbijenoj temi stoji i razlog.
- `MySubmissionsPage` (`/learning/moje-teme`) — status po prijedlogu, razlog odbijanja s linkom na
  doradu, uređivanje i povlačenje dok je `canEdit`.
- `LearningTopicPage` — "Autor: Ime Prezime" kad `authorName` postoji.
- `LearningTopicsAdminPage` — sekcija **Čeka odobrenje** iznad liste (autor, datum, *Pročitaj /
  Odbij / Odobri*), modal za razlog, i status čip na odbijenima. Odbijena tema piše "Odbijena", ne
  "Skica" — "skica" znači da admin nije dovršio pisanje, a to ovdje nije istina.
- Nav: SystemAdmin dobija stavku **Uredi edukaciju** (`/admin/learning-topics`) s badgeom broja
  prijedloga na čekanju, po uzoru na "Povratne informacije".

## Acceptance criteria

- [x] Svaki prijavljeni korisnik može poslati temu; tema nije vidljiva u Edukaciji dok nije odobrena
- [x] Prijedlog s praznim ili prekratkim tekstom (< 200 znakova) se odbija s `400`
- [x] Tuđi prijedlog vraća `404`, ne `403`
- [x] SystemAdmin vidi red za pregled s imenom autora i badge brojem u navigaciji
- [x] Odobrenje objavljuje temu, obavještava autora i emituje postojeću obavijest svima osim autoru
- [x] Odbijanje traži razlog (10–500), čuva ga i šalje autoru
- [x] Odbijena tema se doradom vraća na čekanje, razlog se briše, admini se obavještavaju ponovo
- [x] Odobrena tema se autoru ne može ni urediti ni povući (`422`)
- [x] Postojeće teme ostaju netaknute (`ReviewStatus = None`, bez potpisa)
- [x] `dotnet test` prolazi (697, od toga 20 na `LearningTopicServiceTests`), `tsc --noEmit` i `npm run build` čisti

## Deploy

Migracija `AddLearningTopicSubmissions` nije primijenjena na produkciji — aditivna je (šest kolona,
tri indeksa, dva FK-a sa `SET NULL`), bez backfilla i bez zastoja.
