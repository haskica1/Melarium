# SPEC-18 — Spajanje AI Savjetnika u AI Asistenta

| | |
|---|---|
| **Status** | 🔨 U implementaciji (odluke donesene 2026-08-09) |
| **Effort** | M — aditivna nadogradnja postojećeg Asistenta (SPEC-17) plus gašenje Savjetnika (SPEC-01) |
| **Depends on** | SPEC-01 (AI Savjetnik — gasi se), SPEC-17 (AI Asistent — proširuje se) |
| **New secrets / packages** | none |
| **Breaking** | Da, namjerno: `/advisor` ruta i `AdvisorController` nestaju; `PlanUsageDto` i `appsettings.json`'s kvota polja mijenjaju ime |
| **Reserves** | none novo — `PlanFeature.AiAssistant = 5` se oslobađa brisanjem (bio je vestigijalan i prije ovog spec-a) |
| **Rate-limit policy** | none novo — postojeći `ai-chat`/`voice-parse` se i dalje koriste |

> **How this spec was written.** Odluke u §1 su donesene s Asimom 2026-08-09, protiv koda kakav je tog
> dana na `main` (odmah nakon što je SPEC-17 isporučen). Ovaj dokument je **zapis** tih odluka, ne mjesto
> gdje su donesene. Svaka tvrdnja o postojećem kodu ovdje je provjerena direktnim čitanjem tog dana —
> ponovo provjeriti citirane linije prije implementacije ako prođe vrijeme.

---

## 0. Zašto ovo postoji

Melarium danas ima dva odvojena AI-ja. **AI Savjetnik** (`/advisor`, SPEC-01) odgovara na pitanja — čita
podatke košnice, ne piše ništa. **AI Asistent** (`/assistant`, SPEC-17) izvršava naredbe — predlaže DTO,
korisnik potvrdi, postojeći servis piše. Asim želi jedan: AI Asistent, sa Savjetnikom stopljenim unutra.

Ovo pitanje nije novo — **SPEC-17 §D2 ga je već razmotrio i odbio**, a ADR-033's "Alternatives
considered" ponavlja isti stav: *"Merging the assistant into the advisor — rejected: a router guessing
'question or command' is a new failure mode, and a chat bubble is a poor host for an editable form."*
SPEC-17 §12 dodatno izuzima *"read-only questions over data... that is the advisor's job, not a second
answer path."*

Ovaj spec formalno nadjačava sve troje, i evo zašto oba prigovora više ne važe onako kako su važila
2026-08-08:

- **"Router koji nagađa" se ne dešava.** Koverta koju model vraća već ima `reply` (tekst) + `actions`
  (može biti prazna lista) + `needs`. Pitanje bez naredbe prirodno postane koverta s praznim `actions` —
  klasifikacija se dešava **unutar** istog poziva koji i odgovara, ne kao poseban router korak prije
  njega. Ovo nije bilo očigledno prije nego što su Faze A/B/C stvarno izgrađene i pokazale da koverta
  nosi tu nijansu bez posebnog mehanizma.
- **"Chat bubble je loše mjesto za formu" više nije relevantno jer forma nije u bubble-u.**
  `ProposalCard` je zasebna, editabilna kartica ispod odgovora, ne tekst u mjehuriću. Q&A odgovor u
  istom mjehuriću ne remeti taj obrazac — samo ga dopunjava trećim ishodom pored "predloži karticu" i
  "postavi potpitanje".

Ništa se ne mijenja u tome kako Asistent predlaže i izvršava radnje (SPEC-17 Faze A/B/C ostaju
netaknute, ADR-033 i dalje važi u punini). Ovo je aditivna promjena plus gašenje starog ulaza.

---

## 1. Šta je odlučeno

| # | Odluka | |
|---|---|---|
| D1 | **Stara historija Savjetnika** | **Migrira se** u jedinstvenu historiju Asistenta, kao razgovori bez akcija — uz rezervnu kopiju i audit trag prije, po uzoru na uvoz stare baze (`deploy/data-migration/`, 2026-07-26). |
| D2 | **Mjesečna kvota** | **Jedan zajednički limit**, jedan config ključ, jedan mjerač na `/plans`. Kvota se provjerava **prije** poziva AI-u (`AiAssistantService.cs:84,114`), a sistem tek poslije zna da li je poruka bila pitanje ili naredba — dva odvojena brojača bi ionako morala provjeravati oba unaprijed. Startni broj: **30**, koliko Asistent ima danas. |

**Napomena o D2.** Standard paket danas ima 10 (savjetnik) + 30 (asistent) = do 40 kombinovanih AI
poruka mjesečno. Broj 30 je stvarno smanjenje ukupnog obima, ne samo reorganizacija — namjerno je
predložen kao startna vrijednost jer je najlakši za razumjeti na `/plans`, ne zato što je matematički
neutralan. Mijenja se u jednoj liniji `appsettings.json` ako se pokaže preusko.

---

## 2. Arhitektura — šta se stvarno mijenja

**1. Prompt prestaje da deflektuje pitanja, počinje da odgovara na njih.**
`AssistantPromptBuilder.BuildSystem` ([AssistantPromptBuilder.cs](../../backend/Melarium.Application/Features/Assistant/AssistantPromptBuilder.cs))
danas eksplicitno kaže *"Ne daješ savjete — za savjete postoji AI savjetnik"* (linije 21-26) i pravilo 6
(linije 97-98) traži prazan `reply` koji upućuje na savjetnika kad poruka nije naredba. Oba se prepisuju:
uvod objašnjava da Asistent radi oboje, pravilo 6 traži pun, koristan odgovor u `reply` (uz Markdown gdje
pomaže). Sigurnosne ograde iz `AdvisorService.SystemPrompt` (obavezna prijava AFB/EFB veterinaru, "nisam
veterinar", samo bosanski, odbijanje ne-pčelarskih tema) se prenose u novi blok istog prompta.

**2. Bogat kontekst košnice se dovlači uslovno — kad je košnica u fokusu, ne kad su radnje prazne.**
Gejtovanje na "actions je prazan" je tehnički nedostižno bez drugog poziva modelu (dupla latencija baš
na porukama gdje je brzina najbitnija) ili klasifikatora prije poziva (duplira ono što sam poziv već radi
pouzdano). Umjesto toga: isti okidač koji Savjetnik već koristi od SPEC-01 — kontekst se gradi kad je
`beehiveId` prisutan na turnu (stranica na kojoj korisnik stoji, ili sesija već vezana za košnicu, §4).
`AdvisorContextBuilder.Build(...)` je već čista funkcija bez I/O — seli se u
`Features/Assistant/HiveContextBuilder.cs`, tijelo nepromijenjeno.

**3. Novi sigurnosni zahtjev koji ovo uvodi.** Danas je `beehiveId` na turnu bezopasan ako je tuđ —
koristi se samo kao tie-breaker unutar `AiTargetResolver`, provjeren protiv pristupačnog skupa, pa
nepristupačan id jednostavno ne upari ništa. Kad isto polje počne aktivno **dovlačiti tuđe podatke**
(pregledi, napomene, zadaci) u prompt, to je druga vrsta povjerenja — mora se ponoviti obrazac koji
`AdvisorService` već ima: `EnsureCanAccessBeehiveAsync` (baca) pri zasnivanju sesije na košnicu,
`CanAccessBeehiveAsync` (ne baca, tiho ispušta kontekst) na svakom sljedećem turnu.

**4. Kvota se spaja u jedan pre-flight gate koji već postoji.** `EnsurePlanAllowsCommandAsync()` se zove
na vrhu `StartSessionAsync`/`AddTurnAsync`, prije `InterpretAsync` (prije Groq poziva). Spajanje ne
mijenja tu poziciju, samo šta se broji: novi `EnsureAiInteractionAsync` broji sve `AiTurnRole.User`
turnove — nakon spajanja svaki turn, pitanje ili naredba, je upravo to.

---

## 3. Backend

| Fajl | Šta |
|---|---|
| `Features/Assistant/AssistantPromptBuilder.cs` | prepisan uvod + pravilo 6 (odgovaraj, ne deflektuj); nov opcioni `contextBlock` parametar; preneseni sigurnosni guardrails |
| `Features/Assistant/HiveContextBuilder.cs` | **novo** — `AdvisorContextBuilder.cs` preseljen, namespace promijenjen, tijelo nepromijenjeno |
| `Features/Assistant/AiAssistantService.cs` | + `IWeatherService`; metoda za dovlačenje konteksta (po uzoru na `AdvisorService.BuildContextBlockAsync`); pristupne provjere prije gradnje konteksta; `BeehiveId` na sesiji; `EnsurePlanAllowsCommandAsync` → `EnsurePlanAllowsInteractionAsync` |
| `Common/Security/IPlanGuard.cs`, `PlanGuard.cs` | `EnsureAdvisorMessageAsync` + `EnsureAiCommandAsync` → jedan `EnsureAiInteractionAsync` |
| `Common/Security/PlanFeature.cs` | `AiAssistant = 5` briše se (potvrđeno nula poziva) |
| `Features/Plans/DTOs/MyPlanDto.cs` | `PlanUsageDto`: dva para polja → `AiInteractionsThisMonth`/`AiInteractionsLimit` |
| `Melarium.API/appsettings.json` | `Standard`: `AdvisorMessagesPerMonth`/`AiCommandsPerMonth` → `AiInteractionsPerMonth: 30` |
| Migracija (EF) | `AiAssistantSession.BeehiveId` — nov nullable FK, `SetNull`, po uzoru na `AdvisorConversationConfiguration` |
| Brisanje | `AdvisorController.cs`; `Features/Advisor/AdvisorService.cs`, `IAdvisorService.cs`, `DTOs/*`, `Validators/*`; `IAdvisorAiClient.cs`, `GroqAdvisorAiClient.cs`; `AdvisorConversationRepository.cs` + iz `IUnitOfWork` |

`response_format: json_object` i `temperature: 0` na `GroqAssistantAiClient` ostaju nepromijenjeni — JSON
pouzdanost je bitna i na Q&A turnovima.

---

## 4. Frontend

| Fajl | Šta |
|---|---|
| `shared/components/MarkdownMessage.tsx` | **novo** — `ChatThread.tsx`'s `MarkdownMessage` premješten ovamo |
| `features/assistant/AssistantThread.tsx` | asistent-mjehurić koristi `MarkdownMessage`; primjer pitanja u `EXAMPLES`; prošireni empty-state/placeholder; footer-disclaimer (veterinar/AFB-EFB) |
| `features/assistant/AssistantPage.tsx` | `useSearchParams` za `beehiveId` (nedostaje danas), prosljeđuje `contextBeehiveId`, prikazuje 🐝 chip |
| `features/beehives/BeehiveDetailPage.tsx` | link "Pitaj savjetnika" → `/assistant?beehiveId=...` |
| `shared/components/Sidebar.tsx`, `App.tsx` | uklonjena stavka/ruta za `/advisor` |
| `core/help/helpRoutes.ts`, `helpContent.ts` | uklonjeni `/advisor` unosi; prepisan `/assistant` savjet koji danas upućuje na Savjetnika |
| `features/plans/PlansPage.tsx` | dva AI mjerača → jedan |
| `core/models/index.ts` | `PlanUsage` spojen; `beehiveId`/`beehiveName` na sesiji; Advisor interfejsi uklonjeni |
| Brisanje | `features/advisor/` (folder), `core/services/advisorService.ts`, `advisorQueries.ts` |

`ProposalCard.tsx` ne treba izmjenu — Q&A turn prirodno ima nula `AssistantAction` redova, a
`turn.actions.length > 0` provjera u `AssistantThread.tsx` već preskače kartice za njega.

---

## 5. Migracija historije (D1)

Isti obrazac kao `deploy/data-migration/` (uvoz stare baze, 2026-07-26): numerisane SQL skripte,
`pg_dump` prije, verifikacija poslije — u zasebnoj putanji da se ne miješa sa zatvorenim starim uvozom.

- `deploy/data-migration/advisor-merge/01_backfill.sql` — `AdvisorConversations` → `AiAssistantSessions`
  (uključujući `BeehiveId`), `AdvisorMessages` → `AiAssistantTurns` s eksplicitnim mapiranjem role (danas
  se vrijednosti poklapaju, ali `AiTurnRole` je namjerno odvojen od `AdvisorRole` — ne oslanjati se na
  slučajnost). Nula `AiAssistantAction` redova po migriranom turnu — tačno odgovara semantici "prazne
  akcije = bilo je pitanje".
- `02_verify.sql` — broj redova i zbir po korisniku prije/poslije.
- `README.md` u istom duhu kao postojeći.

Na VPS-u prije pokretanja: `pg_dump -Fc` i CSV mapa stari→novi id kao audit trag, isti obrazac kao
`~/melarium-before-import.dump`/`~/melarium-id_map.csv`. Brisanje starih tabela (`AdvisorConversations`/
`AdvisorMessages`) ide u **poseban, kasniji** deploy, tek nakon što je backfill potvrđen na produkciji —
ne spaja se brisanje izvora s kopiranjem u istom koraku.

---

## 6. Redoslijed implementacije

1. Backend Q&A (prompt, `HiveContextBuilder`, uslovni kontekst, pristupne provjere) + testovi.
2. Frontend Markdown + copy.
3. Brisanje Savjetnika + spajanje kvote, u jednom prolazu.
4. `BeehiveId` na sesiji + preusmjeravanje linka + `AssistantPage` wiring + gašenje navigacije/rute.
5. Migracija historije — skripte se pišu ovdje; **pokretanje na produkciji je Asimova radnja**, ne
   nešto što se izvršava tokom implementacije.
6. Dokumentacija.

---

## 7. Testovi

- `AiAssistantServiceTests.cs` — prazan `actions`/bez `needs` renderuje se kao čist odgovor bez kartica;
  kontekst se gradi samo kad je košnica u fokusu; nedostupan `beehiveId` na startu sesije se odbija;
  naknadno oduzet pristup tiho ispušta kontekst; iscrpljena kvota blokira prije Groq poziva bez obzira
  šta bi turn ispao.
- `PlanGuardTests.cs` — `EnsureAiInteractionAsync`: Free blokira, iscrpljena kvota blokira, ispod kvote
  prolazi, odsutan ključ = neograničeno, SystemAdmin bypass.
- Nov `AssistantPromptBuilderTests.cs` — zaključava da je Q&A pravilo prisutno, staro deflektovanje
  nestalo, guardrails prisutni, ubacivanje datuma i dalje radi.
- `AdvisorContextBuilderTests.cs` seli se uz `HiveContextBuilder.cs` — iste asercije, nov namespace.
- `AdvisorServiceTests.cs` briše se; smislene provjere (vlasništvo→404, pad AI-a ne perzistira ništa,
  pristup provjeren prije poziva AI-u) reinkarniraju se u `AiAssistantServiceTests.cs`.

---

## 8. Ishodi razmotreni

| Situacija | Šta se dešava |
|---|---|
| Pitanje bez konteksta ("kad se vrca lipov med?") | Pun Markdown odgovor, nula kartica |
| Pitanje sa strane konkretne košnice | Odgovor koristi stvarne podatke te košnice |
| Naredba (postojeći primjeri iz SPEC-17) | Radi nepromijenjeno — nula regresije |
| Naredba + pitanje u istoj sesiji | Historija prikazuje oboje ispravno |
| Kombinovana kvota iscrpljena | 402 na sljedećoj poruci, pitanje ili naredba svejedno |
| Stari Savjetnik razgovor nakon migracije | Prikazuje se kao običan razgovor bez kartica |
| Ponovno otvaranje stare sesije vezane za košnicu | 🐝 chip i dalje prikazan (BeehiveId na sesiji, ne samo na turnu) |
| Pristup košnici oduzet između turnova | Kontekst se tiho ispušta, turn ne pada |
| Free paket | 402 → postojeći upsell modal, isto za pitanje i naredbu |
| `/advisor` posjećen direktno nakon gašenja | Ruta ne postoji — ponaša se skladno ostatku app-a |

---

## 9. Kriteriji prihvatanja

**Zaključano automatskim testovima:**

- [ ] `AiAssistantServiceTests`, `PlanGuardTests`, `AssistantPromptBuilderTests` pokrivaju svaki slučaj iz §7.
- [ ] Nedostupna košnica nikad ne dovlači kontekst, ni na startu ni na kasnijem turnu.
- [ ] Kombinovana kvota se provjerava prije Groq poziva, bez obzira šta bi turn ispao.
- [ ] `UpdateTodoDto`/`UpdateInspectionDto` i dalje čuvaju svako polje AI ne spomene (SPEC-17 ostaje netaknut — regresija).

**Verifikovano kroz aplikaciju:**

- [ ] Pitanje bez konteksta → pun, Markdown-formatiran odgovor.
- [ ] Pitanje sa strane košnice → odgovor referiše stvarne podatke (zadnji pregled, aktivna prehrana).
- [ ] Oba primjera iz SPEC-17 §0 i dalje rade identično.
- [ ] `/plans` prikazuje jedan AI mjerač, ne dva.
- [ ] `/advisor` uklonjen iz menija; stara "Pitaj savjetnika" veza vodi na `/assistant` s ispravnom košnicom.
- [ ] Migrirani stari razgovori vidljivi u `/assistant` historiji, ispravnih datuma, bez kartica.

**Gotovo:**

- [ ] Dokumentacija: `features/ai-assistant.md`, `api-contracts.md`, `context.md`, `decisions.md`
      (ADR-033 dodatak), `specs/README.md`, `glossary.md`, SPEC-01 i SPEC-17 status linije.

---

## 10. Namjerno van opsega

**Brisanje starih tabela** (`AdvisorConversations`/`AdvisorMessages`) — poseban, kasniji deploy nakon
potvrđenog backfill-a (§5/§6), ne dio ove implementacije. **Promjena kvota broja** (30) — ostaje
podesiva vrijednost, ne ponovo pregovarana ovdje. **Bilo šta iz SPEC-17 §12** — ovaj spec ne proširuje
opseg radnji Asistenta, samo dodaje odgovaranje na pitanja i gasi Savjetnika. **Streaming odgovora,
dijeljenje razgovora između članova organizacije** — nasljeđeno kao van opsega iz SPEC-01, nije ponovo
otvarano ovim spajanjem.
