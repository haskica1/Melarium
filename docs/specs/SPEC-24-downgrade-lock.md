# SPEC-24 — Zaključavanje pri prelasku na niži paket (Downgrade lock)

| | |
|---|---|
| **Status** | ✅ Implementirano (2026-09-04) |
| **Obim posla** | M (~1–2 dana) |
| **Zavisi od** | SPEC-09 (paketi), SPEC-07 (offline pregledi), SPEC-19 (spojene košnice) |
| **Novi secreti / paketi** | ništa |
| **Migracija** | **nema** — sve se računa, ništa se ne pohranjuje |

## Cilj

SPEC-09 je svjesno odlučio da **prelazak na niži paket ništa ne zaključava**: limiti se provjeravaju
samo pri kreiranju. U praksi je to značilo da neko otvori nalog, dobije 30-dnevni Pro probni period,
napravi 3 pčelinjaka i 50 košnica — i nakon isteka probnog perioda **zadrži pun pristup svemu**,
zauvijek, na Besplatnom paketu. Besplatni paket time nije bio besplatan paket nego trajni Pro za
svakog ko se registrovao.

Ovaj spec obrće tu odluku. Nakon pada na niži paket, sve iznad limita novog paketa postaje
**zaključano**: i dalje se vidi u listi, ali se ne može otvoriti niti pročitati ijedan podatak o
njemu. Ništa se ne briše i sve se vraća čim se paket nadogradi.

> **Ovo mijenja ADR-028 i SPEC-09 §Domenska pravila.** Vidi ADR-042.

## Šta se zaključava

Redoslijed je **determinističan i računat, sistem bira — korisnik ne bira**:

1. Pčelinjaci se rangiraju **najstariji prvi** (`CreatedAt`, pa `Id` kao tiebreaker); oni preko
   `MaxApiaries` su zaključani.
2. Svaka košnica u zaključanom pčelinjaku je zaključana s njim, i **ne troši kvotu košnica**.
3. Košnice u preostalim pčelinjacima rangiraju se najstarije prve **kroz cijelu organizaciju**
   (kvota je po organizaciji, kao i gejt pri kreiranju); one preko `MaxBeehives` su zaključane.
4. Spojene košnice (SPEC-19, `MergedIntoBeehiveId != null`) se preskaču — one ionako ne broje u
   limit, pa ne smiju ni trošiti mjesto.

Primjer koji je pokrenuo spec: Pro probni period, 3 pčelinjaka, 50 košnica → istek → Besplatni.
Ostaje **1 pčelinjak i 7 najstarijih košnica u njemu**; ostala 2 pčelinjaka i 43 košnice su
zaključane.

**Tiebreaker po `Id` nije kozmetika.** Bez njega bi dvije košnice kreirane u istoj sekundi mogle
mijenjati mjesta između dva zahtjeva, pa bi se košnica naizmjenično otključavala i zaključavala.

## Domenska pravila

- **Računa se, ne pohranjuje** — isti presedan kao `PlanHelper.Effective` (SPEC-09) i
  `ADR-034`. Nema migracije, nema background joba, nema polja koje može zastarjeti. Posljedice koje
  su tu besplatno: nadogradnja paketa otključava sve u istoj sekundi, a brisanje aktivne košnice
  automatski promoviše sljedeću zaključanu.
- Efektivni paket je onaj iz `PlanHelper.Effective`, pa **istekao paket zaključava kao Besplatni** —
  probni period i godišnja pretplata prolaze kroz isti mehanizam, bez ijedne posebne grane.
- **Lock stupa na snagu odmah** — nema perioda počeka. Obavijest ide 2 dana ranije (vidi Alarm).
- Zaključan red **ostaje u listi**, označen `isLocked` i **očišćen** od svih polja osim imena
  (`PlanLockRedaction` — jedno mjesto koje definiše šta zaključan red smije odati). `uniqueId` se
  briše jer je ključ, ne labela: ostavljen bi dozvolio da skeniranje QR-a razriješi zaključanu košnicu.
- Svaka putanja koja bi otvorila zaključan red baca `PlanLimitException` → **402** s
  `code: "plan-limit"`, isti oblik koji `UpsellModal` već hvata (SPEC-09) — ni jedna linija
  frontend interceptora nije mijenjana.
- **SystemAdmin zaobilazi sve**, kao i kod ostalih gejtova paketa.

### Dva izuzetka, oba namjerna

**Brisanje zaključane košnice/pčelinjaka je dozvoljeno.** Ovo je jedini izlaz iz ćorsokaka:
organizacija sa 50 košnica na Besplatnom paketu koja ih ne može ni otvoriti ni obrisati nikad ne bi
mogla sići na 7 aktivnih osim plaćanjem. Obriše li 43 košnice koje fizički više nema, ostatak joj se
otključa sam. Implementirano kao `allowLocked: true` na tačno dva mjesta
(`BeehiveService.DeleteAsync`, `ApiaryService.DeleteAsync` preko sinhronog guarda).

**Unos pregleda prolazi i na zaključanoj košnici.** Offline pregledi (SPEC-07) se sinhronizuju kroz
`inspectionService.create` — isti endpoint kao online unos, bez ikakve oznake da dolaze iz outboxa.
Pčelar koji je odradio pčelinjak offline pa se paket u međuvremenu smanjio inače bi izgubio cijelu
turu rada. Podatak **uđe u bazu, ali se ne može pročitati** dok se paket ne nadogradi. Online ovim
putem niko ne može proći: zaključanu košnicu ne možeš ni otvoriti da započneš pregled.

### Članovi: samo za čitanje, ne izbačeni

Kad organizacija padne na paket s manje mjesta nego što ima naloga, **dodatni članovi preko limita
gube pravo unosa, ali zadržavaju prijavu i sve što su mogli čitati**.

- Vlasnik (najstariji `OrganizationAdmin` organizacije; fallback najstariji nalog) **nikad** nije
  read-only.
- Ostali se rangiraju najstariji prvi; oni preko `MaxMembers` su read-only. Ista definicija
  "dodatnog člana" koju broji gejt pri kreiranju (ukupno naloga − 1).
- Enforcement je na **rubu**, u `ReadOnlyMemberMiddleware`: sve što nije GET/HEAD/OPTIONS je pisanje.
  Pravilo koje nijedan budući endpoint ne može zaboraviti primijeniti.
- Ostaju dozvoljene četiri prefiks-putanje, svaka sa razlogom: `/api/auth` (bez nje se korisnik ne
  može ni odjaviti ni promijeniti lozinku — to je sigurnosni, ne naplatni problem), `/api/profile`
  (vlastiti profil; i brisanje vlastitog naloga, što je članova odluka), `/api/notifications` (bez
  nje brojčanik na zvonu nikad ne padne na nulu) i `/api/feedback` (bez nje nema načina da javi da
  ga aplikacija odbija).
- Odbačena alternativa: **izbaciti dodatne članove iz aplikacije**. Član je osoba koja nije ništa
  skrivila — paket je vlasnikova stvar.

## Backend

### `IPlanLock` — nova komponenta

```
IPlanLock
  GetForOrganizationAsync(orgId)         // zaključani id-jevi, keširano po zahtjevu
  GetForCurrentUserAsync()               // isto, za organizaciju pozivaoca
  PreviewForPlanAsync(orgId, plan)       // šta bi paket X zaključao — za alarm 2 dana ranije
  IsApiaryLockedAsync / IsBeehiveLockedAsync
  EnsureApiaryUnlockedAsync / EnsureBeehiveUnlockedAsync   // → PlanLimitException (402)
  IsCurrentUserReadOnlyAsync()
```

Čista logika rangiranja je izdvojena u `Melarium.Domain/Common/PlanLockPolicy.cs` — bez baze, bez
DI-ja, testabilna direktno (`PlanLockPolicyTests`), po uzoru na `PlanHelper`.

`PlanLock` je **scoped i kešira po zahtjevu**. Access guard ga pita na svakoj provjeri resursa, a
lista jednom po redu — bez keša bi to bilo po par upita na svaku košnicu.

### Enforcement ide kroz `IAccessGuard`, ne kroz servise

Lock je ožičen u `AccessGuard.EnsureCanAccessBeehiveAsync` / `EnsureCanManageApiaryAsync`. Kroz te
dvije metode već prolazi ~60 poziva iz svih servisa — pregledi, vrcanja, matice, tretmani, todo,
prihrana, foto analiza, AI asistent. Alternativa (provjera u svakom servisu) je šezdeset mjesta koja
treba držati u koraku, a **jedan propušten poziv je rupa u naplati**.

Redoslijed provjera je bitan: **prvo rola (403), pa paket (402)**. Obrnuto bi strancu iz druge
organizacije reklo nešto o tuđem paketu.

### Agregati koji ne prolaze kroz guard

Ove putanje čitaju direktno iz repozitorija i zato filtriraju zaključano ručno:

| Servis | Zašto |
|---|---|
| `StatsService` | Inače dashboard izvještava o 50 košnica dok se 7 može otvoriti |
| `AlertRuleService` | Alarm "pregled kasni" za košnicu koja vodi na paywall je gori od tišine |
| `CalendarAccessResolver` | Jedno mjesto — feed, dnevna agenda i ICS izvoz idu kroz njega |
| `WeeklySummaryService` | Standard organizacija spuštena s Pro-a i dalje dobija sažetak |
| `TreatmentService` / `HarvestService` / `DietService` | Org-liste bi prikazale redove zaključanih pčelinjaka |
| `BeehiveService.GetQrCodesByApiaryAsync` | Naljepnice se štampaju za košnice na kojima se radi |
| `BeehiveService.GetScanInfoAsync` | Skeniranje zaključane naljepnice vodi na upsell, ne u ćorsokak |

`GetAccessibleBeehivesAsync(includeLocked)` po defaultu **izbacuje** zaključane — asistent (SPEC-17),
skeniranje i matching po broju ih tako ne mogu dohvatiti po konstrukciji. Jedini pozivalac koji traži
`includeLocked: true` je lista košnica, jer je lista upravo mjesto gdje se zaključano treba vidjeti.

### Alarm — `PlanLockPending` (NotificationType **28**)

Dva dana prije isteka (`Alerts:PlanLockNoticeDays`, default 2), organizacijama koje će **stvarno**
nešto izgubiti: `PreviewForPlanAsync(org, Free)` i tišina ako je rezultat prazan. Šalje se
OrganizationAdminima, dedup `noticeDays + 1` dana da dvodnevni prozor proizvede jednu poruku.

Ide i mail i in-app notifikacija — `NotificationService.NotifyAsync` već radi oboje kroz email queue,
pa nema nove infrastrukture.

Namjerno odvojeno od `PlanExpiring` (18): taj alarm ide svima kojima paket ističe i kaže *da* ističe;
ovaj ide samo onima koji gube pristup i kaže **šta konkretno prestaje da se otvara**.

Pokriva samo istek — a tako nastaje skoro svaki downgrade (probni period ili godina). Ručni
downgrade koji SystemAdmin uradi u admin UI-ju stupa na snagu odmah i najavljuje ga osoba koja ga je
i napravila.

### API

Bez novih endpointa. Proširena postojeća polja:

| Endpoint | Novo |
|---|---|
| `GET /api/apiaries`, `/api/apiaries/{id}` | `isLocked` na `ApiaryDto`; zaključani redovi očišćeni |
| `GET /api/beehives`, `/api/apiaries/{id}` | `isLocked` na `BeehiveDto`; zaključani redovi očišćeni |
| `GET /api/organizations/my-plan` | `isReadOnlyMember`; `usage.lockedApiaries`, `usage.lockedBeehives`, `usage.readOnlyMembers` |

Nove 402 situacije: otvaranje zaključanog pčelinjaka/košnice, izmjena istih, skeniranje zaključane
naljepnice, i bilo koji ne-GET zahtjev read-only člana.

## Frontend

- `shared/components/PlanLocked.tsx` — `LockedBadge`, `PlanLockNotice`, `showLockedUpsell`. Klik na
  zaključan red **ne šalje zahtjev koji je siguran da će pasti**, nego direktno emituje `plan-limit`
  event koji `UpsellModal` već sluša.
- Kartica zaključanog pčelinjaka i red zaključane košnice: `opacity-60 grayscale`,
  `cursor-not-allowed`, bez hover efekta, bez brojeva, sa "Zaključano" pilulom. Dugme **Uredi** se
  sakriva, **Obriši** ostaje.
- Vitali na `/apiaries` računaju se **samo iz dostupnih** pčelinjaka — prosjek preko redova koje je
  server namjerno nulirao bi potcijenio svaki broj na ekranu.
- `/plans` dobija `LockSummary`: koliko je zaključano, i rečenicu koja rješava najčešći strah nakon
  isteka — *"Ništa nije obrisano."*
- `ReadOnlyMemberBanner` u `Layout`, uz `AnnouncementBanner`. Mora biti stalan a ne toast na prvom
  odbijanju: taj nalog i dalje sve vidi, pa aplikacija izgleda normalno sve dok snimanje ne padne.

## Rubni slučajevi

- Pčelinjak zaključan, ali unutar njega košnice — poruka razlikuje ta dva slučaja. Reći nekome da je
  košnica preko limita od 7 kad je zapravo izgubio cijeli pčelinjak šalje ga da briše košnice koje
  nisu bile problem.
- Beekeeper dodijeljen samo zaključanim košnicama vidi prazan raspored — očekivano; ako je uz to i
  preko limita članova, ionako je read-only.
- Organizacija koja stane u svoj paket ne vidi **ništa** od ovoga: nema pilula, nema bannera, nema
  alarma, `PlanLockResult.Empty` i brza putanja bez ijednog upita.
- Max i Partner ne ograničavaju ni pčelinjake ni košnice → `Locked()` izlazi prije ijednog upita.
- Spojena košnica (SPEC-19) nikad nije zaključana — ne broji u limit i ne troši mjesto.
- Brisanje aktivne košnice promoviše najstariju zaključanu bez ijedne dodatne akcije.

## Van opsega

Izbor koje košnice ostaju aktivne (sistem bira, tačka), period počeka nakon downgrade-a, izuzetak za
PDF registar tretmana (zaključan kao i sve ostalo), jednokratni izvoz podataka, i najava kroz
"Šta je novo" prije prvog deploya.

## Kriteriji prihvatanja

- [x] Pro probni period sa 3 pčelinjaka i 50 košnica istekne → ostaje 1 pčelinjak i 7 najstarijih
      košnica u njemu; ostalo zaključano (`PlanLockPolicyTests.TrialExpiry_ProToFree_…`).
- [x] Rangiranje je stabilno: isti `CreatedAt` razrješava se po `Id`, isti odgovor na svaki zahtjev
      (`PlanLockPolicyTests.SameCreatedAt_BreaksTieOnId_…`).
- [x] Košnice u zaključanom pčelinjaku ne troše kvotu košnica
      (`PlanLockPolicyTests.HivesInALockedApiary_DoNotConsumeTheHiveQuota`).
- [x] Otvaranje zaključane košnice → **402** `plan-limit`, ne 403; stranac iz druge organizacije i
      dalje dobija 403 (`AccessGuardTests`).
- [x] Brisanje zaključane košnice/pčelinjaka prolazi; unos pregleda na zaključanoj košnici prolazi
      (`AccessGuardTests.…AllowLocked_Passes`).
- [x] Asistent, skeniranje i matching po broju ne mogu dohvatiti zaključanu košnicu; lista može, ali
      očišćenu (`AccessGuardTests.GetAccessibleBeehives_ExcludesLockedByDefault_…`).
- [x] Istekao paket zaključava kao Besplatni; važeći Pro ne zaključava ništa; SystemAdmin ne vidi
      ništa zaključano (`PlanLockTests`).
- [x] Read-only članovi: vlasnik nikad, Besplatni zamrzava sve dodatne, Standard zadržava dva
      najstarija, Max nijednog (`PlanLockTests`).
- [x] `PreviewForPlanAsync` gleda unaprijed neovisno o trenutnom paketu, pa alarm može najaviti
      (`PlanLockTests.PreviewForPlan_IgnoresTheCurrentPlan_…`).
- [x] Svih 666 testova prolazi; `tsc --noEmit` čist; produkcijski frontend build prolazi.
- [ ] **Nije provjereno uživo** — nema lokalnog Postgresa. Vidi napomenu ispod.

## Napomena za deploy

Odluka je bila **lock odmah na deploy**, bez jednokratnog datuma starta. Posljedica koju treba
provjeriti prije nego kod ode gore:

> Organizacije kojima je paket **već istekao u prošlosti** dobijaju lock u sekundi deploya, a alarm
> od 2 dana se za njih **nikad ne može okinuti** jer im je istek iza. One nemaju nikakvu najavu.

Upit koji pokazuje koga to pogađa i koliko:

```sql
SELECT o."Id", o."Name", o."Plan", o."PlanValidUntil",
       (SELECT COUNT(*) FROM "Apiaries" a WHERE a."OrganizationId" = o."Id") AS apiaries,
       (SELECT COUNT(*) FROM "Beehives" b
          JOIN "Apiaries" a ON a."Id" = b."ApiaryId"
         WHERE a."OrganizationId" = o."Id" AND b."MergedIntoBeehiveId" IS NULL) AS beehives
FROM "Organizations" o
WHERE o."PlanValidUntil" IS NOT NULL AND o."PlanValidUntil" < CURRENT_DATE
ORDER BY beehives DESC;
```

Sve iznad 1 pčelinjaka / 7 košnica u tom rezultatu gubi pristup na dan deploya.
