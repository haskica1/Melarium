import { LearningCategory } from '../models'
import type { HelpKey } from './helpRoutes'

/** Role names as they appear on `AuthUser.role`. */
export type HelpRoleName = 'SystemAdmin' | 'OrganizationAdmin' | 'ApiaryAdmin' | 'Beekeeper'

export interface HelpFaqItem {
  q: string
  a: string
}

export interface HelpEntry {
  /** Panel heading — the page's name, not a sentence. */
  title: string
  /** One or two sentences: what this page is for. */
  summary: string
  /** How to do the main thing here, rendered as a numbered list. */
  steps?: string[]
  /** What matters, and the mistakes people actually make. */
  tips?: string[]
  faq?: HelpFaqItem[]
  /**
   * Extra note shown only to that role — the same page genuinely means different things
   * (a Beekeeper is read-only on Tretmani, an OrgAdmin is not).
   */
  notesByRole?: Partial<Record<HelpRoleName, string>>
  /**
   * Deep-links to `/learning?category=…`. A *category*, never a topic id — ids are
   * database-generated and differ per environment, so a hardcoded id would point at the wrong
   * article outside whichever environment it was written in.
   */
  learningCategory?: LearningCategory
}

/**
 * Short, UI-coupled "how does this page work" content. Lives in code on purpose: it describes the
 * interface, so it has to change in the same commit as the interface — a database copy has no way to
 * notice the UI moved. Long-form beekeeping knowledge belongs in Edukacija (SPEC-06) instead, and
 * `learningCategory` links there rather than duplicating it.
 */
export const HELP_CONTENT: Record<HelpKey, HelpEntry> = {
  // ── Apiaries ────────────────────────────────────────────────────────────────
  '/apiaries': {
    title: 'Pčelinjaci',
    summary:
      'Ovo je vaša početna stranica. Pčelinjak je lokacija na kojoj držite košnice — svaka košnica pripada tačno jednom pčelinjaku.',
    steps: [
      'Otvorite pčelinjak klikom na njegovu karticu.',
      'Unutra vidite sve košnice tog pčelinjaka, vremensku prognozu i obaveze.',
      'Novi pčelinjak dodajete dugmetom „Novi pčelinjak“.',
    ],
    tips: [
      'Unesite koordinate pčelinjaka (na formi možete pokazati lokaciju na mapi) — bez njih nema prognoze ni upozorenja na mraz.',
      'Ako radite selidbe, ne pravite novi pčelinjak za novu lokaciju. Koristite „Preseli“ unutar pčelinjaka, da vam ostane historija.',
    ],
    notesByRole: {
      Beekeeper:
        'Vidite samo pčelinjake u kojima imate dodijeljenu barem jednu košnicu. Ako je lista prazna, administrator vam još nije dodijelio košnice.',
      ApiaryAdmin: 'Vidite pčelinjak koji vam je dodijeljen.',
    },
    learningCategory: LearningCategory.Osnove,
  },

  '/apiaries/new': {
    title: 'Novi pčelinjak',
    summary: 'Napravite lokaciju na kojoj ćete držati košnice.',
    steps: [
      'Unesite naziv po kojem ćete ga prepoznati (npr. „Kuća“, „Vlašić“).',
      'Postavite lokaciju na mapi ili unesite koordinate.',
      'Sačuvajte — zatim možete dodavati košnice.',
    ],
    tips: [
      'Lokacija nije samo informacija: iz nje se vuče vremenska prognoza, upozorenje na mraz i prikaz na mapi.',
      'Naziv birajte kratak — pojavljuje se u listama, kalendaru i izvještajima.',
    ],
  },

  '/apiaries/:id/edit': {
    title: 'Uredi pčelinjak',
    summary: 'Mijenjajte naziv, opis i lokaciju pčelinjaka.',
    tips: [
      'Promjena koordinata odmah mijenja prognozu i upozorenja za sve košnice u ovom pčelinjaku.',
      'Ako pčelinjak fizički seli na drugu lokaciju, nemojte samo prepisati koordinate — koristite „Preseli“ da ostane evidencija selidbe i prinos po pašnjaku.',
    ],
  },

  '/apiaries/:id': {
    title: 'Detalji pčelinjaka',
    summary:
      'Sve o jednoj lokaciji: košnice, vremenska prognoza, obaveze, tretmani i historija selidbi.',
    steps: [
      'Dodajte košnicu dugmetom „Nova košnica“.',
      'Otvorite košnicu klikom na nju za preglede, maticu i prehranu.',
      'Zadatke za cijeli pčelinjak dodajete u sekciji obaveza.',
    ],
    tips: [
      'Prognoza dolazi iz koordinata pčelinjaka. Ako je nema, provjerite da su lokacija i koordinate unesene.',
      'QR naljepnice za sve košnice možete izvesti odjednom — praktično je odštampati ih jednom i zalijepiti na košnice.',
    ],
    faq: [
      {
        q: 'Zašto ne vidim sve košnice?',
        a: 'Ako ste u roli pčelara, vidite samo košnice koje su vam dodijeljene. Administrator organizacije to mijenja u „Članovi“.',
      },
    ],
  },

  // ── Beehives ────────────────────────────────────────────────────────────────
  '/beehives/new': {
    title: 'Nova košnica',
    summary: 'Dodajte košnicu u pčelinjak. Svaka košnica dobija svoj QR kod automatski.',
    steps: [
      'Odaberite pčelinjak i unesite broj ili naziv košnice.',
      'Odaberite tip (LR, DB, AŽ, pološka) i materijal.',
      'Sačuvajte — QR kod se generiše sam.',
    ],
    tips: [
      'Broj košnice unesite isti kao onaj napisan na samoj košnici, da se u polju ne gubite.',
      'QR kod možete odštampati i zalijepiti — skeniranjem odmah otvarate tu košnicu, bez traženja po listi.',
    ],
    learningCategory: LearningCategory.Oprema,
  },

  '/beehives/:id/edit': {
    title: 'Uredi košnicu',
    summary: 'Mijenjajte podatke košnice — broj, tip, materijal, napomene.',
    tips: ['QR kod se ne mijenja kad uredite košnicu, pa naljepnice ostaju važeće.'],
  },

  '/beehives/:id': {
    title: 'Košnica',
    summary:
      'Centralna stranica jedne kolonije: pregledi, matica, prehrana, tretmani, prinos i QR kod.',
    steps: [
      'Novi pregled dodajete dugmetom „Novi pregled“ — to je najčešća radnja u aplikaciji.',
      'Karticu „Matica“ koristite kad zamijenite ili izgubite maticu.',
      'Pod prehranom vidite aktivni program hranjenja i naredni termin.',
    ],
    tips: [
      'Pregled možete diktirati glasom — brže je nego tipkati u polju s rukavicama.',
      'Uz pregled možete dodati do 5 fotografija okvira, uz opcionalnu AI analizu.',
      'Bez signala pregled se sprema lokalno i pošalje se sam kad se vratite u mrežu.',
    ],
    faq: [
      {
        q: 'Šta znači boja oznake matice?',
        a: 'Boja se izvodi iz godine rođenja po međunarodnom kodu, tako da po boji odmah znate starost matice. Aplikacija je predlaže sama.',
      },
      {
        q: 'Mogu li pitati savjetnika o ovoj košnici?',
        a: 'Da — „Pitaj savjetnika“ otvara AI razgovor koji poznaje podatke ove košnice: preglede, prehranu, maticu, prinos i zadnji tretman.',
      },
    ],
    learningCategory: LearningCategory.Osnove,
  },

  // ── Inspections ─────────────────────────────────────────────────────────────
  '/inspections/new': {
    title: 'Novi pregled',
    summary:
      'Zabilježite šta ste vidjeli u košnici. Pregledi su temelj svega ostalog — iz njih se izvode upozorenja, statistika i savjeti.',
    steps: [
      'Provjerite datum (predložen je današnji).',
      'Unesite stanje legla i količinu meda, te napomenu ako ima nešto neobično.',
      'Dodajte fotografije ako želite, pa sačuvajte.',
    ],
    tips: [
      'Umjesto tipkanja možete pritisnuti mikrofon i ispričati pregled — aplikacija sama izvuče datum, stanje legla, med i napomenu, a vi provjerite prije spremanja.',
      'Temperatura se popunjava sama iz prognoze pčelinjaka, ne morate je unositi.',
      'Ako nemate signal, pregled ide u „Neposlani pregledi“ i pošalje se automatski kad se vratite u mrežu. Ništa se ne gubi.',
      'Redovni pregledi su ono što pokreće upozorenje „pregled kasni“ — ako ih ne bilježite, aplikacija vas ne može podsjetiti.',
    ],
    faq: [
      {
        q: 'Zašto glasovni unos ne radi offline?',
        a: 'Prepoznavanje govora se izvodi na serveru, pa zahtijeva internet. Sve ostalo u formi radi i bez signala.',
      },
    ],
    learningCategory: LearningCategory.SezonskiRadovi,
  },

  '/inspections/:id/edit': {
    title: 'Uredi pregled',
    summary: 'Ispravite podatke već zabilježenog pregleda.',
    tips: ['Ako ste pregled unijeli na pogrešnoj košnici, obrišite ga i unesite na pravoj — košnica se ne može premjestiti.'],
  },

  // ── Feeding ─────────────────────────────────────────────────────────────────
  '/feedings': {
    title: 'Prehrana',
    summary:
      'Svi programi prehrane po pčelinjacima. Jedan program pokriva onoliko košnica koliko ste odabrali — ne pravi se posebno za svaku.',
    steps: [
      'Novi program dodajete dugmetom „Dodaj prehranu“.',
      'Odaberite pčelinjak, pa čekirajte košnice na koje se program odnosi.',
      'Otvorite program da označite obavljene runde ili promijenite spisak košnica.',
    ],
    tips: [
      'Jedna runda = jedan odlazak na pčelinjak. Označava se jednom, ne po košnici.',
      'Runde se prikazuju u kalendaru — jedna obaveza po datumu, bez obzira na broj košnica.',
      'Košnicu možete izbaciti iz programa usred trajanja; ostaje zapisano da je do tada bila hranjena.',
    ],
    notesByRole: {
      Beekeeper: 'Vi vidite programe koji obuhvataju vaše košnice i možete označiti obavljenu rundu, ali programe ne unosite ni mijenjate.',
    },
    learningCategory: LearningCategory.SezonskiRadovi,
  },

  '/feedings/new': {
    title: 'Novi program prehrane',
    summary:
      'Program prehrane je plan hranjenja za pčelinjak: šta dajete, koliko, koliko često i koliko dugo. Runde se generišu same.',
    steps: [
      'Odaberite pčelinjak, pa čekirajte košnice — po pravilu su sve već označene.',
      'Odaberite vrstu hrane i razlog hranjenja.',
      'Unesite datum početka, trajanje i koliko dana je između hranjenja.',
      'Sačuvajte — runde se izračunaju automatski.',
    ],
    tips: [
      'Količinu po košnici unesite s jedinicom; napomena („1:1“, „pola pogače“) nosi ono što broj ne može.',
      'Runde se prikazuju u kalendaru, pa ne morate pamtiti kada je naredno hranjenje.',
      'Program možete prekinuti ranije — traži se komentar, da poslije znate zašto je prekinut.',
    ],
    learningCategory: LearningCategory.SezonskiRadovi,
  },

  '/feedings/:id/edit': {
    title: 'Uredi program prehrane',
    summary: 'Mijenjajte parametre programa prehrane.',
    tips: [
      'Spisak košnica se ne mijenja ovdje nego na stranici programa — tamo ostaje zapisano kada je koja košnica dodana ili izbačena.',
      'Završen ili prekinut program se ne može mijenjati — to je namjerno, da historija ostane vjerodostojna.',
    ],
  },

  '/feedings/:id': {
    title: 'Program prehrane',
    summary: 'Pojedinačni program: košnice koje obuhvata i sve runde hranjenja s njihovim statusom.',
    steps: [
      'Kad obavite hranjenje, označite tu rundu kao obavljenu — jednom za cijelu grupu.',
      'Košnice dodajete ili izbacujete u sekciji „Košnice“.',
      'Ako prekidate program prije vremena, koristite „Zaustavi ranije“ i upišite razlog.',
    ],
    tips: [
      'Uz označenu rundu možete upisati napomenu — npr. da su dvije košnice preskočene.',
      'Označene runde nestaju iz kalendara, pa vam kalendar ostaje čist i pokazuje samo ono što još treba.',
    ],
  },

  // ── Harvests ────────────────────────────────────────────────────────────────
  '/harvests': {
    title: 'Vrcanja',
    summary:
      'Evidencija izvrcanog meda po pčelinjaku i po košnici — osnova za prinos, profitabilnost i statistiku.',
    steps: [
      'Dodajte vrcanje dugmetom „Novo vrcanje“.',
      'Unesite kilograme po košnici — po tome se računa prinos svake kolonije.',
    ],
    tips: [
      'Unesite vrstu meda: statistika prikazuje prinos po vrsti i po pašnjaku.',
      'Ako vrcate u toku karence nakon tretmana, aplikacija će vas upozoriti. Upozorenje ne blokira unos, ali ga pročitajte.',
    ],
    notesByRole: {
      Beekeeper: 'Vi vidite vrcanja koja uključuju vaše košnice, ali ih ne možete mijenjati.',
    },
  },

  '/harvests/new': {
    title: 'Novo vrcanje',
    summary: 'Zabilježite koliko ste meda izvrcali i iz kojih košnica.',
    steps: [
      'Odaberite pčelinjak i datum vrcanja.',
      'Unesite kilograme za svaku košnicu iz koje ste vrcali.',
      'Odaberite vrstu meda i sačuvajte.',
    ],
    tips: [
      'Pčelinjak se ne može mijenjati nakon spremanja — provjerite ga prije.',
      'Košnice iz kojih niste vrcali ostavite prazne.',
    ],
  },

  '/harvests/:id/edit': {
    title: 'Uredi vrcanje',
    summary: 'Ispravite kilograme ili vrstu meda.',
    tips: ['Uređivanje zamjenjuje cijeli set unosa po košnicama, pa provjerite sve redove prije spremanja.'],
  },

  // ── Treatments ──────────────────────────────────────────────────────────────
  '/treatments': {
    title: 'Tretmani',
    summary:
      'Zakonska evidencija primjene lijekova (varoa i ostalo). Ovo nije samo bilježnica — ovo je registar koji se pokazuje inspekciji.',
    steps: [
      'Novi tretman dodajete dugmetom „Novi tretman“.',
      'Označite košnice na kojima ste tretman primijenili.',
      'PDF registar po pčelinjaku i godini izvozite dugmetom za PDF.',
    ],
    tips: [
      'Karenca se računa sama iz preparata i datuma — ne unosite je ručno.',
      'Status ide „U toku“ → „Karenca“ → „Završen“ i također se izvodi iz datuma.',
      'Ako ste koristili trake, aplikacija će vas upozoriti kad predugo stoje u košnici.',
      'Unesite LOT broj preparata — bez njega evidencija nije potpuna za inspekciju.',
    ],
    notesByRole: {
      Beekeeper: 'Vi možete pregledati tretmane na svojim košnicama, ali ih ne možete unositi ni mijenjati — to je zakonska evidencija koju vodi administrator.',
    },
    learningCategory: LearningCategory.Propisi,
  },

  '/treatments/new': {
    title: 'Novi tretman',
    summary: 'Unesite primjenu lijeka — preparat, doza, LOT, metoda i košnice.',
    steps: [
      'Odaberite pčelinjak i datum primjene.',
      'Odaberite preparat (postoje pripremljeni predlošci) ili unesite svoj.',
      'Označite košnice na kojima je primijenjen i sačuvajte.',
    ],
    tips: [
      'Predlošci preparata popunjavaju aktivnu supstancu, metodu i karencu — koristite ih kad možete, manje je greške.',
      'LOT broj se traži zbog propisa o sljedivosti lijeka. Prepišite ga s pakovanja.',
      'Pčelinjak se ne može promijeniti nakon spremanja.',
    ],
    learningCategory: LearningCategory.Propisi,
  },

  '/treatments/:id/edit': {
    title: 'Uredi tretman',
    summary: 'Ispravite podatke tretmana.',
    tips: ['Uređivanje zamjenjuje cijeli set košnica, pa provjerite koje su označene prije spremanja.'],
  },

  // ── Expenses ────────────────────────────────────────────────────────────────
  '/expenses': {
    title: 'Troškovi',
    summary: 'Evidencija ulaganja — šećer, lijekovi, oprema, gorivo. Uparuje se s prinosom u statistici.',
    steps: [
      'Dodajte trošak dugmetom „Novi trošak“.',
      'Ili skenirajte račun — aplikacija pokuša sama pročitati stavke.',
    ],
    tips: ['Trošak može imati više stavki, pa jedan račun ne morate razbijati na više unosa.'],
  },

  '/expenses/new': {
    title: 'Novi trošak',
    summary: 'Unesite trošak s jednom ili više stavki.',
    tips: ['Datum i kategorija su ono što kasnije koristite za filtriranje i statistiku — nemojte ih preskočiti.'],
  },

  '/expenses/:id/edit': {
    title: 'Uredi trošak',
    summary: 'Mijenjajte stavke i iznose troška.',
  },

  '/expenses/scan': {
    title: 'Skeniranje računa',
    summary:
      'Fotografišite ili priložite račun i aplikacija pokuša prepoznati stavke, da ih ne prepisujete rukom.',
    steps: [
      'Priložite fotografiju računa.',
      'Sačekajte prepoznavanje teksta.',
      'Provjerite i ispravite prepoznate stavke, pa sačuvajte.',
    ],
    tips: [
      'Prepoznavanje se izvodi na vašem uređaju, račun ne ide nikuda van.',
      'Uvijek provjerite iznose — prepoznavanje s ispisanih računa nije nikad 100 % tačno.',
      'Ravan, dobro osvijetljen račun bez sjene daje daleko bolji rezultat.',
    ],
  },

  // ── Pastures ────────────────────────────────────────────────────────────────
  '/pastures': {
    title: 'Pašnjaci i selidbe',
    summary:
      'Registar lokacija na koje selite pčelinjake, plus historija selidbi. Prinos se pripisuje pašnjaku na kojem je pčelinjak bio na dan vrcanja.',
    steps: [
      'Dodajte pašnjak i postavite mu lokaciju na mapi.',
      'Pčelinjak preseljavate iz same stranice pčelinjaka, dugmetom „Preseli“.',
    ],
    tips: [
      'Selidba prepisuje koordinate pčelinjaka, pa prognoza, upozorenja na mraz i mape same prate selidbu.',
      'Unesite broj veterinarske svjedodžbe po selidbi ako je imate — to je zakonski dokument.',
      'Pašnjak se ne može obrisati dok postoji selidba koja ga koristi. Briše se samo posljednja selidba, kao ispravka.',
    ],
    learningCategory: LearningCategory.Napredno,
  },

  // ── Members ─────────────────────────────────────────────────────────────────
  '/members': {
    title: 'Članovi',
    summary:
      'Ljudi u vašoj organizaciji i to šta smiju vidjeti. Ovdje pravite račune za pčelare i administratore pčelinjaka.',
    steps: [
      'Napravite račun članu — dobija e-poštu s pristupom.',
      'Administratoru pčelinjaka dodijelite pčelinjak.',
      'Pčelaru dodijelite pojedinačne košnice preko „Dodjele“.',
    ],
    tips: [
      'Pčelar vidi samo košnice koje mu izričito dodijelite. Bez dodjele mu je aplikacija prazna — to je najčešći uzrok pitanja „zašto ništa ne vidim“.',
      'Administrator pčelinjaka vidi cijeli svoj pčelinjak, ne mora mu se dodjeljivati košnica po košnici.',
      'Promjena role ili dodjele odjavljuje tog korisnika sa svih uređaja — mora se ponovo prijaviti. Tako je namjerno, iz sigurnosnih razloga.',
    ],
    faq: [
      {
        q: 'Koja je razlika između administratora pčelinjaka i pčelara?',
        a: 'Administrator pčelinjaka upravlja cijelim jednim pčelinjakom — može dodavati košnice, unositi tretmane i vrcanja. Pčelar radi na dodijeljenim košnicama: unosi preglede, ali ne vodi tretmane ni vrcanja.',
      },
    ],
  },

  '/members/:id/assignments': {
    title: 'Dodjela košnica',
    summary: 'Odredite koje košnice ovaj član vidi i na kojima radi.',
    tips: [
      'Bez ijedne dodijeljene košnice član se može prijaviti, ali mu je aplikacija prazna.',
      'Promjena dodjele važi odmah, bez potrebe da se član ponovo prijavljuje.',
    ],
  },

  // ── Calendar ────────────────────────────────────────────────────────────────
  '/calendar': {
    title: 'Kalendar',
    summary: 'Sve što vas čeka na jednom mjestu: termini hranjenja i zadaci, po datumima.',
    tips: [
      'Kalendar ne unosite ručno — puni se sam iz programa prehrane i zadataka.',
      'Ako je termin prošao a nije označen kao obavljen, ostaje prikazan. To je namjerno, da ne ispadne iz vida.',
      'Obaveze možete prebaciti u svoj kalendar na telefonu — pogledajte „Sinhronizacija“.',
    ],
  },

  '/calendar/settings': {
    title: 'Sinhronizacija kalendara',
    summary:
      'Prebacite obaveze iz Melariuma u kalendar koji već koristite (Google, Outlook, telefon), da ih vidite bez otvaranja aplikacije.',
    steps: [
      'Kopirajte privatni link kalendara.',
      'Dodajte ga u svoj kalendar kao pretplatu na kalendar.',
    ],
    tips: [
      'Link je privatan i tajan — svako ko ga ima vidi vaše obaveze. Ne dijelite ga i ne objavljujte.',
      'Vanjski kalendari osvježavaju pretplatu po svom rasporedu, obično na nekoliko sati. Promjena se ne vidi u sekundi.',
    ],
  },

  // ── Advisor ─────────────────────────────────────────────────────────────────
  '/advisor': {
    title: 'AI Savjetnik',
    summary:
      'Pitajte na bosanskom o pčelarstvu. Kad razgovor otvorite iz košnice, savjetnik poznaje njene stvarne podatke.',
    steps: [
      'Napišite pitanje ili ga izgovorite.',
      'Za savjet o konkretnoj koloniji, pokrenite razgovor iz stranice te košnice („Pitaj savjetnika“).',
    ],
    tips: [
      'Razgovor pokrenut iz košnice vidi njene preglede, prehranu, maticu, prinos, zadnji tretman i prognozu — pa odgovor nije opšta priča.',
      'Razgovori su lični, drugi članovi organizacije ih ne vide.',
      'Savjetnik nije veterinar. Kod sumnje na bolest koja se prijavljuje, pozovite veterinara.',
    ],
    faq: [
      {
        q: 'Zašto savjetnik ne zna nešto o mojoj košnici?',
        a: 'Zna samo ono što je u aplikaciji zabilježeno. Ako pregledi nisu unošeni, nema iz čega da zaključuje.',
      },
    ],
  },

  // ── Learning ────────────────────────────────────────────────────────────────
  '/learning': {
    title: 'Edukacija',
    summary: 'Tekstovi o pčelarstvu, razvrstani po temama i po mjesecu u kojem su aktuelni.',
    tips: [
      'Sekcija „Aktuelno“ pokazuje ono što je bitno za tekući mjesec.',
      'Pročitane teme se označavaju same, da znate gdje ste stali.',
    ],
  },

  '/learning/:id': {
    title: 'Edukativna tema',
    summary: 'Jedan tekst iz Edukacije.',
    tips: [
      'Dugme „Poslušaj“ čita tekst naglas — korisno kad su vam ruke zauzete.',
      'Tema se označava pročitanom nakon nekoliko sekundi čitanja.',
    ],
  },

  // ── Stats ───────────────────────────────────────────────────────────────────
  '/stats': {
    title: 'Statistike',
    summary: 'Brojevi vaše sezone: prinos, pregledi, temperature, najbolje košnice, troškovi.',
    tips: [
      'Grafikoni se računaju iz onoga što ste unijeli. Prazni grafikon obično znači da podaci za taj period nisu unošeni, a ne da je bilo nula.',
      'Prinos po pašnjaku pripisuje med lokaciji na kojoj je pčelinjak bio na dan vrcanja.',
    ],
  },

  // ── Outbox ──────────────────────────────────────────────────────────────────
  '/outbox': {
    title: 'Neposlani pregledi',
    summary:
      'Pregledi koje ste unijeli bez signala. Čekaju ovdje i šalju se sami kad se vratite u mrežu — nisu izgubljeni.',
    steps: [
      'Kad ste u mreži, obično ne treba ništa raditi — pošalje se samo.',
      'Ako želite odmah, pritisnite „Pošalji sada“.',
      'Ako je slanje odbijeno, uredite unos i pošaljite ponovo.',
    ],
    tips: [
      'Crveni status znači da je server odbio unos (npr. košnica je u međuvremenu obrisana). Otvorite ga i ispravite.',
      'Ne brišite unos ako ga niste prepisali negdje — brisanjem se pregled trajno gubi.',
    ],
  },

  // ── Plans / profile ─────────────────────────────────────────────────────────
  '/plans': {
    title: 'Paketi i pretplata',
    summary: 'Šta vaš paket uključuje i koliko ste od limita iskoristili.',
    tips: [
      'Novi računi dobijaju 30 dana Pro paketa, da sve isprobate.',
      'Ako paket istekne, ništa se ne briše. Samo ne možete dodavati nove stavke dok ne obnovite.',
    ],
  },

  '/profile': {
    title: 'Moj profil',
    summary: 'Vaše ime, e-pošta, lozinka i pregled poslanih povratnih informacija.',
    tips: [
      'Promjena lozinke vas odjavljuje sa svih uređaja — tako je namjerno, da promjena lozinke stvarno izbaci nekog nepoželjnog.',
      'Potvrdite e-poštu. Bez potvrde ne možete vratiti pristup računu ako zaboravite lozinku.',
    ],
  },

  // ── SystemAdmin ─────────────────────────────────────────────────────────────
  '/admin': {
    title: 'Kontrolna ploča',
    summary: 'Platformski pregled: organizacije i korisnici.',
    tips: ['Promjena role, organizacije ili pčelinjaka korisniku odjavljuje tog korisnika sa svih uređaja.'],
  },

  '/admin/learning-topics': {
    title: 'Edukacija — teme',
    summary: 'Pisanje i objava edukativnih tema koje vide svi korisnici platforme.',
    tips: [
      'Prva objava teme pošalje obavijest svim korisnicima. Kasnije izmjene ne — pa provjerite tekst prije prve objave.',
      'Mjeseci određuju kada se tema pojavljuje u „Aktuelno“. Bez mjeseci tema je stalno aktuelna.',
    ],
  },

  '/admin/feedback': {
    title: 'Povratne informacije',
    summary: 'Sve što su korisnici poslali — problemi, žalbe, pohvale, prijedlozi i pitanja.',
    steps: [
      'Otvorite prijavu klikom na nju.',
      'Postavite status i po želji napišite odgovor.',
      'Sačuvajte — korisnik dobija obavijest i e-poštu s vašim odgovorom.',
    ],
    tips: [
      'Prijava nosi stranicu s koje je poslana i podatke o pregledniku, pa problem ne morate rekonstruisati iz opisa.',
      'Brojka na meniju pokazuje koliko je prijava još neobrađeno.',
    ],
  },
}
