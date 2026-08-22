# Treća objava — vikend, 22.–24. august 2026.

**Mreže:** Facebook + Instagram (isti sadržaj)
**Cilj:** klik na link → registracija → isprobavanje
**Tema:** Evidencija tretmana protiv varoe (`/treatments`)
**Status:** prijedlog za odobrenje — sve tvrdnje provjerene u kodu (izvori na dnu)

---

## 1. Šta je stvarno implementirano (provjera koda)

### 1.1 Potpuno završene funkcije

| Funkcija | Ruta | Izvor u kodu |
|---|---|---|
| Pčelinjaci (CRUD) | `/apiaries` | `ApiariesController.cs`, `features/apiaries/` |
| Košnice (CRUD, QR kod) | `/beehives/:id`, `/scan/:uniqueId` | `BeehivesController.cs`, `features/beehives/` |
| Pregledi košnica | `/inspections/new` | `InspectionsController.cs` |
| Pregledi offline (outbox) | `/outbox` | `core/offline/outbox.ts` (IndexedDB) |
| **Tretmani + PDF registar** | `/treatments` | `TreatmentsController.cs`, `features/treatments/` |
| Prehrana / programi | `/feedings` | `DietsController.cs`, `features/diets/` |
| Vrcanja | `/harvests` | `HarvestsController.cs` |
| Troškovi + skeniranje računa | `/expenses`, `/expenses/scan` | `ExpensesController.cs` |
| Kalendar + iCal feed | `/calendar` | `CalendarController.cs`, `CalendarFeedService.cs` |
| Statistika | `/stats` | `StatsController.cs` |
| Edukacija | `/learning` | `LearningTopicsController.cs` |
| Matice | (u detalju košnice) | `QueensController.cs` |
| Članovi + dodjela košnica | `/members` | `OrgManagementController.cs` |
| Notifikacije (in-app + email) | — | `NotificationsController.cs`, `AlertScanWorker.cs` |
| Pozivnice / preporuke | `/invite` | `InvitesController.cs` |
| PWA (instalacija na telefon) | — | `vite.config.ts` → `VitePWA` |

### 1.2 Besplatno vs. iza pretplate (provjereno, ne pretpostavljeno)

Gate se radi kroz `PlanFeature` enum i `PlanGuard`. **Samo ove četiri funkcije + AI asistent su plaćene:**

| Funkcija | Minimalni paket | Izvor |
|---|---|---|
| Pašnjaci i selidbe | Standard | `PlanFeature.Pastures` |
| Glasovni unos pregleda | Standard | `PlanFeature.VoiceInput` |
| Sedmični AI sažetak | Standard | `PlanFeature.WeeklySummary` |
| AI asistent (poruke) | Standard | `PlanGuard.EnsureAiInteractionAsync` |
| AI analiza fotografije okvira | Pro | `PlanFeature.PhotoAnalysis` |

**Sve ostalo radi i na Free paketu** — uključujući tretmane, preglede, prehranu, vrcanja, kalendar, statistiku, PDF registre i notifikacije. Provjereno: `TreatmentService.cs` **nema nijedan poziv** `IPlanGuard`-a.

Limiti po paketima (`appsettings.json` → `Plans`):

- **Free:** 1 pčelinjak, 7 košnica, 0 dodatnih članova
- **Standard:** neograničeno pčelinjaka, 30 košnica, 2 člana
- **Pro:** 100 košnica, 5 članova
- **Probni period: 30 dana Pro** za svaku novu registraciju (60 dana ako dolaziš preko pozivnice)

Cijene (izvor: `frontend/src/core/services/planService.ts:9-15`): Free 0 KM · Standard 20 KM/mj (200 KM/god) · Pro 35 KM/mj (350/god) · Max 50 KM/mj (500/god).
**Napomena:** nadogradnja se trenutno radi ručno preko e-maila (`UPGRADE_EMAIL`) — nema online plaćanja u kodu. Zato u objavi ne spominjem cijene ni plaćanje.

### 1.3 NE SPOMINJATI u objavi

- **Demo/test nalog** — ne postoji javno. Seed nalozi (`@beehive.com`, `@goldenhive.com`) postoje samo u Development modu i **zaključavaju se na produkciji** (`DatabaseInitializer.LockDemoAccountsAsync`). Jedini put unutra je registracija.
- **Offline unos tretmana** — outbox je **samo za preglede** (`outbox.ts`: „Offline outbox for inspections"). Tretman traži konekciju.
- **AI asistent kao besplatan** — Free paket dobija 402.
- **Pašnjaci, glasovni unos, foto-analiza** — plaćeno, ne spominjati u objavi o besplatnom.
- **„Čuvanje evidencije 5 godina"** — ta rečenica postoji samo kao tekst u aplikaciji, bez izvora propisa u repozitoriju. Ne tvrditi kao zakonsku činjenicu.
- Nema TODO/FIXME markera u kodu (0 pogodaka) i nema isključenih feature flagova — ništa nije „napola".

---

## 2. Izbor teme: **Evidencija tretmana protiv varoe**

**Zašto baš ona:**

1. **Aktuelna je upravo sada.** Kraj augusta u BiH = tretman protiv varoe odmah nakon zadnjeg vrcanja. To je posao koji pčelari rade ovog vikenda, ne nešto o čemu će razmišljati za tri mjeseca.
2. **Rješava stvarnu muku, a ne „lakše vođenje evidencije".** Karenca, LOT broj, koje košnice su tretirane, kad vaditi trake — to su stvari koje se zaista zaboravljaju i koje inspekcija zaista traži.
3. **Besplatna je.** Nema gatea — čovjek se registruje i odmah je koristi. CTA je pošten.
4. **Dobro izgleda na screenshotu.** Obojeni statusi (žuto „U toku", crveno „Karenca do…", sivo „Završen"), kartice s brojevima, dugme za PDF.
5. **Nije pokrivena prvim dvjema objavama.** Prva je bila opšti opis, druga video pregled sadržaja. Ova ide u dubinu jedne funkcije — drugi tip objave, ne ponavljanje.

**Šta konkretno aplikacija radi (sve provjereno u kodu):**

- Gotovi predlošci za preparate: **Apivar, Bayvarol, Apiguard, oksalna kiselina (nakapavanje i sublimacija), mravlja kiselina** — odabir popuni aktivnu tvar, način primjene i dozu
- Označiš košnice — dugme **„Označi sve"** za cijeli pčelinjak odjednom
- Odstupanje doze po pojedinoj košnici (opcionalno)
- **Karenca:** upišeš koliko dana traži preparat, aplikacija sama računa datum do kojeg se ne smije vrcati i drži status
- Status ide sam: **U toku → Karenca → Završen**
- Više primjena u serijama (npr. Apiguard 2×) — broj primjena i razmak u danima, svaka se čekira posebno
- **LOT broj i dobavljač** — zbog sljedivosti lijeka
- **PDF registar** po pčelinjaku i godini (A4 landscape, sa svim kolonama)
- **Automatska upozorenja:** „Trake za uklanjanje" (nakon 42 dana), „Istekla karenca — med se ponovo smije vrcati", „Primjena tretmana kasni"
- Rundе tretmana se pojavljuju u **kalendaru**

---

## 3. Tri prijedloga objave

---

### VARIJANTA A — problem → rješenje

**Hook:** „Koliko dana su ti trake u košnici? Ako moraš razmišljati — to je već problem."

#### Instagram (~140 riječi)

> Koliko dana su ti trake u košnici? Ako moraš razmišljati — to je već problem. 🐝
>
> Amitraz koji predugo stoji ne ubija više varou. Uči je da preživi.
>
> A onda dođe i ono drugo pitanje: kad opet smijem vrcati? Karenca se računa od dana vađenja, ne od dana stavljanja. Ko to još drži u glavi za 20 košnica?
>
> Melarium to vodi umjesto tebe:
> 📋 upišeš preparat (Apivar, Apiguard, oksalna, mravlja — već su unutra)
> ✅ označiš košnice — ili sve odjednom
> ⏳ status sam ide: U toku → Karenca → Završen
> 🔔 javi ti kad trake stoje predugo
> 🔔 javi ti kad karenca istekne
> 📄 izvezeš PDF registar kad zatreba
>
> Besplatno. Bosanski. Radi na telefonu.
>
> 👉 www.melarium.app

**Hashtagovi IG (13):**
`#pčelarstvo #pčelari #pčelinjak #košnice #varoa #tretmanprotivvaroe #med #medonosnapčela #pčelarstvobih #pčelaribih #pčelarenje #apikultura #melarium`

#### Facebook (~230 riječi)

> Koliko dana su ti trake u košnici? Ako moraš razmišljati — to je već problem.
>
> Svako od nas zna kako ide. Izvrcaš zadnji med, staviš trake, i onda krene septembar, pa prihrana, pa još sto stvari. Trake ostanu unutra duže nego što piše na uputstvu. Varoa se navikne na aktivnu tvar i sljedeće godine ti isti preparat više ne radi.
>
> I drugo pitanje koje se uvijek pojavi: kad opet smijem vrcati? Karenca teče od dana kad si trake izvadio, ne od dana kad si ih stavio. Za pet košnica se to i zapamti. Za dvadeset — ne.
>
> Zato u Melariumu tretmani nisu obična bilježnica.
>
> Upišeš preparat — Apivar, Bayvarol, Apiguard, oksalna, mravlja kiselina su već pripremljeni, odabir sam popuni aktivnu tvar i način primjene. Označiš košnice na kojima si tretirao, ili klikneš „Označi sve" za cijeli pčelinjak. Upišeš LOT broj s pakovanja.
>
> Dalje aplikacija radi sama:
>
> • računa do kad traje karenca i drži košnicu u tom statusu
> • javi ti kad trake predugo stoje u košnici
> • javi ti kad karenca istekne — med se opet smije vrcati
> • ako preparat traži dvije primjene, upiše obje u kalendar
> • izvezeš PDF registar po pčelinjaku i godini kad ti zatreba
>
> Sve ovo radi i na besplatnom paketu. Ne treba kartica, ne treba instalacija — otvoriš u pregledniku i radi.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #varoa #melarium`

**Vizual:**
- **Ekran:** lista tretmana s obojenim statusima
- **Fajl:** `frontend/src/features/treatments/TreatmentsPage.tsx`
- **Ruta:** `/treatments`
- **Šta mora biti u kadru:** kartice na vrhu (Tretmani / Aktivni-karenca / Pčelinjaci / Registar PDF) + bar 3 tretmana s različitim statusima — jedan žuti „U toku", jedan crveni „Karenca do 12.09.2026." i jedan sivi „Završen". Naziv pčelinjaka i dugme „Preuzmi evidenciju (PDF)" da se vide.
- **Priprema:** unesi 3 test tretmana s različitim datumima da se dobiju sva tri statusa. Mobilni portret kadar izgleda bolje nego desktop.

**Alt tekst:**
> Ekran aplikacije Melarium s listom tretmana protiv varoe. Prikazane su tri stavke: Apivar sa statusom „U toku", Apiguard sa crvenom oznakom „Karenca do 12.09.2026." i oksalna kiselina sa statusom „Završen". Iznad liste su kartice s ukupnim brojem tretmana i dugme za preuzimanje PDF registra.

---

### VARIJANTA B — scenarij iz prakse

**Hook:** „Subota, 7 ujutro. Dvadeset košnica, kanta s Apiguardom i jedna olovka koja ne piše."

#### Instagram (~145 riječi)

> Subota, 7 ujutro. Dvadeset košnica, kanta s Apiguardom i jedna olovka koja ne piše. 🐝
>
> Znaš kako to ide dalje. Tretiraš, u glavi držiš koja je preskočena jer je bila preslaba, i obećaš sebi da ćeš uveče sve prepisati u svesku.
>
> Uveče ne prepišeš.
>
> Ovako to izgleda s telefonom u džepu:
>
> 📱 Novi tretman → odabereš „Apiguard (timol, isparavanje)"
> ⚡ doza i način se popune sami
> ✅ „Označi sve" — pa skineš kvačicu s one dvije slabe
> 🔢 broj primjena: 2, razmak 14 dana
> 💾 spremiš
>
> Druga primjena ti je već u kalendaru. Za 14 dana stigne podsjetnik. Kad je označiš obavljenom, karenca počne teći sama.
>
> Trideset sekundi kod košnice. Bez sveske koja se smoči.
>
> 👉 www.melarium.app

**Hashtagovi IG (14):**
`#pčelarstvo #pčelari #pčelinjak #košnice #varoa #apiguard #jesenjitretman #med #pčelarstvobih #pčelaribih #pčelarenje #apikultura #pčelarskidnevnik #melarium`

#### Facebook (~240 riječi)

> Subota, 7 ujutro. Dvadeset košnica, kanta s Apiguardom i jedna olovka koja ne piše.
>
> Znaš kako ide dalje. Otvaraš redom, stavljaš posudice, u glavi držiš da si preskočio broj 7 i broj 14 jer su preslabe. Kažeš sebi: uveče ću sve lijepo prepisati u svesku.
>
> Uveče dođeš kući, večera, umor — i ne prepišeš. Za dvije sedmice se pitaš je li onaj drugi tretman trebao biti u srijedu ili u petak, i jesi li ono 7 i 14 ipak tretirao.
>
> Evo kako isti taj pregled izgleda s telefonom u džepu.
>
> Otvoriš Melarium, „Novi tretman". Iz spiska odabereš „Apiguard (timol, isparavanje)" — doza, aktivna tvar i način primjene se popune sami. Klikneš „Označi sve" pa skineš kvačicu s one dvije slabe. Broj primjena: 2. Razmak: 14 dana. LOT broj prepišeš s pakovanja. Spremiš.
>
> Gotovo. Trideset sekundi, dok stojiš pored košnice.
>
> Šta se desi dalje bez tebe:
>
> • druga primjena ti je odmah u kalendaru
> • za 14 dana dobiješ podsjetnik da je označiš obavljenom
> • ako je ne označiš, aplikacija te podsjeti ponovo
> • kad završiš, karenca počne teći sama i javi ti kad istekne
> • na kraju sezone izvezeš PDF registar po pčelinjaku
>
> Sveska se smoči, olovka ne piše, telefon ti je ionako u džepu.
>
> Radi besplatno, na bosanskom, i može se instalirati na početni ekran kao aplikacija.
>
> 👉 www.melarium.app

**Hashtagovi FB (5):**
`#pčelarstvo #pčelaribih #varoa #pčelarskidnevnik #melarium`

**Vizual:**
- **Ekran:** forma za novi tretman
- **Fajl:** `frontend/src/features/treatments/TreatmentFormPage.tsx`
- **Ruta:** `/treatments/new`
- **Šta mora biti u kadru:** polje „Brzi odabir preparata" s odabranim Apiguardom, popunjena polja ispod (preparat, doza), sekcija „Broj primjena" = 2 i „Razmak između primjena" = 14, te lista košnica s kvačicama i vidljivim dugmetom „Označi sve". Dolje treba da se vidi „Odabrano: 18 od 20".
- **Alternativa (ako forma bude preduga za jedan kadar):** dva kadra u karusel — prvi forma, drugi `/treatments/:id` (`TreatmentDetailPage.tsx`) sa sekcijom „Predstojeće primjene".

**Alt tekst:**
> Forma za unos novog tretmana u aplikaciji Melarium. U polju za brzi odabir preparata izabran je Apiguard, ispod su automatski popunjeni doza i način primjene. Podešeno je dvije primjene s razmakom od 14 dana. Na dnu je lista košnica s označenim kvačicama i brojačem „Odabrano: 18 od 20".

---

### VARIJANTA C — direktan poziv (CTA-first)

**Hook:** „Tretiraš ovaj vikend? Upiši to za 30 sekundi — i zaboravi."

#### Instagram (~95 riječi)

> Tretiraš ovaj vikend? Upiši to za 30 sekundi — i zaboravi. 🐝
>
> Melarium ti dalje sam vodi računa:
>
> ⏳ do kad traje karenca
> 🔔 kad vaditi trake
> 📅 kad je druga primjena
> 📄 PDF registar kad ga neko zatraži
>
> Apivar, Bayvarol, Apiguard, oksalna, mravlja — svi su već u spisku.
>
> Besplatno. Na bosanskom. Bez instalacije.
>
> Otvori, registruj se, unesi prvi tretman dok ti je još svjež u glavi.
>
> 👉 www.melarium.app

**Hashtagovi IG (12):**
`#pčelarstvo #pčelari #pčelinjak #košnice #varoa #med #pčelarstvobih #pčelaribih #pčelarenje #apikultura #pčelarskidnevnik #melarium`

#### Facebook (~180 riječi)

> Tretiraš ovaj vikend? Upiši to za 30 sekundi — i zaboravi.
>
> Melarium je pčelarski dnevnik na bosanskom. Za tretmane radi ovo:
>
> • preparat biraš iz spiska — Apivar, Bayvarol, Apiguard, oksalna kiselina, mravlja kiselina
> • označiš košnice, ili sve odjednom
> • upišeš LOT broj i karencu s pakovanja
> • aplikacija sama računa do kad se ne smije vrcati
> • javi ti kad trake predugo stoje u košnici
> • javi ti kad karenca istekne
> • ako preparat traži više primjena, upiše ih u kalendar i podsjeti te
> • PDF registar po pčelinjaku i godini — jedan klik
>
> Ne treba ti instalacija iz prodavnice aplikacija. Otvoriš u pregledniku telefona i radi; možeš ga dodati na početni ekran ako hoćeš.
>
> Tretmani i sve ostale evidencije rade i na besplatnom paketu.
>
> Ako ovaj vikend ideš na pčelinjak — otvori ga prije nego kreneš i unesi prvi tretman na licu mjesta.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #varoa #melarium`

**Vizual:**
- **Ekran:** detalj jednog tretmana sa aktivnom karencom
- **Fajl:** `frontend/src/features/treatments/TreatmentDetailPage.tsx`
- **Ruta:** `/treatments/:id`
- **Šta mora biti u kadru:** naslov preparata s crvenom oznakom „Karenca do …" odmah pored, kartice ispod (uključujući karticu „Karenca / povlačenje") i sekcija „Predstojeće primjene" s bar jednom nezavršenom rundom i dugmetom „Označi obavljeno".
- **Priprema:** treba tretman koji ima `endDate` i `withdrawalDays > 0` da bi se status prikazao kao „Karenca", plus `totalRounds` = 2 da bi sekcija predstojećih primjena imala sadržaj.

**Alt tekst:**
> Detaljni prikaz tretmana u aplikaciji Melarium. Na vrhu je naziv preparata Apiguard s crvenom oznakom „Karenca do 12.09.2026.". Ispod su kartice s brojem tretiranih košnica i trajanjem karence, a na dnu sekcija „Predstojeće primjene" s drugom primjenom zakazanom za 05.09.2026. i dugmetom „Označi obavljeno".

---

## 4. Preporuka

**Varijanta B** za vikend. Scenarij „subota, 7 ujutro" je najbliži onome što ljudi taj dan zaista rade, a screenshot forme pokazuje koliko je unos kratak — to je glavni otpor kod pčelara koji nikad nisu vodili digitalnu evidenciju („trajaće mi duže nego sveska"). Varijanta A je dobra rezerva ako želiš oštriji ton. Varijanta C je prekratka da nosi samostalnu objavu — bolje je iskoristi kao Story ili kao drugu objavu u sedmici.

---

## 5. Šta je potvrđeno u kodu

| Tvrdnja iz objava | Gdje je potvrđeno |
|---|---|
| Predlošci: Apivar, Bayvarol, Apiguard, oksalna (nakapavanje + sublimacija), mravlja kiselina | `frontend/src/features/treatments/presets.ts:17-24` |
| Predložak popuni aktivnu tvar, način i dozu | `TreatmentFormPage.tsx:131` (`handlePresetChange`) |
| „Označi sve" za sve košnice pčelinjaka | `TreatmentFormPage.tsx:356-358` |
| Brojač „Odabrano: X od Y" | `TreatmentFormPage.tsx:402-404` |
| Odstupanje doze po košnici | `TreatmentFormPage.tsx:389-396` |
| Karenca se računa: `(endDate ?? startDate) + withdrawalDays` | `backend/Melarium.Domain/Common/TreatmentStatusHelper.cs:12-13` |
| Status U toku → Karenca → Završen, izveden iz datuma | `TreatmentStatusHelper.cs:20-27`, `models/index.ts:1004-1014` |
| Više primjena (1–10) s razmakom (1–90 dana) | `TreatmentFormPage.tsx:180-198` |
| Runda se čekira pojedinačno („Označi obavljeno") | `TreatmentsController.cs` → `POST /{id}/rounds/{roundId}/complete`; `TreatmentDetailPage.tsx:121-151` |
| LOT broj i dobavljač | `TreatmentFormPage.tsx:442-448` |
| PDF registar po pčelinjaku i godini | `frontend/src/shared/utils/treatmentPdf.ts` (jsPDF, A4 landscape) |
| PDF ima kolone LOT, dobavljač, karenca, košnice | `treatmentPdf.ts:20-34` |
| Upozorenje „Trake za uklanjanje" nakon 42 dana | `AlertRuleService.cs:165-173`; prag `Alerts:StripRemovalDays` = 42 |
| Upozorenje „Istekla karenca — med se ponovo smije vrcati" | `AlertRuleService.cs:175-183` |
| Upozorenje „Primjena tretmana kasni" (2 dana) | `AlertRuleService.cs:190-213`; `appsettings.json:58` |
| Upozorenja stvarno rade (background worker) | `Melarium.Infrastructure/DependencyInjection.cs:35` → `AlertScanWorker` |
| Runde tretmana ulaze u kalendar | `CalendarObligationService.cs:119-167` |
| Karenca vidljiva na kartici košnice | `HiveTreatmentCard.tsx:57-75` |
| Tretmani nisu iza pretplate | `TreatmentService.cs` — nijedan poziv `IPlanGuard`; `PlanFeature.cs` nema stavku za tretmane |
| Free: 1 pčelinjak, 7 košnica | `appsettings.json:89` |
| 30 dana Pro probno pri registraciji | `AuthService.cs:113-121` (`Plan = PlanType.Pro`, `PlanNotes = "Probni period"`) |
| 60 dana preko pozivnice | `InvitationService.cs:304-305` |
| PWA — instalacija na početni ekran, radi u pregledniku | `frontend/vite.config.ts:8-20` (`VitePWA`, `display: standalone`) |
| UI na bosanskom | cijeli `frontend/src` (labeli, `helpContent.ts`) |
| Cijene 20/35/50 KM | `frontend/src/core/services/planService.ts:9-15` — **nisu korištene u objavama** |

---

## 6. Šta NISAM mogao potvrditi — provjeri ručno

1. **Živa aplikacija nije bila dostupna iz ovog okruženja.** `https://melarium.app` vraća 403 na mrežnom gatewayu ove sesije, pa se nisam mogao prijaviti s nalogom `testorg@melarium.com`. **Sve gore je iz koda na grani `main`, ne s produkcije.** Prije objave provjeri da je verzija na produkciji ista — posebno da su tretmani i PDF izvoz živi.

2. **Nesklad oko AI poruka (nije u objavi, ali popravi).** Frontend u tabeli paketa piše da Standard ima **10 poruka/mjesec** (`PlansPage.tsx:24`), a backend konfiguracija dozvoljava **30** (`appsettings.json:90`, `AiInteractionsPerMonth`). Jedno od to dvoje je pogrešno prema korisniku.

3. **„Zakonska obaveza čuvanja evidencije 5 godina".** Ta rečenica stoji u aplikaciji (`TreatmentsPage.tsx:197`, `TreatmentDetailPage.tsx:351`), ali u repozitoriju nema izvora propisa. Namjerno je nisam stavio u objavu. Ako je želiš koristiti, provjeri kod veterinarske inspekcije / nadležnog ministarstva.

4. **Da li PDF registar zadovoljava formu koju inspekcija traži.** Kod generiše uredan registar, ali da li je *prihvatljiv* nadležnima nije stvar koda. U objavama sam pisao samo „izvezeš PDF registar", bez tvrdnje da je zvanično priznat obrazac — nemoj to pojačavati u komentarima.

5. **Karenca 0 u predlošcima.** Svi predlošci imaju `withdrawalDays: 0` uz komentar da je karenca 0 za te registrovane preparate i da korisnik prilagodi ako mu deklaracija kaže drugačije. Zato u objavama stoji „upišeš karencu s pakovanja", a ne „aplikacija zna karencu svakog preparata". Zadrži tu formulaciju.

6. **E-mail notifikacije zavise od `Smtp:Password` na produkciji.** Ako Resend ključ nije postavljen, e-mailovi se tiho preskaču (in-app notifikacije i dalje rade). Provjeri da produkcija šalje e-mail prije nego u objavi naglašavaš „javi ti".

7. **Screenshot treba pripremiti s realnim podacima.** Nemoj slikati prazan ekran ili očigledno lažne nazive („Test 1", „aaa"). Trebaju ti tretmani s tri različita statusa — vidi pripremu kod svake varijante.

8. **Ne objavljuj podatke test naloga.** `testorg@melarium.com` nije demo nalog iz koda nego običan nalog na produkciji; javno dijeljenje pristupa bi značilo da svako može mijenjati te podatke.
