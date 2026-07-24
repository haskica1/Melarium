# SPEC-09 — Paketi i naplata (Plans & Billing)

| | |
|---|---|
| **Status** | ✅ Implementirano (2026-07-06) — 4 javna paketa + skriveni Partner, 30-dnevni probni period, kvota AI savjetnika |
| **Obim posla** | L (~2–3 dana) |
| **Zavisi od** | ničega (kad se implementira, gejtuje SPEC-01 AI funkcije i SPEC-10 pašnjake) |
| **Novi secreti / paketi** | ništa u v1 (provajder plaćanja u fazi 2 dodaje webhook secret) |

## Cilj

Monetizacija platforme pretplatničkim paketima po organizaciji: **Besplatni** paket zadržava
početnike, tri plaćena paketa (**Standard / Pro / Max**) rastu po skali i AI funkcijama, a
skriveni **Partner** paket — koji se nikad ne prikazuje u javnom UI-ju — omogućava SystemAdminu
da prijateljima, rodbini, udruženjima pčelara i demo nalozima dodijeli sve otključano, besplatno.
**Naplata u v1 je ručna i godišnja** (uplatnica / faktura; SystemAdmin aktivira paket u admin
UI-ju) — Stripe ne podržava merchant naloge iz BiH, pa automatska plaćanja ciljaju
merchant-of-record provajdera (Paddle) kasnije, a ručno *mjesečno* knjiženje uplata od 20 KM je
operativno neodrživo — otud u v1 isključivo godišnja naplata. v1 isporučuje cijeli model paketa,
enforcement, probni period pri registraciji i UX nadogradnje; odgađa se samo klik-tok plaćanja.

## Paketi (v1)

Cijene se komuniciraju mjesečno; v1 naplaćuje **godišnje** uz popust "2 mjeseca gratis"
(Standard 200 KM/god, Pro 350 KM/god, Max 500 KM/god). Mjesečna naplata stiže s Paddle-om
(faza 2).

| | **Besplatni** | **Standard** 20 KM/mj | **Pro** 35 KM/mj | **Max** 50 KM/mj | **Partner** (skriven) |
|---|---|---|---|---|---|
| Pčelinjaci | 1 | neograničeno | neograničeno | neograničeno | neograničeno |
| Košnice | 7 | 30 | 100 | neograničeno | neograničeno |
| Dodatni članovi (uz vlasnika) | 0 | 2 | 5 | neograničeno | neograničeno |
| Evidencije: pregledi (i offline), prihrana, vrcanja, matice, tretmani + PDF registar | ✓ | ✓ | ✓ | ✓ | ✓ |
| Todo, kalendar, vremenska prognoza, statistika | ✓ | ✓ | ✓ | ✓ | ✓ |
| Rule-based alarmi, edukacija | ✓ | ✓ | ✓ | ✓ | ✓ |
| Pašnjaci i selidbe | ✗ | ✓ | ✓ | ✓ | ✓ |
| Glasovni unos pregleda | ✗ | ✓ | ✓ | ✓ | ✓ |
| Sedmični AI sažetak | ✗ | ✓ | ✓ | ✓ | ✓ |
| AI Savjetnik (chat + glas) | ✗ | 10 poruka/mj | ✓ | ✓ | ✓ |
| Foto + AI analiza okvira (SPEC-05) | ✗ | ✗ | ✓ | ✓ | ✓ |
| Prioritetna podrška (telefon/Viber) | ✗ | ✗ | ✗ | ✓ | ✓ |

Logika paketa (obrazloženje za buduće izmjene): sve što je besplatno za serviranje **i** gradi
dnevnu naviku (prognoza je Open-Meteo bez API ključa, kalendar, alarmi, edukacija koristi
browserski TTS) ide u besplatni paket — to vraća korisnika u aplikaciju i pokreće preporuke od
usta do usta. **Unos podataka i zakonske evidencije se nikad ne naplaćuju** (PDF registar
tretmana je zakonska obaveza), a prelazak na niži paket ništa ne zaključava. Naplaćuje se stvarni
trošak (Groq AI), skala (**košnice su jedina mjera skale** — pčelinjaci su besplatni redovi u
bazi, ograničeni samo na Besplatnom paketu gdje 1 pčelinjak označava hobistu) i komercijalni
signali (dodatni članovi, selidbe na pašu — oboje ukazuje na ozbiljnu proizvodnju, a ništa ne
košta za serviranje). Kontrola AI troška: glasovni unos raste s brojem pregleda (prirodno
ograničen brojem košnica), sedmični sažetak je jedan Groq poziv po organizaciji sedmično, a
savjetnik — jedini neograničeni tip poziva — na Standardu ima mjesečnu kvotu.

Limiti žive u configu, nikad hardkodirani u servisima. Odsutan ključ = neograničeno/omogućeno;
Max i Partner nemaju unose:

```json
"Plans": {
  "Free":     { "MaxApiaries": 1, "MaxBeehives": 7, "MaxMembers": 0 },
  "Standard": { "MaxBeehives": 30, "MaxMembers": 2, "AdvisorMessagesPerMonth": 10 },
  "Pro":      { "MaxBeehives": 100, "MaxMembers": 5 },
  "Trial":    { "Days": 30 }
}
```

(Config ključevi prate imena enum vrijednosti — engleski kao i ostatak configa; "Besplatni" je samo
UI labela preko `BsLabels`/frontend mapa.)

## Domenska pravila

- Paket je **po organizaciji**: `Organization` += `Plan PlanType { Free=1, Standard=2, Pro=3,
  Max=4, Partner=5 }` (default Free), `PlanValidUntil DateTime?` (null = bez isteka),
  `PlanNotes string(300)?` (ručno knjigovodstvo: broj uplatnice, ko je platio, "Probni
  period"…). Migracija `AddOrganizationPlan`.
- **Efektivni paket se računa, ne pohranjuje** (presedan Diets/Treatments —
  `PlanHelper.Effective(plan, validUntil, today)`): bilo koji plaćeni/Partner paket +
  `PlanValidUntil < today` → ponaša se kao **Besplatni**. Porede se datumi, ne trenuci (paket
  važi do kraja dana isteka). Nikakav background job ništa ne prebacuje.
- **Probni period**: registracija kreira organizaciju sa `Plan = Pro`, `PlanValidUntil = danas +
  Plans:Trial:Days` (30), `PlanNotes = "Probni period"`. Nula novih mehanizama — probni period je
  samo unaprijed postavljeni Pro s istekom; po isteku pada na Besplatni kroz izračunati efektivni
  paket.
- Prelazak na niži paket / istek nikad ne dira podatke: košnice/pčelinjaci/pašnjaci/članovi iznad
  limita ostaju čitljivi i izmjenjivi — limiti se provjeravaju **samo pri kreiranju**. AI
  endpointi i kreiranje pašnjaka/selidbi prestaju raditi odmah.
- **Partner** je u enforcementu identičan Max-u (bez limita, sve omogućeno), ali se nikad ne
  prikazuje u javnim listama paketa niti (faza 2) u checkoutu; dodjeljuje ga isključivo
  SystemAdmin. `PlanValidUntil` važi kao i inače (null = doživotno).
- `MaxMembers` broji **dodatne** naloge uz prvog OrganizationAdmina (vlasnika) — tj. ukupan broj
  naloga organizacije minus jedan.
- SystemAdmin organizacije (ne postoje — SystemAdmin nema organizaciju) i SystemAdmin korisnik
  bez organizacije nisu obuhvaćeni gejtovima.

## Backend

### Enforcement — `IPlanGuard` (jedan izvor istine, presedan `IAccessGuard`)

```
IPlanGuard
  EnsureCanAddApiaryAsync(organizationId)      // ApiaryService.CreateAsync
  EnsureCanAddBeehiveAsync(organizationId)     // BeehiveService.CreateAsync
  EnsureCanAddMemberAsync(organizationId)      // endpointi za kreiranje članova organizacije
  EnsureFeatureAsync(organizationId, feature)  // PlanFeature { VoiceInput, WeeklySummary, Pastures, PhotoAnalysis }
                                               //   glasovni parse; weekly worker; PastureService.CreateAsync + kreiranje selidbe;
                                               //   InspectionPhotoService.AnalyzeAsync (SPEC-05 — PhotoAnalysis je Pro+)
  EnsureAdvisorMessageAsync(organizationId)    // savjetnik create/send: gejt funkcije + mjesečna kvota
  GetMyPlanAsync(organizationId)               // efektivni paket + limiti + potrošnja (za DTO/UI)
```

Kvota savjetnika: COUNT **korisničkih** poruka savjetnika organizacije sa `CreatedAt` u tekućem
UTC kalendarskom mjesecu, poređeno sa `AdvisorMessagesPerMonth` (odsutno = neograničeno).
Resetuje se implicitno prvog u mjesecu — bez tabele brojača, bez background joba. Kvota je **po
organizaciji, ne po korisniku** — članovi je dijele (svjesna odluka: štiti trošak i jednostavnija
je; po korisniku bi Standard efektivno imao 30 poruka).

Prekršaji bacaju `PlanLimitException(message)` → GlobalExceptionMiddleware mapira na **402
Payment Required** sa ProblemDetails `{ code: "plan-limit", detail: <poruka na bosanskom> }` —
različito od 403 da frontend može prikazati upsell umjesto "nemate pravo". Primjeri poruka:
*"Besplatni paket uključuje do 7 košnica — nadogradite na Standard."* / *"Iskoristili ste 10 AI
poruka ovog mjeseca — Pro paket nema ograničenja."* / *"Pašnjaci i selidbe su dio plaćenih
paketa."*

`WeeklySummaryService`: preskače organizacije čiji efektivni paket nema `WeeklySummary` (bez
Groq poziva, log-and-continue).

### Endpointi

| Metoda | Putanja | Napomene |
|---|---|---|
| GET | `/api/organizations/my-plan` | `{ plan, planName, effectivePlan, planValidUntil, usage: { apiaries/limit, beehives/limit, members/limit, advisorMessagesThisMonth/limit } }` (null limit = neograničeno) — svaki prijavljeni korisnik |
| PUT | `/api/admin/organizations/{id}/plan` | SystemAdmin: `{ plan, planValidUntil?, planNotes? }` → ažurirana organizacija; prihvata svih pet paketa uklj. Partner (ručna aktivacija u v1) |

Postojeći `AdminService` DTO-ovi organizacija dobijaju polja `plan`/`planValidUntil` (admin lista
prikazuje ko plaća).

### Alarm (SPEC-04 tabela pravila)

`PlanExpiring` (NotificationType = **18** — 15/16 zauzeo SPEC-08, 17 SPEC-06): efektivni paket ≠
Besplatni i `PlanValidUntil` u narednih 7 dana → obavijesti OrganizationAdmine organizacije *"Vaš
{paket} paket ističe {datum} — produžite da zadržite AI funkcije i limite."* (pokriva i istek
probnog perioda); dedup 7 dana, prekidač `Alerts:PlanExpiring:Enabled`.

## Frontend

- Modeli: `PlanType` enum + labeli ("Besplatni", "Standard", "Pro", "Max", "Partner"), `MyPlan`
  model; `planService.ts` + hookovi.
- **Stranica `/plans`** (`features/plans/PlansPage.tsx`, svi prijavljeni korisnici): **četiri
  javne kartice paketa** (tabela funkcija iznad, cijene iz jedne `PLAN_PRICING` konstante —
  20/35/50 KM/mj + godišnja uplata 200/350/500 KM, u v1 informativno), badge trenutnog paketa,
  mjerači potrošnje (košnice {used}/{limit}, članovi, **AI poruke {used}/10 ovog mjeseca** na
  Standardu), CTA **"Kontaktirajte nas za nadogradnju"** (mailto + upute za godišnju uplatu:
  žiro račun, a **svrha uplate sadrži ID organizacije** radi lakšeg uparivanja uplata) — bez
  toka plaćanja u v1. Organizacija na **Partner** paketu vidi samo jednu karticu "Partner paket —
  sve uključeno" umjesto cjenovnika; Partner se nigdje drugdje ne renderuje.
- Prikaz probnog perioda: dok je `PlanNotes = "Probni period"` i nije istekao, `/plans` i
  padajući meni profila prikazuju "Pro (probni period do {datum})".
- **Obrada upsella**: axios interceptor prepoznaje 402 + `code: "plan-limit"` → globalni
  `UpsellModal` (poruka sa servera + link na `/plans`). Ulazne tačke za AI, pašnjake i članove se
  dodatno proaktivno sakrivaju/onemogućavaju prema `myPlan` (upsell hint umjesto mrtvog dugmeta);
  savjetnik na Standardu prikazuje brojač preostale kvote — 402 putanja ostaje kao osigurač.
- **Admin**: `OrganizationFormPage` (SystemAdmin) dobija select paketa (svih pet uklj. Partner) +
  datum `planValidUntil` + `planNotes`; admin tabela organizacija prikazuje kolonu paketa.
- Admini organizacije vide status paketa + istek na `/plans`; navigacija ostaje nepromijenjena
  (bez nove nav stavke — link iz padajućeg menija profila "Paket: {name}").

## Napomena za lansiranje (ručno, jednokratno)

Postojeće organizacije na migraciji dobijaju `Free` (podaci iznad limita ostaju izmjenjivi po
dizajnu). Pri lansiranju SystemAdmin može kroz admin UI nagraditi rane korisnike — npr. 3 mjeseca
Pro, ili Partner za najranije/najaktivnije organizacije — namjerna ručna akcija, bez podrške u
kodu.

## Faza 2 — automatska plaćanja (odvojeni nastavak, van v1)

Merchant-of-record provajder (**Paddle** — dostupan prodavcima iz BiH, rješava EU PDV) →
checkout link na `/plans` → webhook `POST /api/billing/webhook` (novi secret) postavlja
`Plan/PlanValidUntil`; tada mjesečna naplata postaje izvodljiva. Partner ostaje van checkouta.
Šav za v1 su tačno ta dva polja — ne očekuje se promjena šeme.

## Rubni slučajevi

- Standard organizacija pošalje 11. poruku savjetniku u istom mjesecu → 402 s porukom o kvoti;
  radi ponovo prvog u narednom mjesecu.
- Probni period istekne → organizacija se ponaša kao Besplatna: pčelinjaci/košnice/pašnjaci
  kreirani tokom probnog perioda ostaju čitljivi i izmjenjivi; kreiranje preko besplatnih limita
  → 402.
- Organizacija spuštena na niži paket sa 30 košnica: sve ostaje vidljivo/izmjenjivo; kreiranje
  košnice #31 → 402.
- `PlanValidUntil` je danas → paket važi do kraja dana (porede se datumi, ne trenuci).
- Besplatna organizacija otvori `/advisor` sa starim razgovorima: historija je čitljiva (podaci
  se nikad ne zaključavaju); slanje poruke → 402 upsell. Isto za pašnjake: historija selidbi
  čitljiva, novi pašnjak/selidba → 402.
- SystemAdmin postavi Pro/Partner bez isteka → doživotno (rani korisnici / partnerske
  organizacije).
- Partner organizacija nikad ne vidi cjenovnik ni upsell — nijedan gejt za nju ne može okinuti.

## Van opsega (v1)

Online tok plaćanja (faza 2 — Paddle), ručna mjesečna naplata (samo godišnja dok ne stigne
Paddle), naplata po korisničkom nalogu (per-seat), probni periodi mimo jedinog registracionog,
kuponi/kodovi za popust (kanal za udruženja pčelara — kasnije), mjerenje AI potrošnje mimo
mjesečne kvote savjetnika, kvote za pohranu fotografija (definisaće SPEC-05), generisanje PDF
faktura, tabela historije/audita promjena paketa.

## Kriteriji prihvatanja

- [x] Limiti iz configa: Besplatna org → 2. pčelinjak / 8. košnica / 1. dodatni član → 402 sa
      `code: "plan-limit"` i porukom na bosanskom; Standard → 31. košnica / 3. dodatni član →
      402; Pro → 101. košnica → 402; Max/Partner neograničeno (`PlanGuardTests` sa config
      override-ima; 2. pčelinjak na Free live-provjeren → 402 modal).
- [x] Kvota savjetnika: 11. korisnička poruka Standard organizacije u UTC kalendarskom mjesecu →
      402; Pro/Max/Partner neograničeno; brojanje se resetuje preko granice mjeseca (`PlanGuardTests`).
- [x] Besplatna org: glasovni parse + kreiranje pašnjaka/selidbe → 402 (feature gejtovi ožičeni);
      weekly summary worker preskače besplatne/istekle organizacije (`WeeklySummaryPlanTests` —
      `GetAllByOrganizationAsync` se ne poziva za Free org, dakle ni Groq).
- [x] Registracija kreira organizaciju kao Pro sa `PlanValidUntil = +30 dana` i `PlanNotes =
      "Probni period"` (`RegistrationTrialTests`); istekli paket se ponaša identično Besplatnom
      (`PlanHelperTests`, uklj. granicu istog dana).
- [x] Prelazak na niži paket ništa ne zaključava: gejtovi su samo na *kreiranju* (Update/Delete
      putanje netaknute); live-provjereno — Free org sa 5 postojećih članova / 4 košnice ostaje
      čitljiva i mjerači prikazuju prekoračenje bez blokiranja.
- [x] SystemAdmin endpoint za promjenu paketa je zaštićen po roli (`[Authorize(Roles=SystemAdmin)]`),
      prihvata Partner (`AdminPlanUpdateTests`) i odražava se u admin tabeli i `/organizations/my-plan`
      (live: `PUT …/plan → 200`, tabela pokazuje "Standard").
- [x] Frontend: 402 bilo gdje → UpsellModal sa porukom servera + link na `/plans` (live-provjereno);
      `/plans` prikazuje 4 javne kartice + mjerače potrošnje uklj. brojač AI poruka; Partner
      organizacija vidi samo Partner karticu; Partner se ne pojavljuje ni u jednoj javnoj listi;
      AI savjetnik onemogućen uz hint za besplatne organizacije (banner na `AdvisorPage`).
- [x] `PlanExpiring` alarm (NotificationType 18) okida jednom (dedup 7 dana) unutar 7 dana do
      isteka (uklj. probni period), samo OrgAdminima — u `AlertRuleService`.
- [x] Svi labeli na bosanskom (`BsLabels.Label(PlanType)` + `PlanTypeLabels`). Dokumentacija
      ažurirana: `features/plans-billing.md`, `api-contracts.md`, `context.md`, `decisions.md`
      (ADR-028: ručna-godišnja-naplata + 402 + izračunati efektivni paket), ovaj spec → ✅.

> **Napomena (van koda, prije prve naplate):** pravno-porezni okvir (registracija djelatnosti,
> fakture, fiskalizacija), Uslovi korištenja + politika privatnosti, i stvarni podaci žiro računa
> u uputama za uplatu na `/plans`. Zloupotreba probnog perioda (novi email svakih 30 dana) je
> prihvaćen rizik za v1. Javna cjenovna stranica (van logina) dolazi uz Paddle fazu 2.
