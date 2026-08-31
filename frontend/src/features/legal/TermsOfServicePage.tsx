import { Link } from 'react-router-dom'
import { ScrollText } from 'lucide-react'
import { CONTACT_EMAIL, CONTACT_PHONE_DISPLAY, CONTACT_PHONE_E164 } from '../../core/contact/contactInfo'
import { LegalPage, LegalSection } from './LegalPage'

/**
 * Uslovi korištenja — public, like the privacy policy and for the same reason: the stores open it
 * themselves, and so does someone deciding whether to register.
 *
 * Two sections carry real weight and are not boilerplate: the AI assistant is explicitly **not**
 * professional advice (it can propose a treatment, and a beekeeper who follows one blindly can
 * contaminate honey or break a withdrawal period), and the treatment register stays the user's own
 * legal obligation — Melarium is where you keep it, not who is responsible for it.
 *
 * Plan limits are deliberately **not** repeated here as numbers. They live in `Plans:` in
 * appsettings and are shown on `/plans`; a legal page restating them would be wrong the first time
 * a limit changes.
 */

/** Last substantive change to the text below. Shown to the reader — keep it honest. */
const LAST_UPDATED = '31. augusta 2026.'

export default function TermsOfServicePage() {
  return (
    <LegalPage
      title="Uslovi korištenja"
      icon={<ScrollText className="w-6 h-6 text-honey-600 dark:text-honey-400" />}
      lastUpdated={LAST_UPDATED}
    >
      <LegalSection title="Ukratko">
        <p>
          Melarium je alat za vođenje pčelarske evidencije. Vaši podaci su vaši, možete ih preuzeti i
          obrisati kad god želite. Naplata je ručna i godišnja — ništa vam se ne naplaćuje samo od
          sebe. AI asistent je pomoć, a ne stručni savjet, i odluke o košnicama ostaju vaše.
        </p>
      </LegalSection>

      <LegalSection title="1. Ko pruža uslugu">
        <p>
          Uslugu pruža <strong>Asim Haskić</strong> kao fizičko lice, putem web aplikacije na
          adresi melarium.app.
        </p>
        <ul className="mt-2 space-y-1">
          <li>E-pošta: <a className="link" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a></li>
          <li>Telefon: <a className="link" href={`tel:${CONTACT_PHONE_E164}`}>{CONTACT_PHONE_DISPLAY}</a></li>
        </ul>
      </LegalSection>

      <LegalSection title="2. Prihvatanje uslova">
        <p>
          Registracijom računa i korištenjem Melariuma prihvatate ove uslove. Ako se s njima ne
          slažete, nemojte koristiti uslugu. Uz ove uslove vrijedi i naša{' '}
          <Link className="link" to="/privatnost">politika privatnosti</Link>.
        </p>
      </LegalSection>

      <LegalSection title="3. Ko može koristiti uslugu">
        <p>
          Usluga je namijenjena punoljetnim osobama. Pri registraciji unosite tačne podatke i
          održavate ih ažurnim — na e-poštu koju navedete šaljemo potvrde, reset lozinke i obavijesti.
        </p>
      </LegalSection>

      <LegalSection title="4. Račun i organizacija">
        <p>
          Registracijom nastaje vaš račun i vaša <strong>organizacija</strong>, čiji ste
          administrator. Podaci koje unosite pripadaju organizaciji, a ne pojedinačnom korisniku —
          zato ostaju kada neki član ode.
        </p>
        <p className="mt-2">
          Odgovorni ste za čuvanje svoje lozinke i za sve što se dogodi pod vašim računom. Ako
          posumnjate da je neko drugi ima, odmah je promijenite — promjena lozinke odjavljuje sve
          uređaje.
        </p>
        <p className="mt-2">
          Kao administrator organizacije možete dodavati članove i određivati šta ko vidi. Za njihovo
          korištenje usluge odgovarate vi.
        </p>
      </LegalSection>

      <LegalSection title="5. Paketi, probni period i naplata">
        <ul className="space-y-1.5 list-disc pl-5">
          <li>
            Nova organizacija dobija <strong>probni period</strong>. Ako ste došli preko pozivnice
            drugog korisnika, probni period je duži.
          </li>
          <li>
            Nakon probnog perioda organizacija prelazi na besplatni paket, s manjim ograničenjima.
            <strong> Vaši podaci se ne brišu</strong> — ostaju vam dostupni.
          </li>
          <li>
            <strong>Naplata je ručna i godišnja.</strong> Nema automatske naplate, nema sačuvane
            kartice i nema automatske obnove. Paket aktiviramo nakon uplate, u pravilu u roku od
            jednog radnog dana.
          </li>
          <li>
            Aktuelni paketi i njihova ograničenja (broj košnica, broj članova, mjesečni broj AI
            upita) prikazani su na stranici <em>Paketi</em> u aplikaciji.
          </li>
          <li>
            Kad paket istekne, ograničenja se vraćaju na besplatni nivo. Ono što ste već unijeli
            ostaje vam dostupno za pregled i preuzimanje.
          </li>
          <li>
            Cijene i sastav paketa možemo mijenjati. Izmjena ne utiče na već plaćeni period.
          </li>
        </ul>
      </LegalSection>

      <LegalSection title="6. Vaš sadržaj">
        <p>
          Sve što unesete — pregledi, fotografije, vrcanja, tretmani, bilješke — ostaje vaše,
          odnosno vaše organizacije. Ne polažemo pravo vlasništva nad tim sadržajem.
        </p>
        <p className="mt-2">
          Dajete nam samo ono što je nužno da usluga radi: da vaše podatke pohranimo, prikažemo vama
          i članovima vaše organizacije, obradimo radi funkcija koje sami pokrenete (npr. slanje
          fotografije na AI analizu) i sigurnosno kopiramo.
        </p>
      </LegalSection>

      <LegalSection title="7. AI asistent — pomoć, ne stručni savjet">
        <p>
          Melarium koristi umjetnu inteligenciju za razumijevanje vaših naredbi, prepoznavanje
          govora, analizu fotografija i odgovaranje na pitanja. <strong>To nije veterinarski,
          agronomski niti bilo koji drugi stručni savjet.</strong>
        </p>
        <p className="mt-2">
          AI može pogriješiti, može pogrešno pročitati fotografiju ili krivo razumjeti izgovorenu
          rečenicu. Prije nego postupite po njegovom prijedlogu — posebno kada je riječ o{' '}
          <strong>tretmanima, dozama i karenci</strong> — provjerite podatke sami i pridržavajte se
          uputstva proizvođača preparata i važećih propisa. Odgovornost za odluke o vašim košnicama
          ostaje na vama.
        </p>
        <p className="mt-2">
          Prije nego što AI nešto upiše u vašu evidenciju, uvijek vam prikaže šta je razumio i traži
          potvrdu. Provjerite taj prikaz prije potvrde.
        </p>
      </LegalSection>

      <LegalSection title="8. Evidencija tretmana i zakonske obaveze">
        <p>
          Melarium vam olakšava vođenje registra tretmana i ispis PDF-a, ali{' '}
          <strong>zakonska obaveza vođenja i čuvanja evidencije ostaje vaša</strong>. Mi smo mjesto
          gdje je držite, ne onaj ko za nju odgovara.
        </p>
        <p className="mt-2">
          Preporučujemo da povremeno preuzmete PDF registra i sačuvate ga izvan aplikacije. Ako
          obrišete organizaciju, evidencija se briše s njom i ne može se vratiti.
        </p>
      </LegalSection>

      <LegalSection title="9. Šta nije dozvoljeno">
        <ul className="space-y-1.5 list-disc pl-5">
          <li>Dijeliti pristupne podatke s osobama izvan vaše organizacije.</li>
          <li>Pokušavati pristupiti tuđim podacima ili zaobići ograničenja paketa i uloga.</li>
          <li>Opterećivati uslugu automatiziranim zahtjevima ili je koristiti na način koji je ometa drugima.</li>
          <li>Unositi sadržaj koji je protivzakonit ili tuđ, a nemate pravo da ga koristite.</li>
          <li>Koristiti uslugu za bilo šta protivno propisima.</li>
        </ul>
      </LegalSection>

      <LegalSection title="10. Dostupnost usluge">
        <p>
          Trudimo se da Melarium radi bez prekida, ali ga ne možemo garantovati bez prestanka.
          Moguća su održavanja, nadogradnje i kvarovi, kao i ispadi servisa koje koristimo (AI,
          e-pošta, vremenska prognoza). Funkcije koje o njima ovise tada privremeno ne rade.
        </p>
        <p className="mt-2">
          Unos pregleda radi i bez signala i čuva se lokalno dok se veza ne vrati. Dok se ne
          sinhronizuje, taj unos postoji <strong>samo na vašem uređaju</strong> — ako u međuvremenu
          obrišete podatke preglednika ili aplikaciju, izgubljen je.
        </p>
      </LegalSection>

      <LegalSection title="11. Prestanak korištenja">
        <p>
          Račun možete obrisati sami: <em>Profil → Brisanje računa</em>. Prije potvrde vam tačno
          prikažemo šta se briše. Ako ste jedini član svoje organizacije, s računom se briše i ona,
          sa svim podacima.
        </p>
        <p className="mt-2">
          Mi možemo ograničiti ili ukinuti pristup ako se uslovi grubo krše — na primjer kod
          pokušaja pristupa tuđim podacima. U tom slučaju vas obavještavamo na e-poštu i, kad je to
          moguće, dajemo vam priliku da preuzmete svoje podatke.
        </p>
      </LegalSection>

      <LegalSection title="12. Odgovornost">
        <p>
          Melarium je alat za evidenciju. Ne odgovaramo za odluke koje donesete na osnovu podataka u
          aplikaciji, niti za štetu na pčelinjaku, gubitak prinosa ili posljedice pogrešno unesenih
          ili protumačenih podataka.
        </p>
        <p className="mt-2">
          Naša odgovornost je u svakom slučaju ograničena na iznos koji ste platili za paket u
          posljednjih 12 mjeseci. Ovo ograničenje ne isključuje odgovornost koja se po zakonu ne
          može isključiti.
        </p>
      </LegalSection>

      <LegalSection title="13. Izmjene uslova">
        <p>
          Uslove možemo mijenjati. Datum na vrhu stranice tada se ažurira, a o značajnim izmjenama
          obavještavamo vas u aplikaciji. Nastavak korištenja nakon izmjene znači da ih prihvatate.
        </p>
      </LegalSection>

      <LegalSection title="14. Mjerodavno pravo">
        <p>
          Na ove uslove primjenjuje se pravo Bosne i Hercegovine. Sporove prvo pokušavamo riješiti
          dogovorom — javite nam se prije nego što se obratite bilo kome drugom.
        </p>
      </LegalSection>

      <LegalSection title="15. Kontakt">
        <p>
          Za svako pitanje o ovim uslovima pišite na{' '}
          <a className="link" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a> ili nazovite{' '}
          <a className="link" href={`tel:${CONTACT_PHONE_E164}`}>{CONTACT_PHONE_DISPLAY}</a>.
          Odgovaramo u roku od 24 sata.
        </p>
      </LegalSection>
    </LegalPage>
  )
}
