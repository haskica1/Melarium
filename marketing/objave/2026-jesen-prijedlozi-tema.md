# Prijedlozi tema za objave — jesen/zima 2026.

**Nastavak na:** `2026-08-22-vikend-objava.md` (tema: tretmani protiv varoe)
**Sve tvrdnje provjerene u kodu** — izvori u sekciji 9. Ograničenja i stvari za ručnu provjeru u sekciji 10.

---

## 0. Prijedlog rasporeda

| # | Tema | Ruta | Kada objaviti | Zašto tada |
|---|---|---|---|---|
| **1** | **Prehrana i spremanje za zimu** | `/feedings` | **početak septembra** | Prihrana ide odmah nakon tretmana — logičan nastavak objave o varoi |
| 2 | Mraz i prognoza po pčelinjaku | `/apiaries/:id` | sredina oktobra | Prvi mrazevi, pitanje utopljenosti |
| 3 | Šta te je koštala prihrana (skeniranje računa) | `/expenses/scan` | kraj oktobra | Kad su računi za šećer još svježi |
| 4 | Sezona u brojkama | `/stats` | novembar | Sezona gotova, vrijeme za sabiranje |
| 5 | Obaveze u telefonskom kalendaru | `/calendar/settings` | decembar | Mirno doba — ljudi imaju vremena podesiti |
| 6 | QR kod na košnici | `/scan/:uniqueId` | januar/februar | Zimska priprema opreme |
| 7 | Pregled bez signala (offline) | `/outbox` | mart | Počinju pregledi |
| 8 | Koliko je stara tvoja matica | detalj košnice | **mart** (ne ranije) | Alarm za staru maticu radi **samo u martu** — vidi 10.4 |

Tema 1 je razrađena u tri varijante. Teme 2–8 imaju po jednu gotovu objavu.

---

# TEMA 1 — Prehrana i spremanje za zimu

**Ruta:** `/feedings` · **Besplatno:** da (`DietService.cs` nema `IPlanGuard`)

## Šta aplikacija stvarno radi

- **Razlog „Zimsko hranjenje"** je ugrađena stavka (`DietReason.WinterFeeding`)
- **Vrsta hrane:** šećerni sirup, fondan, polen, proteinski kolači, ili vlastito
- **Količina po košnici** + jedinica (L, ml, kg, g) i napomena (npr. „1:1", „pola pogače")
- **Program sam generiše runde:** upišeš trajanje i frekvenciju, aplikacija izračuna koliko rundi ide i kad — forma to pokazuje uživo dok kucaš
- Svaka runda se **čekira posebno** kad je obaviš
- **Podsjetnik „Hranjenje kasni"** ako runda kasni 2 dana
- **Upozorenje „Opada nivo meda — razmisli o prehrani"** kad nivo meda padne između dva pregleda
- **Upozorenje „Najavljen mraz … Provjeri prehranu i utopljenost"** iz stvarne prognoze
- **Trošak prihrane po košnici** — kad račun za šećer povežeš s programom
- Runde se pojavljuju u **kalendaru**

**Ne tvrditi:** aplikacija ne mjeri težinu košnice niti računa zalihe u kilogramima. Nivo meda je procjena u tri stepena (nisko/srednje/visoko) koju sam upisuješ. Također — **datum početka ne može biti u budućnosti**, program se pokreće danas i dalje se sam raspoređuje.

---

## VARIJANTA A — problem → rješenje

**Hook:** „Prihranio si ih u septembru. Znaš li tačno koliko je koja košnica dobila?"

### Instagram (~140 riječi)

> Prihranio si ih u septembru. Znaš li tačno koliko je koja košnica dobila? 🐝
>
> Uvijek ista priča. Prve dvije doze ideš uredno. Onda padne kiša, preskočiš sedmicu, i u novembru se pitaš je li ono društvo na kraju reda uopšte dobilo treću turu.
>
> Do proljeća saznaš. Tad je kasno.
>
> U Melariumu prihrana je program, ne bilješka:
> 🍯 odabereš „Zimsko hranjenje" i vrstu hrane
> ⚖️ upišeš koliko ide po košnici
> 📅 upišeš trajanje i razmak — runde se generišu same
> ✅ čekiraš svaku kad je obaviš
> 🔔 ako runda kasni, aplikacija te podsjeti
> ❄️ kad se najavi mraz, javi ti da provjeriš prehranu i utopljenost
>
> Vidiš tačno koja je košnica gdje stala.
>
> Besplatno, na bosanskom.
>
> 👉 www.melarium.app

**Hashtagovi IG (14):**
`#pčelarstvo #pčelari #pčelinjak #košnice #prihrana #zimskaprihrana #pripremazazimu #šećernisirup #med #pčelarstvobih #pčelaribih #pčelarenje #apikultura #melarium`

### Facebook (~240 riječi)

> Prihranio si ih u septembru. Znaš li tačno koliko je koja košnica dobila?
>
> Uvijek ide isto. Prve dvije doze podijeliš uredno, sve po redu. Onda padne kiša, preskočiš sedmicu, dođe posao — i u novembru se pitaš je li ono društvo na kraju reda uopšte dobilo treću turu. Ili si ga preskočio jer je tog dana bilo ljuto pa si rekao „vratiću se".
>
> Odgovor dobiješ u martu, kad otvoriš poklopac. Tad više ne možeš ništa.
>
> U Melariumu prihrana nije jedna bilješka nego program.
>
> Odabereš razlog — „Zimsko hranjenje" je već u spisku. Odabereš vrstu hrane: šećerni sirup, fondan, polen ili proteinski kolači. Upišeš koliko ide po košnici i u čemu mjeriš — litre ili kilogrami. Upišeš koliko dana program traje i na koliko dana ponavljaš.
>
> Aplikacija onda sama izračuna koliko rundi ide i kojim datumima. Vidiš to odmah dok kucaš, prije nego spremiš.
>
> Dalje samo čekiraš svaku rundu kad je obaviš. Šta dobiješ:
>
> • tačan spisak koja je košnica dobila koju turu
> • podsjetnik ako runda kasni dva dana
> • upozorenje kad nekoj košnici padne nivo meda između dva pregleda
> • upozorenje kad se najavi mraz — da provjeriš prehranu i utopljenost
> • runde ti se pojave u kalendaru
> • ako povežeš račun za šećer, vidiš i koliko te prihrana košta po košnici
>
> Sve ovo radi na besplatnom paketu.
>
> 👉 www.melarium.app

**Hashtagovi FB (5):**
`#pčelarstvo #pčelaribih #prihrana #pripremazazimu #melarium`

**Vizual:**
- **Ekran:** lista programa prehrane
- **Fajl:** `frontend/src/features/diets/FeedingsPage.tsx`
- **Ruta:** `/feedings`
- **U kadru:** kartice na vrhu (Programi / Aktivni / Pčelinjaci / Runde) + bar dva programa, od kojih jedan „Zimsko hranjenje" sa statusom „U toku" i vidljivim brojem obavljenih rundi.
- **Priprema:** unesi program s 4–5 rundi i označi 2 kao obavljene, da se vidi napredak, a ne prazno stanje.

**Alt tekst:**
> Ekran aplikacije Melarium s listom programa prehrane. Prikazan je program „Zimsko hranjenje" sa šećernim sirupom, statusom „U toku" i oznakom da su obavljene 2 od 5 rundi. Iznad liste su kartice s ukupnim brojem programa i aktivnih rundi.

---

## VARIJANTA B — scenarij iz prakse

**Hook:** „Prva subota u septembru. Kupio si 100 kg šećera i imaš 18 košnica."

### Instagram (~145 riječi)

> Prva subota u septembru. Kupio si 100 kg šećera i imaš 18 košnica. 🐝
>
> Plan je jednostavan: sirup 1:1, svakih 5 dana, dok ne uzmu koliko treba.
>
> Plan uvijek jeste jednostavan. Problem je treća sedmica.
>
> Evo kako to upišeš jednom i ne razmišljaš više:
>
> 📝 Nova prehrana → naziv „Zimska prihrana 2026"
> 🍯 razlog: Zimsko hranjenje
> 🥣 hrana: šećerni sirup, napomena „1:1"
> ⚖️ 1,5 L po košnici
> 📅 trajanje 30 dana, svakih 5 dana
>
> Aplikacija ti odmah kaže: 6 rundi, i koje su datume.
>
> Dalje samo otvoriš i čekiraš kad podijeliš. Ako zaboraviš — podsjeti te. U martu tačno znaš šta je koja košnica dobila.
>
> 👉 www.melarium.app

**Hashtagovi IG (13):**
`#pčelarstvo #pčelari #pčelinjak #košnice #prihrana #zimskaprihrana #šećernisirup #fondan #med #pčelarstvobih #pčelaribih #pčelarenje #melarium`

### Facebook (~245 riječi)

> Prva subota u septembru. Kupio si 100 kg šećera i imaš 18 košnica.
>
> Plan je jednostavan: sirup 1:1, svakih pet dana, dok ne uzmu koliko treba. Plan je uvijek jednostavan. Problem počinje treće sedmice, kad se pomiješa šta si podijelio kad.
>
> Evo kako to izgleda kad se upiše jednom.
>
> Otvoriš Melarium, „Nova prehrana". Naziv: Zimska prihrana 2026. Razlog: Zimsko hranjenje — već je u spisku. Hrana: šećerni sirup, u napomenu upišeš 1:1. Količina: 1,5 litara po košnici. Trajanje: 30 dana. Ponavljanje: svakih 5 dana.
>
> Dok još kucaš, aplikacija ispod piše koliko će rundi biti generisano i tokom koliko dana. Vidiš plan prije nego ga spremiš. Odabereš košnice i sačuvaš.
>
> To je bio cijeli unos. Dalje samo otvaraš i čekiraš rundu kad je podijeliš.
>
> Šta se dešava bez tebe:
>
> • ako runda kasni dva dana, dobiješ podsjetnik
> • runde su ti u kalendaru, možeš ih povući i u telefonski kalendar
> • ako nekoj košnici padne nivo meda između dva pregleda, javi ti da razmisliš o prehrani
> • kad se najavi mraz, javi ti da provjeriš prehranu i utopljenost
> • račun za šećer možeš povezati s programom pa vidiš trošak po košnici
>
> U martu ne pogađaš. Otvoriš program i vidiš tačno koja je košnica dobila koju turu.
>
> Besplatno, na bosanskom, radi u pregledniku telefona.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #zimskaprihrana #melarium`

**Vizual:**
- **Ekran:** forma za novi program prehrane
- **Fajl:** `frontend/src/features/diets/DietFormPage.tsx`
- **Ruta:** `/feedings/new`
- **U kadru:** popunjena polja (naziv, razlog „Zimsko hranjenje", hrana „Šećerni sirup", količina 1,5 L) i **obavezno** rečenica koja se generiše uživo ispod polja za frekvenciju — „… rundi bit će generisano, svakih 5 dana tokom 30 dana". To je najuvjerljiviji dio kadra.
- **Napomena:** datum početka mora biti današnji ili raniji — forma ne prima budući datum.

**Alt tekst:**
> Forma za unos novog programa prehrane u aplikaciji Melarium. Popunjen je naziv „Zimska prihrana 2026", razlog „Zimsko hranjenje", vrsta hrane „Šećerni sirup" i količina 1,5 litara po košnici. Ispod polja za trajanje i frekvenciju aplikacija ispisuje da će biti generisano 6 rundi, svakih 5 dana tokom 30 dana.

---

## VARIJANTA C — direktan poziv (CTA-first)

**Hook:** „Počinješ prihranu ovog vikenda? Upiši program jednom — runde se sračunaju same."

### Instagram (~100 riječi)

> Počinješ prihranu ovog vikenda? Upiši program jednom — runde se sračunaju same. 🐝
>
> Upišeš:
> 🍯 vrstu hrane — sirup, fondan, polen, kolači
> ⚖️ koliko ide po košnici
> 📅 trajanje i razmak
>
> Dobiješ:
> ✅ spisak rundi s datumima
> 🔔 podsjetnik ako zakasniš
> ❄️ upozorenje kad se najavi mraz
> 📊 trošak po košnici, ako povežeš račun
>
> I u martu tačno znaš koja je košnica šta dobila.
>
> Besplatno. Bez instalacije — otvoriš u pregledniku.
>
> 👉 www.melarium.app

**Hashtagovi IG (12):**
`#pčelarstvo #pčelari #pčelinjak #prihrana #zimskaprihrana #pripremazazimu #med #pčelarstvobih #pčelaribih #pčelarenje #apikultura #melarium`

### Facebook (~175 riječi)

> Počinješ prihranu ovog vikenda? Upiši program jednom — runde se sračunaju same.
>
> Melarium je pčelarski dnevnik na bosanskom. Za prihranu radi ovo:
>
> • razlog biraš iz spiska — „Zimsko hranjenje" je već unutra
> • hrana: šećerni sirup, fondan, polen, proteinski kolači ili vlastito
> • upišeš količinu po košnici i napomenu (npr. 1:1)
> • upišeš trajanje i na koliko dana ponavljaš — runde se generišu same
> • čekiraš svaku rundu kad je obaviš
> • podsjeti te ako runda kasni
> • javi ti kad se najavi mraz, da provjeriš prehranu i utopljenost
> • javi ti kad nekoj košnici padne nivo meda
> • povežeš račun za šećer i vidiš trošak po košnici
>
> Ne treba instalacija iz prodavnice. Otvoriš u pregledniku telefona i radi, a možeš ga dodati na početni ekran.
>
> Radi na besplatnom paketu.
>
> Ako ovog vikenda kreneš s prihranom — upiši program prije nego počneš.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #prihrana #melarium`

**Vizual:**
- **Ekran:** detalj programa prehrane s rundama
- **Fajl:** `frontend/src/features/diets/DietDetailPage.tsx`
- **Ruta:** `/feedings/:id`
- **U kadru:** naziv programa, status „U toku", i spisak rundi gdje su neke označene kao obavljene a jedna predstojeća.

**Alt tekst:**
> Detaljni prikaz programa prehrane „Zimska prihrana 2026" u aplikaciji Melarium. Vidljiv je status „U toku" i spisak rundi hranjenja, gdje su tri označene kao obavljene, a sljedeća je zakazana za 20.09.2026.

---

# TEMA 2 — Mraz i prognoza po pčelinjaku

**Ruta:** `/apiaries/:id` · **Besplatno:** da · **Objaviti:** sredina oktobra

**Hook:** „Ne gledaš prognozu za grad. Gledaš je za svoj pčelinjak."

### Instagram (~120 riječi)

> Ne gledaš prognozu za grad. Gledaš je za svoj pčelinjak. 🐝
>
> Razlika zna biti tri stepena. A tri stepena u novembru je razlika između „u redu je" i „trebao sam ih utopliti prošle sedmice".
>
> Melarium svakom pčelinjaku vuče prognozu za **njegove koordinate**:
>
> 🌤️ 7 dana unaprijed
> 🌡️ min i max temperatura po danu
> ❄️ i sam ti javi kad se najavi mraz
>
> Poruka koja stigne nije opšta — piše ime tvog pčelinjaka i koliko stepeni se najavljuje.
>
> Da bi radilo, pčelinjaku moraju biti upisane koordinate. To je jedan unos, jednom.
>
> 👉 www.melarium.app

**Hashtagovi IG (12):**
`#pčelarstvo #pčelari #pčelinjak #košnice #mraz #zimapčele #pripremazazimu #prognoza #pčelarstvobih #pčelaribih #pčelarenje #melarium`

### Facebook (~200 riječi)

> Ne gledaš prognozu za grad. Gledaš je za svoj pčelinjak.
>
> Ko ima pčelinjak u kotlini ili uz rijeku, zna o čemu pričam. Prognoza za grad kaže 2 stepena, a kod tebe dolje bude minus. Razlika od tri stepena u novembru je razlika između „u redu je" i „trebao sam ih utopliti prošle sedmice".
>
> Melarium vuče prognozu za koordinate svakog pčelinjaka posebno. Ne za najbližu meteorološku stanicu u gradu — za tačku koju si ti označio.
>
> Na stranici pčelinjaka vidiš sedam dana unaprijed, s najnižom i najvišom temperaturom po danu.
>
> A kad se u naredna dva dana najavi temperatura ispod nule, aplikacija ti sama pošalje upozorenje. Ne opšte — u poruci piše ime tvog pčelinjaka i koliko stepeni se najavljuje, uz podsjetnik da provjeriš prehranu i utopljenost.
>
> Jedini uslov je da pčelinjaku upišeš koordinate. To uradiš jednom, kad ga dodaješ.
>
> Ako imaš pčelinjake na dvije lokacije, svaki dobija svoju prognozu i svoje upozorenje.
>
> Radi na besplatnom paketu, na bosanskom.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #mraz #melarium`

**Vizual:**
- **Ekran:** kartice sedmodnevne prognoze na stranici pčelinjaka
- **Fajl:** `frontend/src/features/apiaries/ApiaryDetailPage.tsx` (komponenta `DayCard`, oko linije 70)
- **Ruta:** `/apiaries/:id`
- **U kadru:** red od 7 dnevnih kartica s emoji ikonama i min/max temperaturama, plus naziv pčelinjaka iznad. Idealno uslikati kad prognoza pokazuje nisku temperaturu.

**Alt tekst:**
> Stranica pčelinjaka u aplikaciji Melarium sa sedmodnevnom vremenskom prognozom. Prikazano je sedam kartica, po jedna za svaki dan, sa ikonom vremena te najnižom i najvišom temperaturom.

---

# TEMA 3 — Šta te je koštala prihrana (skeniranje računa)

**Ruta:** `/expenses/scan` · **Besplatno:** da · **Objaviti:** kraj oktobra

**Hook:** „Koliko te je koštala prihrana po košnici? Ne po pčelinjaku — po košnici."

### Instagram (~130 riječi)

> Koliko te je koštala prihrana po košnici? Ne po pčelinjaku — po košnici. 🐝
>
> Većina nas zna koliko je dala za šećer. Malo ko zna koliko to ispada po društvu.
>
> U Melariumu ide ovako:
>
> 📸 slikaš račun za šećer
> 🔍 aplikacija pročita stavke s računa
> ✏️ pregledaš i ispraviš ako je nešto pokupila krivo
> 🔗 povežeš stavku s programom prihrane
> 📊 dobiješ trošak po košnici
>
> Čitanje računa radi **na samom telefonu** — slika se ne šalje nikuda.
>
> Nije čarobno i nije uvijek 100% tačno, zato i postoji korak gdje pregledaš prije nego spremiš. Ali je brže nego prekucavati.
>
> 👉 www.melarium.app

**Hashtagovi IG (12):**
`#pčelarstvo #pčelari #pčelinjak #prihrana #troškovi #šećer #med #pčelarstvobih #pčelaribih #pčelarenje #apikultura #melarium`

### Facebook (~215 riječi)

> Koliko te je koštala prihrana po košnici? Ne po pčelinjaku — po košnici.
>
> Većina nas zna koliko je otišlo na šećer ove jeseni. Puno manje nas zna koliko to ispadne po jednom društvu, ili koliko je prihrana pojela od onoga što je med donio.
>
> U Melariumu se to izračuna samo, ali treba mu jedan podatak — račun.
>
> Slikaš račun telefonom. Aplikacija ga pročita i izvuče stavke s cijenama. Zatim ti pokaže spisak da ga pregledaš — i tu ispraviš ako je nešto pokupila krivo. To je namjerno tako: čitanje računa nije savršeno i nije zamišljeno da mu se slijepo vjeruje, nego da ti uštedi prekucavanje.
>
> Kad potvrdiš, svaku stavku možeš povezati s programom prihrane. Od tog trenutka program prihrane pokazuje ukupan trošak i trošak po košnici.
>
> Jedna stvar koju vrijedi znati: čitanje računa se dešava na samom telefonu, u pregledniku. Slika računa se ne šalje na server.
>
> Radi na besplatnom paketu.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #troškovi #melarium`

**Vizual:**
- **Ekran:** korak pregleda nakon skeniranja računa
- **Fajl:** `frontend/src/features/expenses/ReceiptScanPage.tsx`
- **Ruta:** `/expenses/scan`
- **U kadru:** prepoznate stavke s cijenama u tabeli za ispravku. Idealno uslikati stvarni račun za šećer.
- **Alternativa:** karusel — prvi kadar skeniranje, drugi `/feedings/:id` s prikazom troška po košnici.

**Alt tekst:**
> Ekran za skeniranje računa u aplikaciji Melarium. Nakon fotografisanja računa prikazane su prepoznate stavke s količinama i cijenama, u obliku tabele koju korisnik može ispraviti prije spremanja.

---

# TEMA 4 — Sezona u brojkama

**Ruta:** `/stats` · **Besplatno:** da · **Objaviti:** novembar

**Hook:** „Koja ti je košnica ove godine dala najviše? Ako nagađaš — nagađaš i sljedeće godine."

### Instagram (~125 riječi)

> Koja ti je košnica ove godine dala najviše? Ako nagađaš — nagađaš i sljedeće godine. 🐝
>
> Sezona je gotova. Vrijeme je da se vidi šta je bilo.
>
> Melarium ti iz unesenih vrcanja složi:
>
> 📊 ukupan prinos sezone u kg
> 🍯 koliko po vrsti meda — bagrem, lipa, kesten, livadski…
> 🏆 najbolje košnice po prinosu
> 🏡 koliko je dao koji pčelinjak
> 📈 poređenje po godinama
> 💰 procjenu vrijednosti i trošak prihrane
>
> Ništa se ne izmišlja — sve je iz onoga što si sam unio tokom sezone.
>
> Zato se isplati unositi usput, a ne po sjećanju u decembru.
>
> 👉 www.melarium.app

**Hashtagovi IG (13):**
`#pčelarstvo #pčelari #pčelinjak #košnice #med #vrcanje #prinos #bagremovmed #statistika #pčelarstvobih #pčelaribih #pčelarenje #melarium`

### Facebook (~210 riječi)

> Koja ti je košnica ove godine dala najviše? Ako nagađaš — nagađaš i sljedeće godine.
>
> Svako od nas ima osjećaj koja društva „dobro rade". Problem je što je to osjećaj. A kad dođe vrijeme da odlučiš od koje matice uzimaš potomstvo, ili koje društvo spajaš — osjećaj nije dovoljan.
>
> Melarium iz vrcanja koja si unio tokom sezone složi pregled:
>
> • ukupan prinos sezone u kilogramima
> • koliko po vrsti meda — bagrem, lipa, kesten, suncokret, livadski, šumski
> • koje su košnice dale najviše
> • koliko je dao koji pčelinjak
> • poređenje s prethodnim godinama
> • procjena vrijednosti prinosa i koliko je otišlo na prihranu
>
> Sve je izvedeno iz tvojih unosa — aplikacija ništa ne pretpostavlja. Ako nisi unosio, neće biti šta pokazati.
>
> Zato je poenta unositi usput, na pčelinjaku, dok traje sezona. Vrcanje se unese za pola minute, a u novembru imaš sliku cijele godine umjesto pokušaja prisjećanja.
>
> Radi na besplatnom paketu, na bosanskom.
>
> 👉 www.melarium.app

**Hashtagovi FB (5):**
`#pčelarstvo #pčelaribih #med #prinos #melarium`

**Vizual:**
- **Ekran:** stranica statistike
- **Fajl:** `frontend/src/features/stats/StatsPage.tsx`
- **Ruta:** `/stats`
- **U kadru:** grafikon prinosa po vrsti meda ili top košnice po prinosu, plus kartica s ukupnim kg sezone.
- **Priprema:** treba unesenih vrcanja iz više mjeseci — prazni grafikoni izgledaju loše. Ovo je tema koju objavi tek kad imaš realan set podataka.

**Alt tekst:**
> Stranica statistike u aplikaciji Melarium. Prikazan je ukupan prinos sezone u kilogramima, grafikon raspodjele po vrstama meda i lista košnica poredanih po prinosu.

---

# TEMA 5 — Obaveze u telefonskom kalendaru

**Ruta:** `/calendar/settings` · **Besplatno:** da · **Objaviti:** decembar

**Hook:** „Ako pčelarska obaveza nije u kalendaru u kojem gledaš sve ostalo — nije ni obaveza."

### Instagram (~120 riječi)

> Ako pčelarska obaveza nije u kalendaru u kojem gledaš sve ostalo — nije ni obaveza. 🐝
>
> Melarium ti obaveze s pčelinjaka može ubaciti u **Google, Apple ili Outlook kalendar**. Tamo gdje ti već stoje sastanci i rođendani.
>
> Sam biraš šta ide:
> 🍯 hranjenja
> 💊 tretmani — vađenje traka i istek karence
> 📋 zadaci s rokom
> 🔍 preporučeni pregledi
>
> Plus jutarnji podsjetnik u 8h sa svim današnjim obavezama.
>
> Adresa kalendara je privatna i možeš je poništiti kad hoćeš.
>
> Uputstvo za Google i iPhone je u samoj aplikaciji.
>
> 👉 www.melarium.app

**Hashtagovi IG (11):**
`#pčelarstvo #pčelari #pčelinjak #košnice #kalendar #organizacija #med #pčelarstvobih #pčelaribih #pčelarenje #melarium`

### Facebook (~205 riječi)

> Ako pčelarska obaveza nije u kalendaru u kojem gledaš sve ostalo — nije ni obaveza.
>
> To je razlog zašto podsjetnici unutar aplikacija često ne rade. Čovjek otvori aplikaciju kad se sjeti, a treba mu obrnuto.
>
> Zato Melarium obaveze s pčelinjaka može ubaciti direktno u tvoj Google, Apple ili Outlook kalendar — tamo gdje ti već stoje sastanci, rođendani i sve ostalo.
>
> Sam biraš šta se prenosi:
>
> • hranjenja (runde prihrane)
> • tretmani — vađenje traka i istek karence
> • zadaci s rokom
> • preporučeni pregledi za košnice koje dugo nisu pregledane
>
> Uz to možeš uključiti jutarnji podsjetnik u 8h — zvono u aplikaciji i email sa svim današnjim obavezama.
>
> Adresa kalendara je privatna i vezana samo za tebe. Ako je nekad podijeliš greškom, izdaš novu i stara prestaje raditi.
>
> Uputstvo korak po korak za Google Calendar i za iPhone stoji u samoj aplikaciji, na stranici s podešavanjima.
>
> Jedna napomena da ne bude iznenađenja: vanjski kalendari osvježavaju pretplatu periodično — Google zna kasniti i do 24 sata. Jutarnji podsjetnik u 8h stiže odmah, kroz aplikaciju.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #kalendar #melarium`

**Vizual:**
- **Ekran:** podešavanja kalendara i podsjetnika
- **Fajl:** `frontend/src/features/calendar/CalendarSettingsPage.tsx`
- **Ruta:** `/calendar/settings`
- **U kadru:** prekidači za kategorije (Hranjenja, Zadaci, Tretmani, Preporučeni pregledi) i prekidač „Jutarnji podsjetnik u 8h".
- **VAŽNO:** **zamutiti adresu ICS feeda** na slici — to je privatni token koji daje pristup obavezama.

**Alt tekst:**
> Stranica podešavanja kalendara u aplikaciji Melarium. Vidljivi su prekidači za sinhronizaciju hranjenja, zadataka, tretmana i preporučenih pregleda, te prekidač za jutarnji podsjetnik u 8 sati.

---

# TEMA 6 — QR kod na košnici

**Ruta:** `/scan/:uniqueId` · **Besplatno:** da · **Objaviti:** januar/februar

**Hook:** „Stojiš pred košnicom. Koja je ovo bila po redu?"

### Instagram (~115 riječi)

> Stojiš pred košnicom. Koja je ovo bila po redu? 🐝
>
> Svi smo brojali od kraja reda da bismo bili sigurni.
>
> Svaka košnica u Melariumu dobija svoj QR kod. Odštampaš ga, zalijepiš na poklopac, i dalje samo skeniraš telefonom.
>
> 📱 skeniraš → otvara se ta košnica
> 📋 vidiš zadnje preglede
> 💊 vidiš je li u karenci
> 🍯 vidiš je li na prihrani
> ✍️ unosiš pregled odmah, bez traženja po spisku
>
> Bez brojanja i bez „mislim da je ovo ona s mladom maticom".
>
> Zimi je pravo vrijeme da se kodovi odštampaju i zalijepe.
>
> 👉 www.melarium.app

**Hashtagovi IG (12):**
`#pčelarstvo #pčelari #pčelinjak #košnice #qrkod #evidencija #med #pčelarstvobih #pčelaribih #pčelarenje #apikultura #melarium`

### Facebook (~195 riječi)

> Stojiš pred košnicom. Koja je ovo bila po redu?
>
> Svako od nas je bar jednom brojao od kraja reda da bi bio siguran koju upisuje. A onda se javi sumnja je li ono jučer upisano bilo za ovu ili za susjednu.
>
> Svaka košnica u Melariumu dobija svoj QR kod. Odštampaš ga, zalijepiš na poklopac — i to je sve što treba uraditi jednom.
>
> Dalje samo skeniraš telefonom. Otvara se tačno ta košnica: zadnji pregledi, je li u karenci nakon tretmana, je li na programu prihrane, koja je matica unutra. Pregled unosiš odmah, bez traženja po spisku.
>
> Ako kod skenira neko ko nema pristup tvojoj organizaciji, neće vidjeti ništa — kod vodi u aplikaciju, ali podatke otvara samo tvoj nalog.
>
> Zima je pravo vrijeme za ovo. Kad su pčele u klubetu i nema posla oko njih, odštampaš kodove i polijepiš ih na miru. Do proljeća je sve spremno.
>
> Radi na besplatnom paketu.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #qrkod #melarium`

**Vizual:**
- **Ekran:** QR kod na stranici košnice
- **Fajl:** `frontend/src/features/beehives/BeehiveDetailPage.tsx` (oko linije 282–331)
- **Ruta:** `/beehives/:id`
- **U kadru:** generisani QR kod s ispisanim identifikatorom košnice ispod.
- **Bolja varijanta:** fotografija stvarnog odštampanog QR koda zalijepljenog na poklopac košnice, s telefonom koji ga skenira. To je jača slika od screenshota.

**Alt tekst:**
> QR kod jedne košnice prikazan u aplikaciji Melarium, s jedinstvenim identifikatorom košnice ispisanim ispod koda.

---

# TEMA 7 — Pregled bez signala

**Ruta:** `/outbox` · **Besplatno:** da · **Objaviti:** mart

**Hook:** „Pčelinjak u brdu, signala nema. Pregled ipak upisuješ."

### Instagram (~115 riječi)

> Pčelinjak u brdu, signala nema. Pregled ipak upisuješ. 🐝
>
> Melarium sprema pregled na sam telefon kad nema mreže. Kad se vratiš u signal, sam se pošalje.
>
> 📴 unosiš normalno, bez interneta
> 💾 ostaje sačuvano na telefonu
> 📤 pošalje se samo kad se mreža vrati
> 📋 u „Neposlani pregledi" vidiš šta još čeka
>
> Nema prepisivanja uveče i nema „sjećam se da je bilo nešto s onom trećom".
>
> Jedna napomena da ne bude iznenađenja: ovako rade **pregledi**. Tretmani i prihrana i dalje traže konekciju.
>
> 👉 www.melarium.app

**Hashtagovi IG (11):**
`#pčelarstvo #pčelari #pčelinjak #košnice #pregledkošnica #offline #med #pčelarstvobih #pčelaribih #pčelarenje #melarium`

### Facebook (~190 riječi)

> Pčelinjak u brdu, signala nema. Pregled ipak upisuješ.
>
> Ovo je razlog zašto dosta pčelara odustane od digitalne evidencije poslije prve sezone. Aplikacija traži internet, a internet je tamo gdje pčela nije. Pa se vratiš na svesku, ili na „zapamtiću pa upisati uveče" — što u praksi znači da se ne upiše.
>
> Melarium radi i bez mreže. Pregled unosiš normalno; ako konekcije nema, sprema se na sam telefon. Kad se vratiš u signal, sam se pošalje na server.
>
> U meniju postoji stavka „Neposlani pregledi" gdje tačno vidiš šta još čeka na slanje, tako da ne moraš vjerovati na riječ.
>
> Da bude jasno i pošteno: ovako rade pregledi košnica. Unos tretmana i programa prihrane i dalje traže konekciju — to su stvari koje ionako rijetko upisuješ stojeći nad otvorenom košnicom.
>
> Aplikaciju možeš dodati na početni ekran telefona pa se ponaša kao obična aplikacija.
>
> Radi na besplatnom paketu.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #pregledkošnica #melarium`

**Vizual:**
- **Ekran:** neposlani pregledi
- **Fajl:** `frontend/src/features/offline/OutboxPage.tsx`
- **Ruta:** `/outbox`
- **U kadru:** spisak pregleda koji čekaju slanje, s nazivima košnica. Idealno uslikati s uključenim avionskim modom da se u statusnoj traci vidi da nema mreže — to je detalj koji objavu čini uvjerljivom.

**Alt tekst:**
> Ekran „Neposlani pregledi" u aplikaciji Melarium, sa spiskom pregleda košnica sačuvanih na telefonu koji čekaju slanje na server kad se vrati internet veza.

---

# TEMA 8 — Koliko je stara tvoja matica

**Ruta:** detalj košnice · **Besplatno:** da · **Objaviti: mart** (ne ranije — vidi 10.4)

**Hook:** „Koje je godine matica u trećoj košnici? Ne koje boje — koje godine."

### Instagram (~120 riječi)

> Koje je godine matica u trećoj košnici? Ne koje boje — koje godine. 🐝
>
> Boju vidiš kad otvoriš. Ali ako je matica tiho zamijenjena, boja laže.
>
> Melarium vodi maticu po košnici:
>
> 👑 godina i boja markiranja
> ✂️ je li markirana i je li podrezana
> 📍 porijeklo — kupljena, vlastiti uzgoj, roj, tiha zamjena
> 📜 cijela historija zamjena po košnici
>
> Kad upišeš novu maticu, stara se automatski zatvori kao zamijenjena. Ne briše se — ostaje u historiji.
>
> A u martu ti aplikacija javi za svaku maticu koja ulazi u treću sezonu.
>
> 👉 www.melarium.app

**Hashtagovi IG (12):**
`#pčelarstvo #pčelari #pčelinjak #košnice #matica #zamjenamatice #uzgojmatica #med #pčelarstvobih #pčelaribih #pčelarenje #melarium`

### Facebook (~200 riječi)

> Koje je godine matica u trećoj košnici? Ne koje boje — koje godine.
>
> Boju vidiš kad otvoriš. Ali boja ti kaže samo šta si ti markirao. Ako je društvo u međuvremenu izvelo tihu zamjenu, unutra je matica koju nisi ni vidio, a ti i dalje računaš po staroj boji.
>
> Melarium vodi maticu kao zasebnu stavku svake košnice: godina, boja markiranja, je li markirana, je li podrezana, porijeklo — kupljena, vlastiti uzgoj, roj ili tiha zamjena.
>
> Kad upišeš novu maticu, stara se u istom potezu zatvori kao zamijenjena. Ne briše se. Ostaje ti historija po košnici — koliko je matica prošlo kroz to društvo i koliko je koja izdržala. To je podatak koji ti govori više o društvu nego jedan pregled.
>
> A u martu, kad je vrijeme za planiranje, aplikacija ti sama javi za svaku maticu koja ulazi u treću sezonu — da je staviš na spisak za zamjenu prije nego krene sezona.
>
> Radi na besplatnom paketu, na bosanskom.
>
> 👉 www.melarium.app

**Hashtagovi FB (4):**
`#pčelarstvo #pčelaribih #matica #melarium`

**Vizual:**
- **Ekran:** kartica matice u detalju košnice
- **Fajl:** `frontend/src/features/beehives/BeehiveDetailPage.tsx` (sekcija matica)
- **Ruta:** `/beehives/:id`
- **U kadru:** aktivna matica s godinom i bojom markiranja, plus historija ranijih matica ispod.

**Alt tekst:**
> Prikaz matice u detalju košnice u aplikaciji Melarium. Vidljiva je aktivna matica s godinom, bojom markiranja i porijeklom, a ispod je historija ranijih matica u toj košnici.

---

## 9. Šta je potvrđeno u kodu

### Prehrana (tema 1)

| Tvrdnja | Izvor |
|---|---|
| Razlog „Zimsko hranjenje" postoji kao ugrađena stavka | `models/index.ts:397` (`DietReason.WinterFeeding`), label `:409` |
| Vrste hrane: sirup, fondan, polen, proteinski kolači, vlastito | `models/index.ts:419-433` (`FoodType`) |
| Količina po košnici + jedinica (L/ml/kg/g) | `models/index.ts:435-447`, `DietFormPage.tsx:424` |
| Napomena o količini (npr. „1:1, pola pogače") | `DietFormPage.tsx:453` |
| Runde se generišu iz trajanja i frekvencije | `DietFormPage.tsx:29-31` (`calcEntryCount`), `:117` |
| Živi prikaz „X rundi … svakih N dana tokom D dana" | `DietFormPage.tsx:378` |
| Runda se čekira pojedinačno | `DietsController.cs` → `POST /{dietId}/feeding-entries/{entryId}/complete` |
| Status programa: Nije počeo / U toku / Završen / Prekinut | `models/index.ts:383-388` (`DietStatus`) |
| Alarm „Hranjenje kasni" nakon 2 dana | `AlertRuleService.cs:241-243`; `appsettings.json:57` (`FeedingOverdueDays`) |
| Alarm „Opada nivo meda — razmisli o prehrani" | `AlertRuleService.cs` → `ApplyHoneyDropAsync` |
| Alarm „Najavljen mraz … Provjeri prehranu i utopljenost" | `AlertRuleService.cs` → `ApplyFrostAsync` |
| Trošak prihrane i trošak po košnici | `DietService.cs:572-574`; `models/index.ts:504-516` |
| Trošak se veže preko stavke računa | `ExpenseFormPage.tsx:150, 334-336` (`dietId` po stavci) |
| Prehrana nije iza pretplate | `DietService.cs` — nema poziva `IPlanGuard` |

### Ostale teme

| Tvrdnja | Izvor |
|---|---|
| Prognoza 7 dana po koordinatama pčelinjaka | `WeatherService.cs:21-28` (`forecast_days=7`, Open-Meteo) |
| Mraz se gleda za naredna 2 dana, prag < 0 °C | `AlertRuleService.cs` → `ApplyFrostAsync` |
| Bez koordinata pčelinjaka nema upozorenja o mrazu | isto — `if (apiary.Latitude is not double lat …) return` |
| Čitanje računa radi na uređaju (Tesseract.js, model `hrv`) | `ReceiptScanPage.tsx:24-31` |
| Prepoznate stavke se ručno pregledaju prije spremanja | `ReceiptScanPage.tsx:34-36` (komentar: „designed to be edited by the user") |
| Statistika: kg sezone, po vrsti meda, top košnice, po godinama | `StatsDto.cs:24-32` |
| Statistika: procjena vrijednosti i trošak prihrane | `StatsDto.cs:26`, `:44` |
| Vrste meda: bagrem, lipa, kesten, suncokret, livadski, šumski, uljana repica | `models/index.ts:866-885` (`HoneyType`) |
| ICS feed za Google/Apple/Outlook, privatni token | `CalendarController.cs:58-70`; `CalendarSettingsPage.tsx:46-48` |
| Token se može poništiti i izdati novi | `CalendarController.cs:40` |
| Kategorije koje se sinhronizuju (4 prekidača) | `CalendarSettingsPage.tsx:117-120` |
| Jutarnji podsjetnik u 8h (zvono + email) | `CalendarSettingsPage.tsx:136`; `appsettings.json:73` (`LocalHour: 8`) |
| Upozorenje da Google kasni i do 24h | `CalendarSettingsPage.tsx:89` — preuzeto doslovno u objavu |
| QR kod po košnici (`uniqueId` + slika koda) | `BeehiveDetailPage.tsx:78, 282-331` |
| Skeniranje bez pristupa ne otkriva podatke | `ScanPage.tsx:25-45` (javni lookup → login s `returnUrl`) |
| Offline pregledi u IndexedDB, sami se šalju | `core/offline/outbox.ts`, `syncOutbox.ts` |
| Matica: godina, boja, markirana, podrezana, porijeklo | `Domain/Entities/Queen.cs:14-33` |
| Boje markiranja: bijela, žuta, crvena, zelena, plava | `models/index.ts:229-243` |
| Nova matica automatski zatvara staru kao zamijenjenu | `QueensController.cs:45-49` |
| Historija izmjena zapisa o matici | `QueensController.cs:81-82` |
| Alarm za staru maticu (3. sezona) | `AlertRuleService.cs` → `ApplyOldQueenAsync` |

---

## 10. Ograničenja i šta provjeriti ručno

**10.1 — Aplikacija na produkciji nije provjerena.** Kao i prošli put, `melarium.app` je blokiran na mrežnom gatewayu ove sesije, pa se nisam mogao prijaviti. Sve gore je iz koda na grani `main`. Prije svake objave provjeri da funkcija stvarno radi na živoj verziji.

**10.2 — Prehrana ne mjeri zalihe u kilogramima.** Aplikacija nema težinu košnice ni broj okvira hrane. Nivo meda je procjena u tri stepena (nisko/srednje/visoko) koju sam upisuješ pri pregledu (`Inspection.HoneyLevel`). **Ne pisati** da aplikacija računa je li društvo spremno za zimu ili koliko mu kilograma fali.

**10.3 — Datum početka prehrane ne može biti u budućnosti.** Frontend to odbija (`DietFormPage.tsx:132`). Backend validator to ne provjerava (`CreateDietValidator.cs:18-19`), ali kroz aplikaciju budući datum ne prolazi. Zato objave kažu „upiši program kad krećeš", a ne „zakaži unaprijed". Ako to hoćeš mijenjati, to je izmjena u kodu, ne u tekstu objave.

**10.4 — Alarm za staru maticu radi samo u martu.** Prva linija pravila je `if (now.Month != 3) return`. Ako temu 8 objaviš u septembru, ljudi će uključiti aplikaciju i neće dobiti nikakvo upozorenje mjesecima. **Zato je ta tema stavljena u mart.**

**10.5 — Mraz i prognoza traže koordinate pčelinjaka.** Bez `Latitude`/`Longitude` pravilo se tiho preskače. Objava to spominje, ali provjeri koliko je lako unijeti koordinate pri dodavanju pčelinjaka — ako je to zeznuto, tema 2 će razočarati ljude.

**10.6 — Trošak prihrane vide samo administratori.** Server izostavlja iznose za ulogu Beekeeper (`DietService.cs:577`), a stranica troškova je ograničena na `ApiaryAdmin`/`OrganizationAdmin`/`SystemAdmin` (`App.tsx`, `EXPENSE_MANAGERS`). Za pčelara koji radi sam nije problem — on je vlasnik organizacije. Ali ne obećavati to članovima tima.

**10.7 — Skeniranje računa nije pouzdano 100%.** Parser je jednostavan (traži cijenu na kraju reda) i sam komentar u kodu kaže da je zamišljen tako da korisnik ispravlja rezultat. Objava je namjerno formulisana skromno — **ne pojačavati je** u komentarima u „automatski unosi račune".

**10.8 — Statistiku ne objavljivati s praznim grafikonima.** Sve brojke su izvedene iz unesenih vrcanja. Screenshot s jednim stupcem je gori od nikakvog. Objavi temu 4 tek kad na test nalogu bude realan set podataka kroz više mjeseci.

**10.9 — Na screenshotu kalendara zamutiti adresu feeda.** ICS token je jedina zaštita tog feeda — ko ga vidi, vidi tvoje obaveze.

**10.10 — Provjeri da produkcija šalje email.** Sva upozorenja iz ovih objava („javi ti", „podsjeti te") stižu kao obavijest u aplikaciji uvijek, a e-mailom samo ako je `Smtp:Password` postavljen na produkciji. In-app zvono radi neovisno.

**10.11 — Ne objavljivati podatke test naloga.** Isto kao prošli put: `testorg@melarium.com` je običan nalog na produkciji, ne demo nalog iz koda.
