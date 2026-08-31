# SPEC-23 — Mobilne aplikacije (Google Play + App Store)

| | |
|---|---|
| **Status** | 🔨 Faza 4 djelimično isporučena (2026-08-31) — **brisanje računa i prenos vlasništva** te **politika privatnosti** rade na webu, prije ostatka specifikacije. Vidi `features/account-deletion.md` i `features/legal-pages.md`. Ostalo 📋 Planned |
| **Effort** | L — kod ~2–3 sedmice; **plus 14 dana čekanja** na Google zatvoreno testiranje koje teče paralelno |
| **Depends on** | ništa u kodu. Dodiruje SPEC-04 (push nosi upozorenja), SPEC-06 (TTS), SPEC-07 (outbox ostaje), SPEC-08 (PDF registar), SPEC-09 (skriven CTA), SPEC-22 (prenos vlasništva) |
| **New secrets / packages** | Firebase service account JSON (FCM), APNs `.p8` ključ, Android keystore; `@capacitor/*` paketi |
| **Breaking** | Ne za web. Backend dobija jednu tabelu i četiri endpointa; `AllowedOrigins` mora dobiti dva nova origina |

## Goal

Korisnici pitaju kako da skinu Melarium. Trenutni odgovor je "otvori sajt u pregledniku pa dodaj na
početni ekran" — to je PWA instalacija (ADR-010), radi, ali je ljudima nepoznata i djeluje kao da
aplikacija ne postoji. Ovaj spec je stavlja na **Google Play i App Store**, gdje je ljudi i traže.

Web aplikacija ostaje **netaknuta** i nastavlja raditi kako radi danas, uključujući PWA instalaciju
za one koji je već koriste. Mobilna aplikacija je dodatak, ne zamjena.

Tvrdo pravilo: **mobilna aplikacija nema nijednu funkciju manje od weba.** Sve što na webu radi mora
raditi i u aplikaciji — a nekoliko stvari u webview-u ne radi bez intervencije (§ Domain rules), pa
je najveći dio ovog specifikacije upravo to: ne nove funkcije, nego postojeće koje bi tiho nestale.

## Decisions (settled with Asim before implementation, 2026-08-31)

### D1 — Capacitor, ne React Native

Capacitor pakuje postojeći `vite build` izlaz u pravi native Xcode / Android Studio projekat. Isti
kod, isti ekrani, isti feature set — **parity je posljedica arhitekture, a ne obećanje o disciplini.**

Razmatrano i odbijeno: React Native / Expo i .NET MAUI. Oba znače drugi UI codebase, dakle prepisati
172 fajla i ~31.000 linija, pa zauvijek pisati svaku novu funkciju dva puta. Jedan čovjek to ne
održava, i prva funkcija koja se pojavi samo na webu ruši tvrdo pravilo iz § Goal.

Odbijen je i puki webview wrapper (TWA i slično): Apple ga odbija po pravilu 4.2 (*Minimum
Functionality*). Ono što aplikaciju odvaja od "sajta u okviru" su native mogućnosti — push, kamera,
GPS, offline — i zato D3 nije opciono.

### D2 — Obje platforme paralelno

Isti Capacitor projekat rađa oba builda. Asim ima pristup Macu, pa iOS ne čeka nabavku opreme i
cloud CI (Codemagic i sl.) nije potreban za prvi release.

### D3 — Push notifikacije ulaze u **prvi** release

Ovo je izričito traženo: prave notifikacije na telefon, ne postojeće in-app zvono. Danas
`NotificationBell` povlači `/notifications` dok je aplikacija otvorena — što znači da upozorenje o
mrazu (SPEC-04) korisnik vidi tek kad se sam sjeti otvoriti aplikaciju, a to je tačno suprotno od
smisla upozorenja.

Push je i najjači argument protiv odbijanja po Apple 4.2.

### D4 — Dugme za nadogradnju paketa je **skriveno** u mobilnoj aplikaciji

Apple pravilo 3.1.1 traži svoj IAP za sve što se u aplikaciji prodaje, a `PlansPage` danas nudi
`mailto:` za nadogradnju — to je poziv na kupovinu izvan prodavnice i predvidiv razlog za odbijanje.

U aplikaciji `PlansPage` i dalje prikazuje **trenutni paket, limite i datum isteka**; umjesto dugmeta
stoji rečenica da se nadogradnja radi na `melarium.app`. Razlika u odnosu na web je jedno dugme.

Odbijeno: implementirati IAP / Play Billing. To je vlastiti spec (validacija računa na backendu,
sinhronizacija s ručnim godišnjim modelom iz ADR-028, 15–30% provizije), a naplata je i danas ručna
pa aplikacija ionako ne bi ništa aktivirala sama.

Ovo je **jedina namjerna razlika** između weba i mobilne aplikacije. Svaka druga razlika je bug.

### D5 — Nalozi u prodavnicama se registruju na **fizičko lice**

Posljedice koje su prihvaćene svjesno:

- Na App Storeu kao prodavac stoji Asimovo ime i prezime, javno.
- Google Play traži **zatvoreno testiranje: ~12 testera koji drže aplikaciju instaliranu 14 dana
  neprekidno**, prije nego što je uopšte dozvoljen izlazak na produkciju. Nalozi na firmu su toga
  izuzeti, ali traže D-U-N-S broj i vlastito čekanje.

To čekanje je **najduži rok u cijelom projektu i ne rješava se kodom** — kreće paralelno s Fazom 1,
a ne kad kod bude gotov. Provjeriti aktuelno pravilo u Play konzoli, Google ga je već mijenjao.

### D6 — Brisanje računa: tri slučaja, i organizacija ide samo sa zadnjim adminom

Apple 5.1.1(v) i Play politika traže da korisnik može obrisati račun **iz same aplikacije**. Danas
takva funkcija ne postoji nigdje u kodu — `AdminService.DeleteUserAsync` je alat SystemAdmina, ne
korisnika. Bez ovoga nema izlaska na prodavnice.

| Ko briše | Šta se dešava |
|---|---|
| ApiaryAdmin / pčelar | Briše se korisnik i njegovi lični zapisi. Podaci organizacije ostaju — nisu njegovi. |
| OrganizationAdmin koji je **sam** u organizaciji | Briše se korisnik **i organizacija sa svim podacima**. Potvrda upisivanjem naziva organizacije. |
| OrganizationAdmin koji **ima članove** | Mora prvo prenijeti vlasništvo (D7). Poslije prenosa njegovo brisanje je obično brisanje iz prvog reda. |

Zašto treći red nije "obriši i organizaciju": u organizaciji s pet pčelara jedan čovjek bi pritiskom
na jedno dugme uništio tuđe pčelinjake, tuđe preglede i **tuđe naloge za prijavu**, a niko od njih to
nije odobrio. Pravo na brisanje je pravo na brisanje **svog** računa.

Zašto onda nije dovoljno samo zabraniti: Apple traži da brisanje bude *uvijek* moguće. Trajna zabrana
bez izlaza je ćorsokak i sama po sebi razlog za odbijanje. Prenos vlasništva je taj izlaz.

### D7 — Prenos vlasništva organizacije (nova funkcija, posljedica D6)

Danas organizacija ima **tačno jednog** OrganizationAdmina — onog ko ju je napravio pri registraciji
— i ne postoji način da se napravi drugi: `MembersPage` tipizira ulogu člana kao
`'ApiaryAdmin' | 'Beekeeper'`, a `OrgManagementService` nema nijednu metodu za promjenu uloge.

Zato D6 traži novu radnju: OrgAdmin bira člana, taj član postaje `OrganizationAdmin`, a dosadašnji
admin pada na **`Beekeeper`**. Organizacija i dalje ima tačno jednog vlasnika — model se ne mijenja,
mijenja se samo ko je on.

*(Ispravka pri implementaciji: ovdje je prvo pisalo `ApiaryAdmin`. Ne može — ta uloga po pravilu
`ValidateRoleOrgApiaryConsistency` **mora** biti vezana za konkretan pčelinjak, a nema poštenog
načina da se izabere koji. Kao pčelar bez dodjela dosadašnji vlasnik ne vidi ništa dok mu novi ne
da pristup, što i jeste smisao predaje. Iz istog razloga se nasljedniku briše `ApiaryId`.)*

Uzgredna korist koja nije bila motiv ali jeste rezultat: rješava i "šta ako vlasnik napusti
gazdinstvo" — danas organizacija ostane bez ikoga ko je može voditi.

Uloga je JWT claim, pa prenos **ukida sesije oba korisnika** kroz postojeći `ISessionRevoker` — isto
pravilo koje već vrijedi za svaku izmjenu uloge (SPEC-22 / auth).

### D8 — Jedan build za sve; native se prepoznaje u runtimeu, ne build flagom

`Capacitor.isNativePlatform()` vraća `false` u web buildu, pa isti `dist/` služi i nginxu i
`npx cap sync`. Nema drugog builda koji se može zabunom deployati na pogrešno mjesto.

Jedina stvar koja se stvarno razlikuje je adresa API-ja, jer u aplikaciji origin nije `melarium.app`:

```ts
// apiClient.ts
const baseURL = Capacitor.isNativePlatform()
  ? (import.meta.env.VITE_NATIVE_API_URL ?? 'https://melarium.app/api')
  : (import.meta.env.VITE_API_URL ?? '/api')
```

Posljedica koju treba izgovoriti naglas: **CORS postaje stvaran.** Komentar na `Program.cs:184` kaže
da se CORS u produkciji "ne koristi zapravo", jer web i API dijele origin. Aplikacija ga koristi —
`AllowedOrigins` mora dobiti `capacitor://localhost` (iOS) i `https://localhost` (Android).

### D9 — Service worker je isključen u nativnom buildu, outbox ostaje

`vite-plugin-pwa` sam ubacuje registraciju service workera. U webview-u je taj SW u najboljem slučaju
suvišan (bundle je ionako lokalan), a u najgorem servira staru verziju unutar objavljene aplikacije —
kvar koji se teško dijagnosticira i koji se ne može popraviti bez novog store releasa.

Zato: `injectRegister: null` u konfiguraciji, ručna registracija u `main.tsx` iza
`!Capacitor.isNativePlatform()`. Web ponašanje se ne mijenja ni za jedan znak.

Offline outbox (SPEC-07) **ostaje netaknut**: on je IndexedDB na nivou aplikacije, nema veze sa
service workerom (ADR-026 je to i odabrao svjesno) i u webview-u radi isto.

### D10 — FCM za obje platforme

Firebase Cloud Messaging prosljeđuje na APNs, pa je to jedna integracija umjesto dvije: jedan
`IPushSender`, jedan format poruke, jedno mjesto gdje se griješi. Apple i dalje traži svoj `.p8`
APNs ključ, ali on se samo otpremi u Firebase.

### D11 — Push se šalje **poslije** commita notifikacije, i njegov kvar ne ruši zapis

`NotificationService` je **jedino** mjesto u backendu gdje se `Notification` red kreira — kroz njega
prolaze i `AlertScanWorker` i `DailyAgendaWorker`. Push se zato kači na jednu tačku, a ne po kodu.

Slanje ide **nakon** `SaveChangesAsync`, u `try/catch`, s logiranjem. Razlog je isti onaj iz SPEC-15
§ nagrade: `DbContext` je dijeljen, pa bi izuzetak iz slanja srušio i sam upis notifikacije. In-app
notifikacija je izvor istine i mora preživjeti FCM ispad; propuštena push poruka je neugodnost, a
izgubljena notifikacija je izgubljen podatak.

Odbijeno za v1: red poruka i worker po uzoru na `QueuedEmail` / ADR-021. Push je efemeran — poruka o
mrazu koja stigne sutra nije vrijedna reda — a jedina tačka slanja se kasnije pretvara u worker bez
ikakve izmjene pozivatelja, ako obim to jednom zatraži.

### D12 — Ažuriranja idu kroz prodavnice; bez live updatesa u v1

Pošto je UI zapakovan u aplikaciju, svaka izmjena ekrana traži novi store release (Apple ~1–3 dana,
Google obično brže). To je prihvaćeno.

Odbijeno za v1: live update mehanizmi (Capgo, Appflow). Dodaju vlastitu infrastrukturu i vlastiti
rizik pred pravilima prodavnica, a prvi release ionako treba proći review "onakav kakav jeste".

Web se i dalje deploya kad se hoće — `deploy/deploy.sh` se ne mijenja.

## User stories

- Kao pčelar nađem Melarium na Google Play-u i skinem ga kao svaku drugu aplikaciju, bez objašnjavanja šta je "dodaj na početni ekran".
- Kao pčelar dobijem upozorenje o mrazu na telefon dok aplikacija nije otvorena, i tapom na notifikaciju odem tačno na tu košnicu.
- Kao pčelar iz aplikacije preuzmem i podijelim PDF registar tretmana, isto kao na webu.
- Kao pčelar u aplikaciji čujem "Poslušaj" na edukaciji, isto kao na webu.
- Kao pčelar u polju bez signala unesem pregled i on se sinhronizuje kad se signal vrati — kao i do sad.
- Kao korisnik obrišem svoj račun iz same aplikacije, bez pisanja mejla ikome.
- Kao administrator organizacije prenesem vlasništvo na kolegu prije nego odem.

## Domain rules

### Šta u webview-u puca ako se ne dira (Faza 2)

| Šta | Zašto | Rješenje |
|---|---|---|
| **PDF izvoz ne radi** — tiho, bez greške | `qrPdf.ts` i `treatmentPdf.ts` zovu jsPDF `.save()`, što je preuzimanje u pregledniku; webview nema gdje da preuzme | `@capacitor/filesystem` + `@capacitor/share` |
| **"Poslušaj" nestaje na Androidu** (SPEC-06) | `useSpeech` traži `speechSynthesis`, kojeg Android WebView nema; `isSupported` bi samo sakrio dugme — tiho manje funkcionalnosti | native TTS plugin |
| **Sesija se gubi na iOS-u** | JWT i refresh token su u `localStorage`; WKWebView ga zna očistiti pod pritiskom na memoriju | sigurno spremište, izolovano u `authService.ts` |
| **Linkovi iz mejlova otvaraju preglednik** | Potvrda e-pošte, reset lozinke i pozivnice vode na `FrontendUrl` | Universal Links / App Links |
| **Android hardversko dugme "nazad" zatvara aplikaciju** | webview ne zna za React Router | `@capacitor/app` → `router.back()`, izlaz tek na korijenskoj ruti |
| Sadržaj ulazi u "zarez" (notch) i ispod statusne trake | — | safe-area insets + `@capacitor/status-bar` |
| **Dugi pritisak na karticu podiže lupu i "Copy / Look Up"** | `index.css` nigdje ne postavlja `user-select`, pa se webview ponaša kao stranica; native aplikacije tako ne rade | `user-select: none` na UI, a `text` samo tamo gdje tekst zaista treba kopirati (bilješke pregleda, AI odgovori, edukacija). Uz to `-webkit-tap-highlight-color: transparent` i ukroćen overscroll bounce |

Zadnji red nije kozmetika nego **rizik za review**: to je jedan od signala po kojima recenzent
zaključi da gleda sajt u okviru (Apple 4.2). Aplikacija inače na tom pitanju stoji dobro — nema
nijednog `window.confirm` / `alert` / `prompt` u cijelom kodu, nema URL trake, nema linkova ka sajtu
niti "instaliraj aplikaciju" bannera u UI-u. Nema ni social logina ni OAuth-a, pa se **pravilo 4.8
(obavezan "Sign in with Apple") ne primjenjuje** — cijela ta kategorija problema otpada.

Kamera (`<input capture>`, zxing QR), geolokacija i snimanje glasa rade u webview-u i **ne mijenjaju
se u v1** — traže samo permisije i tekstove razloga (§ Permisije). Native kamera i MLKit skener su
poboljšanja, ne parity, i idu u Fazu 5.

### Push

| Pravilo | Gdje |
|---|---|
| Push se šalje samo ako korisnik ima bar jedan registrovan uređaj i `User.PushEnabled` | `NotificationService` |
| Naslov i tekst push poruke = naslov i tekst in-app notifikacije. Bez posebnog teksta | `NotificationService` |
| Payload nosi `notificationId`, `type`, `relatedEntityId` — tap otvara tu rutu | `FcmPushSender` + `App` listener |
| FCM odgovor `UNREGISTERED` / `INVALID_ARGUMENT` briše token iz baze | `FcmPushSender` |
| Token se registruje pri prijavi i pri svakom pokretanju aplikacije (`LastSeenAt`) | frontend |
| Odjava briše token s ovog uređaja — **ne** i s ostalih | `DELETE /devices/{token}` |
| Kvar slanja ne poništava upis notifikacije (D11) | `NotificationService` |

Dedup iz SPEC-04 (`ExistsRecentAsync`) ostaje jedina zaštita od poplave: push preslikava notifikaciju,
pa ako se notifikacija ne ponavlja, ne ponavlja se ni push.

### Brisanje računa

Redoslijed u `DeleteMyAccountAsync`, jedna transakcija:

1. Provjeri lozinku iz tijela zahtjeva. **Bez toga** ukradeni otključan telefon briše račun u dva tapa.
2. Ako je korisnik OrgAdmin i organizacija ima još članova → **422** s porukom da prvo prenese
   vlasništvo. (Specifikacija je prvo govorila `409`; kućno pravilo je `BusinessRuleException` → 422,
   i svako odbijanje ovdje ide istim putem.)
3. `Todo.AssignedToId = null` za sve zadatke dodijeljene ovom korisniku. **Obavezno**, i lako se
   previdi: `TodoConfiguration` veže `AssignedToId` sa `DeleteBehavior.NoAction`, pa brisanje puca na
   FK. To je isti onaj postojeći bug koji `AdminService.DeleteUserAsync` ima i danas (SPEC-16 Faza C).
4. Obriši korisnika. Kaskade rješavaju: `RefreshToken`, `UserToken`, `Notification`, `AnnouncementRead`,
   `LearningTopicRead`, `CalendarSettings`, `AdvisorConversation`, `AiAssistantSession`, `UserBeehive`,
   `DeviceToken`.
5. Ako je bio jedini član organizacije → obriši i organizaciju.

Šta **ostaje** i postaje anonimno, po već postojećoj konfiguraciji `SetNull`: `Feedback`, `Invitation`
(i pozivalac i pozvani), `Todo.CreatedById`, `Expense`, `Apiary`/`Beehive.CreatedBy`, `ApiaryMove`,
`BeehiveMerge`. Trag rada organizacije se ne gubi kad jedan član ode — to je već bila namjera tih
konfiguracija, ovaj spec je samo prvi koji je iskoristi.

Pčelarski podaci (`Inspection`, `Harvest`, `Beehive`, `Apiary`, `TreatmentEntry`) **nemaju FK na
korisnika** — vise o organizaciji. Brisanje računa ih ne može dotaći.

⚠️ Kad se briše i organizacija, briše se **i zakonski registar tretmana** (SPEC-08), koji je SPEC-19
posebno štitio od nestanka. To je ispravno — korisnik traži da njegovi podaci nestanu — ali mora biti
**izričito napisano u potvrdi**, ne otkriveno poslije.

`DeleteOrganizationAsync` danas odbija organizaciju koja ima korisnike, pa brisanje ide redoslijedom
korisnik → organizacija. Ne dirati taj metod: SPEC-16 Faza C ima svoj plan s njim.

### Prenos vlasništva

| Pravilo | Gdje |
|---|---|
| Samo OrganizationAdmin pokreće prenos | atribut na kontroleru |
| Meta mora biti član **iste** organizacije, i ne smije biti sam pozivatelj | `OrgManagementService` |
| Meta postaje `OrganizationAdmin`, pozivatelj postaje `ApiaryAdmin` | isto |
| `ApiaryId` novog admina se briše (OrgAdmin nije vezan za jedan pčelinjak) | isto, uz `ValidateRoleOrgApiaryConsistency` |
| Obje sesije se ukidaju — uloga je JWT claim | `ISessionRevoker` |
| Potvrda upisivanjem imena člana; radnja je nepovratna bez saradnje novog vlasnika | frontend |

## API

| Method | Path | Ko | Vraća |
|---|---|---|---|
| POST | `/devices` | prijavljen | `204` — registruje ili osvježava token uređaja |
| DELETE | `/devices/{token}` | prijavljen | `204` — pri odjavi, idempotentno |
| GET | `/profile/deletion-preview` | prijavljen | `AccountDeletionPreviewDto` — koji od tri ishoda slijedi ✅ |
| DELETE | `/profile` | prijavljen | `204` — tijelo nosi lozinku; **422** kad treba prenos vlasništva ✅ |
| POST | `/org/transfer-ownership` | OrgAdmin | `204` — tijelo `{ memberId }` ✅ |

Sve četiri rute postoje i na webu — nijedna nije "mobilna". Brisanje računa i prenos vlasništva su
funkcije aplikacije, ne prodavnice; prodavnica je samo razlog zašto su sad prioritet.

## Faze

Faze su nezavisno isporučive i idu ovim redom.

### Faza 0 — administracija (kreće **odmah**, paralelno s kodom)

Ne piše se nijedna linija koda, a najduži je rok. Vidi § Non-code checklist.

### Faza 1 — Capacitor skelet

Capacitor u repo, `ios/` i `android/` projekti, `apiClient` po D8, CORS po D8, service worker po D9,
ikona i splash. **Cilj: aplikacija se pokreće na Asimovom telefonu i uspješno se prijavljuje.**

### Faza 2 — native rupe

Sve iz tabele "Šta u webview-u puca". Kraj faze = na uređaju se ne primjećuje nijedna razlika u odnosu
na web osim D4.

### Faza 3 — push

`DeviceToken`, `IPushSender` / `FcmPushSender`, poziv iz `NotificationService`, registracija tokena i
rukovanje tapom na frontendu, prekidač u profilu.

### Faza 4 — brisanje računa i prenos vlasništva ✅ (isporučeno 2026-08-31, prije ostatka)

D6 i D7 su **gotovi i rade na webu** — vidi `features/account-deletion.md`. Isporučeni su prvi na
Asimov zahtjev, jer su korisni sami po sebi i ne čekaju nijednu odluku o prodavnicama.

Politika privatnosti je također gotova (`/privatnost`, `features/legal-pages.md`).

Ostaje u ovoj fazi: **javna stranica za zahtjev za brisanje računa** (Google je traži i za one koji
su aplikaciju već deinstalirali), pravni identitet voditelja obrade na stranici privatnosti, i
uslovi korištenja.

### Faza 5 — objava

Zatvoreno testiranje na Google Play-u (počelo u Fazi 0), TestFlight, metapodaci, pa produkcija na obje
prodavnice.

## Files

### Backend

| Fajl | Uloga |
|---|---|
| `Domain/Entities/DeviceToken.cs` | **novo** — `UserId`, `Token` (unique), `Platform`, `CreatedAt`, `LastSeenAt` |
| `Entity/Configurations/DeviceTokenConfiguration.cs` | **novo** — cascade na korisnika, unique index na token |
| `Entity/Migrations/…_AddDeviceTokens.cs` | **novo** — jedna tabela |
| `Application/Common/Interfaces/IPushSender.cs` | **novo** |
| `Infrastructure/Push/FcmPushSender.cs` | **novo** — FCM HTTP v1, service account iz env varijable |
| `Application/Features/Notifications/NotificationService.cs` | slanje pusha poslije commita (D11) |
| `Application/Features/Profile/**` | `DeleteMyAccountAsync` + validator lozinke |
| `Application/Features/OrgManagement/OrgManagementService.cs` | `TransferOwnershipAsync` |
| `API/Controllers/DevicesController.cs` | **novo** |
| `API/Controllers/ProfileController.cs` | `DELETE /profile` |
| `API/Controllers/OrganizationsController.cs` | `POST /my/transfer-ownership` |
| `Domain/Entities/User.cs` | `PushEnabled` (bool, default `true`) |
| `API/appsettings.json` + `docker-compose.yml` + `.env.example` | `AllowedOrigins` + FCM varijable |

### Frontend

| Fajl | Uloga |
|---|---|
| `capacitor.config.ts`, `ios/`, `android/` | **novo** |
| `core/services/apiClient.ts` | baseURL po D8 |
| `core/services/authService.ts` | token u sigurno spremište na nativnom |
| `core/native/push.ts` | **novo** — dozvola, registracija, tap → ruta |
| `core/native/share.ts` | **novo** — jsPDF izlaz kroz Filesystem + Share |
| `core/native/backButton.ts` | **novo** — Android "nazad" |
| `core/hooks/useSpeech.ts` | native TTS kad `isNativePlatform()` |
| `shared/utils/qrPdf.ts`, `treatmentPdf.ts` | izlaz kroz `share.ts` umjesto `.save()` |
| `features/plans/PlansPage.tsx` | D4 — CTA skriven na nativnom |
| `features/profile/**` | prekidač za push, brisanje računa s potvrdom |
| `features/members/MembersPage.tsx` | prenos vlasništva |
| `main.tsx` | ručna registracija SW-a iza web guarda (D9) |
| `vite.config.ts` | `injectRegister: null` |
| `core/help/helpContent.ts` | pomoć za nove ekrane (SPEC-14) |

### Deploy

| Fajl | Uloga |
|---|---|
| `deploy/nginx.melarium.conf.example` | `/.well-known/apple-app-site-association` i `assetlinks.json`, `application/json`, bez redirecta |
| `docs/deployment.md` | nove env varijable i objava aplikacija |

## Permisije

| Platforma | Šta | Zašto |
|---|---|---|
| iOS `Info.plist` | `NSCameraUsageDescription` | fotografije pregleda, QR, skeniranje računa |
| | `NSMicrophoneUsageDescription` | glasovni unos (SPEC-17) |
| | `NSLocationWhenInUseUsageDescription` | lokacija pčelinjaka |
| | `NSPhotoLibraryUsageDescription` | logotip, fotografije iz galerije |
| Android | `CAMERA`, `RECORD_AUDIO`, `ACCESS_FINE_LOCATION`, `POST_NOTIFICATIONS` | isto + push (Android 13+) |

Tekstovi razloga su na bosanskom i moraju reći **šta aplikacija radi s tim**, ne "za bolje iskustvo" —
Apple odbija generičke.

## Non-code checklist (Faza 0)

- [ ] Apple Developer Program — $99/god, fizičko lice (D5)
- [ ] Google Play Console — $25 jednokratno, fizičko lice (D5)
- [ ] **~12 testera za Google zatvoreno testiranje**, 14 dana neprekidno — skupiti ljude odmah
- [ ] Politika privatnosti na `melarium.app` — javni URL, traže je obje prodavnice
- [ ] Uslovi korištenja
- [ ] Javna stranica za zahtjev za brisanje računa (Play politika)
- [ ] Reviewer nalog na produkciji s pravim podacima — demo nalozi su u produkciji zaključani (`Program.cs`)
- [ ] Play Data Safety obrazac i Apple Privacy Labels
- [ ] Age rating; odgovor na pitanje o AI sadržaju (Groq asistent)
- [ ] Android keystore + Play App Signing
- [ ] APNs `.p8` ključ → Firebase
- [ ] Screenshotovi, opis, ikona za obje prodavnice

## Out of scope (v1)

- IAP / Play Billing (D4) — vlastiti spec ako naplata ikad prestane biti ručna
- Live update mehanizmi (D12)
- Native kamera i MLKit QR skener — poboljšanja, ne parity; Faza 5
- Biometrijska prijava
- Podešavanje pusha **po vrsti** notifikacije — v1 ima jedan prekidač; OS ionako nudi svoje gašenje
- Batch slanje na FCM — jedan poziv po notifikaciji dok obim to ne zatraži
- Izvoz svih podataka prije brisanja računa (razmatrano; PDF registra tretmana se već može preuzeti ručno)
- Widgeti, Apple Watch, Siri prečice
- Objava na Huawei AppGallery i sličnim prodavnicama

## Acceptance criteria

- [ ] Aplikacija se instalira iz Google Play-a i s App Storea i prijavljuje se na produkcijski backend
- [ ] Svaka ruta koja postoji na webu postoji i radi u aplikaciji — jedina razlika je D4
- [ ] PDF registar tretmana i QR naljepnice se u aplikaciji otvaraju i dijele
- [ ] "Poslušaj" radi i na Androidu i na iOS-u
- [ ] Unos pregleda bez signala ide u outbox i sinhronizuje se pri povratku signala
- [ ] Prijava preživi gašenje i ponovno pokretanje aplikacije na iOS-u
- [ ] Link iz mejla za reset lozinke otvara aplikaciju, ne preglednik
- [ ] Hardversko "nazad" na Androidu vraća na prethodni ekran, a ne gasi aplikaciju
- [ ] Push stiže na oba sistema dok je aplikacija zatvorena; tap otvara tačan ekran
- [ ] Gašenje pusha u profilu ga zaista zaustavlja; in-app notifikacija i dalje stiže
- [ ] Ispad FCM-a ne spriječi upis in-app notifikacije (test s namjernom greškom)
- [ ] Odjava briše token samo s tog uređaja
- [ ] Brisanje računa traži lozinku i briše korisnika i njegove lične zapise
- [ ] Brisanje računa korisnika koji ima dodijeljen zadatak **ne puca na FK**
- [ ] OrgAdmin s članovima dobija `409` i uputu za prenos vlasništva
- [ ] OrgAdmin sam u organizaciji briše i organizaciju, uz potvrdu naziva i uz izričito upozorenje o registru tretmana
- [ ] Prenos vlasništva mijenja obje uloge i ukida obje sesije
- [ ] `PlansPage` u aplikaciji nema nijedan poziv na kupovinu; na webu je nepromijenjen
- [ ] Web aplikacija se ponaša identično kao prije ovog specifikacija, uključujući PWA instalaciju i service worker
- [ ] Aplikacija radi u tamnoj i svijetloj temi, i sadržaj ne ulazi u notch
- [ ] Cijeli backend test paket prolazi

## Verification note

Po `melarium-local-verification-limits`: nema lokalnog Postgresa ni iOS uređaja, pa vrijedi podjela.

- **Android** se provjerava do kraja na Asimovom telefonu — instalacija, push, kamera, offline, "nazad".
- **iOS** traži Mac (ima ga) i **pravi uređaj za push** — simulator za APNs nije dovoljan dokaz.
- **Backend** ide `dotnet build` + `dotnet test`; brisanje računa i prenos vlasništva su čisti servisni
  testovi i pišu se s ostatkom paketa.
- **Migracija** `AddDeviceTokens` se primjenjuje sama pri restartu kontejnera (`Program.cs` bezuslovno
  zove `db.Database.MigrateAsync()`), dakle kroz `deploy/deploy.sh` — nema ručnog koraka.
- Redoslijed koji se ne preskače: **backend s pushom mora biti na produkciji prije nego što aplikacija
  ode u zatvoreno testiranje**, inače testeri 14 dana testiraju verziju bez glavne nove funkcije.
