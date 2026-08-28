# SPEC-20 — Kontakt i podrška ("Contact & support")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-08-28) — see `features/contact-support.md` |
| **Effort** | S (~0.5 dana; frontend-only, bez sheme, bez API-ja) |
| **Depends on** | ništa. Soft-link na SPEC-13 (forma za prijavu problema), koja ostaje netaknuta |
| **New secrets / packages** | nema |
| **Breaking** | Ne — čisto aditivno |

## Goal

Korisnik koji zapne nema nijedan način da dođe do živog čovjeka. Jedini kanal koji danas postoji je
SPEC-13 forma "Prijavi problem / pohvali" — asinhrona, bez povratne informacije kada će odgovor
stići, i **dostupna samo prijavljenom korisniku**. Čovjek koji se ne može prijaviti — a to je
najčešći razlog za javljanje — nema apsolutno ništa.

Ovaj spec dodaje **kontakt modal** s direktnim kanalima (WhatsApp, Viber, telefon, email), dostupan
s **login i register ekrana** jednako kao i iz aplikacije.

## User stories

- Kao neko ko se ne može prijaviti, s login ekrana otvorim kontakt i pošaljem WhatsApp poruku — bez
  gubljenja onoga što sam već upisao u formu.
- Kao korisnik na terenu bez signala, i dalje vidim broj telefona i mogu nazvati.
- Kao korisnik na desktopu bez instaliranog Vibera, kopiram broj umjesto da kliknem link koji ne radi.
- Kao korisnik koji piše mail, poruka mi je već pripremljena s mojim imenom i organizacijom, pa ne
  moram objašnjavati ko sam.
- Kao Asim, dobijem prijavu iz koje odmah znam ko piše i s koje stranice.

## Domain rules

### D1 — Modal svugdje, bez `/kontakt` rute (pročitati prvo)

Prvi nacrt je bio javna ruta `/kontakt`. Odbačen je iz razloga koji se ne vidi dok se ne proba:
`/login` i `/kontakt` su dvije rute, pa odlazak na kontakt **demontira login formu i briše upisani
email i lozinku**. Korisnik koji ima problem s prijavom je tačno taj koji bi to najviše osjetio.

Posljedica: **nema nove rute, nema nove stranice.** Postoji jedan `ContactModal`, otvoren s tri
mjesta. Ovo je namjerno, ne propust — link na kontakt se zato ne može poslati niti sačuvati kao
bookmark, i to je prihvaćena cijena.

### D2 — Podaci su konstanta u frontendu, ne API i ne env

Kontakt ne mora biti promjenjiv bez deploya (izričita odluka). Uz to se aplikacija koristi offline
(SPEC-07): broj dobavljen s backenda bio bi **nevidljiv tačno onda kada je telefon jedini preostali
kanal**. Zato sve živi u `core/contact/contactInfo.ts` — jedan fajl, bez mrežnog poziva.

### D3 — `info@melarium.app`, ne `noreply@`

`noreply@melarium.app` je Resend **adresa za slanje** (`EmailService.cs`, `SMTP_FROM_EMAIL`) i ostaje
netaknuta. Kontakt adresa je `info@melarium.app`. Ime `noreply` korisniku poručuje "ne odgovaraj",
što je suprotno od svrhe ovog ekrana.

### D4 — Kopiranje je obavezno, ne ukras

`viber://` deep link na desktopu bez instaliranog Vibera **tiho ne uradi ništa** — nema greške, nema
poruke, korisnik misli da je aplikacija pokvarena. Zato svaki red s brojem ili adresom ima dugme
"Kopiraj". Isto vrijedi za `tel:` na desktopu.

### D5 — Kontekst u pripremljenoj poruci samo kad je korisnik prijavljen

`mailto` i WhatsApp tekst se popunjavaju imenom, organizacijom, ulogom i trenutnom stranicom — ali
samo kada `useAuth()` ima korisnika. Na login/register ekranu ide generičan subject. Kontekst se
uzima **isključivo iz onoga što `AuthContext` već drži** — nema dodatnog upita za paket ili profil,
jer bi modal tada mogao zavrtjeti mrežni poziv na ekranu čija je cijela svrha da radi kad mreža ili
prijava ne rade.

### D6 — SPEC-13 forma ostaje odvojena

Kontakt i "Prijavi problem / pohvali" nisu duplikat i ne spajaju se:

| | Kontakt (ovaj spec) | Prijavi problem (SPEC-13) |
|---|---|---|
| Kada | Hitno, razgovor | Prijava s dokazom |
| Ko | Svako, i neprijavljen | Samo prijavljen korisnik |
| Nosi | Poruku | Screenshot + trag u bazi + trijaža |

### D7 — Bez pravnog bloka

Melarium još nije registrovana firma. Uslovi korištenja i politika privatnosti su zasebna tema i
**nisu** dio ovog speca.

### Rules

- Jedan broj: `+387603209030` (E.164), isti za poziv, Viber i WhatsApp. Prikaz: `+387 60 32 09 030`.
- Obećanje odgovora: **24 sata**, bez radnog vremena.
- Svi tekstovi na bosanskom.

## Frontend

### Novi fajlovi

| Fajl | Šta radi |
|---|---|
| `core/contact/contactInfo.ts` | Broj, email, prikazni oblici, graditelji linkova, prefill poruke |
| `shared/components/ContactModal.tsx` | Modal s listom kanala (koristi postojeći `Modal`) |
| `shared/components/ContactLink.tsx` | Samostalan trigger + vlastito stanje — za auth ekrane koji nemaju `Layout` |

### Ulazne tačke

| Gdje | Oblik | Fajl |
|---|---|---|
| Footer, svaka stranica | Link pored copyrighta | `Layout.tsx` |
| Profil dropdown (desktop) | Stavka "Kontakt i podrška" | `Layout.tsx` |
| Mobilni hamburger panel | Ista stavka — **obje kopije, uvijek** | `Layout.tsx` |
| Login | Pill ispod kartice | `LoginPage.tsx` |
| Register | Pill ispod kartice | `RegisterPage.tsx` |
| Forgot / Reset / Verify | Pill ispod kartice | `AuthCard.tsx` (pokriva sva tri) |

`Layout` drži jedno `contactOpen` stanje i renderuje `ContactModal` jednom, isto kao što već radi za
`FeedbackFormModal`. Auth ekrani koriste `ContactLink`, koji nosi svoje stanje.

### Namjerno **nije** uključeno

Dno Help panela, ErrorBoundary, NotFoundPage i command palette su razmatrani i **odbijeni za v1**.
Nisu zaboravljeni — ako se ispostavi da ljudi ne nalaze kontakt, Help panel je prvi sljedeći kandidat.

## Backend

Nema izmjena. Nema migracije, nema endpointa, nema novog env vara.

## Edge cases

| Slučaj | Ponašanje |
|---|---|
| Viber nije instaliran (desktop) | Link ne uradi ništa — zato dugme "Kopiraj" u istom redu |
| `navigator.clipboard` blokiran (http, stari browser) | Kopiranje tiho ne uspije; broj ostaje označiv mišem |
| Offline | Modal radi u cijelosti — podaci su lokalna konstanta |
| Neprijavljen korisnik | Prefill bez imena i organizacije, generičan subject |
| Dugačko ime organizacije | Red se skraćuje s `truncate`, ne lomi layout |

## Out of scope (v1)

- Uslovi korištenja i politika privatnosti (zasebna tema, D7)
- Telegram, Facebook, Instagram, adresa, mapa
- Blok "najčešća pitanja" prije kontakta
- Prikaz verzije aplikacije i dugme za dijagnostiku
- Status smetnji

## Phases

Jedna faza. Feature je premalen da se dijeli, a polovična isporuka (npr. samo u aplikaciji, bez
login ekrana) ne bi riješila glavni problem zbog kojeg spec postoji.

## Acceptance criteria

- [x] Kontakt modal se otvara s login, register, forgot, reset i verify ekrana
- [x] Otvaranje kontakta **ne briše** upisani email i lozinku na login formi
- [x] Kontakt modal se otvara iz footera na svakoj stranici u aplikaciji
- [x] Kontakt modal se otvara iz profil dropdowna **i** iz mobilnog hamburger panela
- [x] WhatsApp red otvara `https://wa.me/387603209030`
- [x] Viber red otvara `viber://chat?number=%2B387603209030`
- [x] Telefon red otvara `tel:+387603209030`
- [x] Email red otvara `mailto:info@melarium.app` s pripremljenim subjectom
- [x] Svaki red s brojem/adresom ima dugme "Kopiraj" koje potvrđuje kopiranje — *uspješna* putanja
      (ikona ✓ "Kopirano") nije mogla biti provjerena lokalno: preglednik u kojem je testirano odbija
      Clipboard API (`NotAllowedError`) jer prozor nije fokusiran. Provjerena je putanja neuspjeha
- [x] Prijavljenom korisniku prefill sadrži ime, organizaciju, ulogu i stranicu; neprijavljenom ne
- [x] Tekst "odgovaramo u roku od 24 sata" je vidljiv u modalu
- [x] Modal radi u tamnoj temi i na širini telefona
- [x] `noreply@melarium.app` nigdje nije prikazan kao kontakt adresa

## Changed during implementation (2026-08-28)

**Kopiranje je dobilo fallback koji nije bio u nacrtu.** Spec je tražio dugme "Kopiraj" (D4) jer
`viber://` link tiho zakaže. Tokom provjere se pokazalo da i sam Clipboard API zna biti odbijen
(`NotAllowedError` — plain http, neki in-app preglednici, nefokusiran prozor), pa bi `catch {}`
reproducirao **isti tihi kvar zbog kojeg dugme uopšte postoji**. Sada, kad kopiranje padne, handler
označi tekst tog reda i ispiše "Kopiranje nije dozvoljeno u ovom pregledniku — tekst je označen,
kopirajte ga ručno." Ručno kopiranje i dalje radi, i korisnik zna zašto.

**`ContactModal` čisti stanje kad se zatvori.** Dijalog ostaje montiran dok je zatvoren, pa bi bez
toga stara poruka "Kopirano" ili poruka o grešci dočekala korisnika pri sljedećem otvaranju.

## Open questions

- Da li dodati kontakt na dno Help panela ako se pokaže da ga korisnici ne nalaze? (namjerno
  odgođeno, vidi "Namjerno nije uključeno")
