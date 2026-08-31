# Pravne stranice — politika privatnosti i uslovi korištenja

> Status: ✅ Implemented (2026-08-31). Izdvojeno iz [SPEC-23](../specs/SPEC-23-mobile-apps.md).

Dvije javne stranice: `/privatnost` i `/uslovi`. Do sada aplikacija nije imala nijednu.

## Zajednički okvir

Obje koriste `features/legal/LegalPage.tsx` — jedan omotač s povratnim linkom, zaglavljem, datumom
posljednje izmjene i podnožjem. Podnožje **unakrsno linkuje drugu stranicu**, a trenutnu izostavlja
(`useLocation`), pa se linkovi ne moraju održavati ručno po stranicama.

Razlog za zajednički okvir nije ušteda koda nego to što druga kopija prestane odgovarati prvoj.

## Zašto su javne i izvan `Layout`-a

Rute stoje među javnim rutama u `App.tsx`, izvan `<ProtectedRoute>` i izvan `Layout`-a. Ne zbog
stila nego zbog toga ko ih otvara: prodavnice aplikacija traže URL koji **same** mogu otvoriti, bez
naloga, a otvara ih i čovjek koji tek odlučuje hoće li se registrovati.

Linkovane su s **prijave** (oba linka u podnožju) i s **registracije** ("Registracijom prihvatate
uslove korištenja i politiku privatnosti") — tu se donosi odluka, pa tu tekst mora biti čitljiv.

Nijedna ne radi ijedan API poziv; obje su statične i rade bez backenda.

## Politika privatnosti — revizija koda, ne šablon

Svaka tvrdnja provjerena je u kodu prije nego je napisana. Treće strane su one koje backend i
frontend zaista zovu:

| Servis | Šta dobija | Odakle u kodu |
|---|---|---|
| Groq | tekst naredbi, glasovne snimke, fotografije pregleda i računa | `GroqAssistantAiClient`, `GroqTranscriptionService`, `GroqPhotoAnalysisAiClient`, `GroqHiveNumberOcrClient`, `VoiceParsingService`, `WeeklySummaryService` |
| Resend | e-pošta i sadržaj poruke | `Smtp` konfiguracija, `EmailNotificationWorker` |
| Open-Meteo | koordinate pčelinjaka | `WeatherService` |
| OpenStreetMap | IP uređaja (pločice se učitavaju iz preglednika) | `PasturesPage`, `LocationPickerModal` |

Ostale provjerene tvrdnje: nema kolačića ni analitike (sesija je u `localStorage`, ne u kolačiću);
IP se koristi za rate limiting u trenutku zahtjeva i ne sprema se u bazu; `Feedback` sprema
`UserAgent` i `PageContext`; lozinke su BCrypt, tokeni SHA-256 sažeci; pristupni token traje 30
minuta, obnova 14 dana.

**Zato se stranica mijenja zajedno s kodom.** Politika koja opisuje stariju verziju aplikacije gora
je nego da je nema, a najbrže zastarijeva tabela trećih strana. Kad se doda ili ukloni vanjski
servis, mijenja se i ova stranica i njen `LAST_UPDATED`.

## Uslovi korištenja — dvije tačke koje nisu šablon

**§7 AI asistent nije stručni savjet.** Ovo je jedina tačka napisana zbog konkretne štete: AI može
predložiti tretman, a pčelar koji ga slijedi naslijepo može zagaditi med ili prekršiti karencu. Tekst
izričito traži provjeru doza i karence prije postupanja i podsjeća da asistent uvijek pokaže šta je
razumio prije nego išta upiše.

**§8 Registar tretmana ostaje korisnikova zakonska obaveza.** Melarium je mjesto gdje se evidencija
drži, ne onaj ko za nju odgovara. Uz preporuku da se PDF povremeno preuzme izvan aplikacije, i
upozorenje da brisanje organizacije briše i registar — isto ono što potvrda u
[account-deletion.md](account-deletion.md) kaže.

**Ograničenja paketa nisu prepisana kao brojevi.** Žive u `Plans:` u `appsettings.json` i prikazana
su na `/plans`; pravna stranica koja ih ponavlja bila bi netačna prvi put kad se limit promijeni.
Zato uslovi opisuju **model** (probni period, ručna godišnja naplata, nema automatske obnove, pad na
besplatni paket bez brisanja podataka) i upućuju na stranicu *Paketi*.

## Voditelj obrade / pružalac usluge

Obje stranice navode **Asima Haskića kao fizičko lice**, uz `info@melarium.app` i telefon. To je
tačno: fizičko lice koje pruža uslugu jeste voditelj obrade, i ne čeka se registracija firme.

**Fizička adresa nije navedena.** Za web je ime + e-pošta + telefon branjivo, ali obje prodavnice
traže adresu, a Google Play je **javno objavljuje** na stranici aplikacije — što za fizičko lice
znači kućnu adresu. Odluka o tome (poštanski pretinac, registrovan obrt, ili kućna adresa) ostaje
uz SPEC-23 i mora biti donesena prije objave.

## Šta još fali

- Fizička adresa (gore).
- **Javna stranica za zahtjev za brisanje računa** — Google je traži za one koji su aplikaciju već
  deinstalirali. Ostaje uz SPEC-23, jer prije objave na Play-u nema kome da služi. Brisanje računa
  **iz** aplikacije postoji, vidi [account-deletion.md](account-deletion.md).
