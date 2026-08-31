import { Link } from 'react-router-dom'
import { ShieldCheck } from 'lucide-react'
import { CONTACT_EMAIL, CONTACT_PHONE_DISPLAY, CONTACT_PHONE_E164 } from '../../core/contact/contactInfo'
import { LegalPage, LegalSection, LegalSubTitle } from './LegalPage'

/**
 * Politika privatnosti — a public page, reachable without signing in (see `LegalPage`).
 *
 * Everything below describes what the code actually does: the third parties are the ones the
 * backend and the frontend really call (Groq, Resend, Open-Meteo, OpenStreetMap), and the storage
 * claims match `authService` and the outbox. If any of that changes, this page changes with it —
 * a privacy policy describing an older version of the app is worse than none.
 */

/** Last substantive change to the text below. Shown to the reader — keep it honest. */
const LAST_UPDATED = '31. augusta 2026.'

export default function PrivacyPolicyPage() {
  return (
    <LegalPage
      title="Politika privatnosti"
      icon={<ShieldCheck className="w-6 h-6 text-honey-600 dark:text-honey-400" />}
      lastUpdated={LAST_UPDATED}
    >
      <LegalSection title="Ukratko">
        <p>
          Melarium je aplikacija za vođenje pčelarske evidencije. Vaše podatke koristimo da bi
          aplikacija radila — ništa ne prodajemo, ne razmjenjujemo s oglašivačima i ne koristimo
          za praćenje po drugim stranicama. Nemamo kolačiće za analitiku ni reklamne mreže.
        </p>
      </LegalSection>

      <LegalSection title="1. Ko obrađuje vaše podatke">
        <p>
          Voditelj obrade je <strong>Asim Haskić</strong>, fizičko lice koje pruža uslugu putem web
          aplikacije melarium.app. Možete nam se obratiti u vezi s bilo kojim pitanjem o vašim
          podacima:
        </p>
        <ul className="mt-2 space-y-1">
          <li>E-pošta: <a className="link" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a></li>
          <li>Telefon: <a className="link" href={`tel:${CONTACT_PHONE_E164}`}>{CONTACT_PHONE_DISPLAY}</a></li>
        </ul>
        <p className="mt-2">Odgovaramo u roku od 24 sata.</p>
      </LegalSection>

      <LegalSection title="2. Koje podatke prikupljamo">
        <LegalSubTitle>Podaci o računu</LegalSubTitle>
        <p>
          Ime i prezime, e-pošta, broj telefona i lozinka (koju čuvamo isključivo u obliku
          kriptografskog sažetka, nikada kao tekst). Uz to vaša uloga i organizacija kojoj
          pripadate.
        </p>

        <LegalSubTitle>Pčelarski podaci koje sami unosite</LegalSubTitle>
        <p>
          Pčelinjaci i njihove koordinate, košnice, pregledi i fotografije pregleda, vrcanja,
          matice, tretmani, prehrana, pašnjaci i selidbe, troškovi i fotografije računa, zadaci i
          bilješke. Ovi podaci pripadaju <strong>organizaciji</strong> u kojoj radite, a ne
          pojedinačnom korisniku.
        </p>

        <LegalSubTitle>Tehnički podaci</LegalSubTitle>
        <p>
          Tokeni sesije (čuvamo ih u obliku sažetka), notifikacije, historija razgovora s AI
          asistentom, oznake pročitanog za edukaciju i objave, te postavke kalendara. Kada nam
          pošaljete prijavu problema, uz nju se sprema i stranica s koje je poslana i podatak o
          vašem pregledniku — da bismo grešku mogli ponoviti.
        </p>
        <p className="mt-2">
          IP adresu koristimo privremeno, u trenutku zahtjeva, za ograničavanje broja pokušaja
          prijave i sličnih zloupotreba. Ne spremamo je u bazu.
        </p>
      </LegalSection>

      <LegalSection title="3. Zašto ih obrađujemo">
        <ul className="space-y-1.5 list-disc pl-5">
          <li><strong>Izvršenje ugovora</strong> — da biste mogli koristiti aplikaciju: vaš račun, vaša evidencija, vaše notifikacije.</li>
          <li><strong>Zakonska obaveza</strong> — registar tretmana je evidencija koju ste kao pčelar dužni voditi.</li>
          <li><strong>Legitimni interes</strong> — sigurnost računa, sprječavanje zloupotrebe i otklanjanje kvarova.</li>
          <li><strong>Saglasnost</strong> — za pristup kameri, mikrofonu i lokaciji, koje uređaj traži posebno i koje možete uskratiti ili kasnije povući.</li>
        </ul>
      </LegalSection>

      <LegalSection title="4. Kome ih prosljeđujemo">
        <p>
          Podatke ne prodajemo. Prosljeđujemo ih samo servisima bez kojih pojedine funkcije ne bi
          radile, i samo u obimu koji je toj funkciji potreban:
        </p>
        <div className="mt-3 overflow-x-auto">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="text-left text-gray-500 dark:text-slate-400 border-b border-gray-200 dark:border-slate-700">
                <th className="py-2 pr-4 font-medium">Servis</th>
                <th className="py-2 pr-4 font-medium">Šta dobija</th>
                <th className="py-2 font-medium">Zašto</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
              <tr>
                <td className="py-2.5 pr-4 align-top font-medium">Groq</td>
                <td className="py-2.5 pr-4 align-top">Tekst vaših naredbi i pitanja, glasovne snimke koje diktirate, fotografije pregleda i računa koje šaljete na analizu</td>
                <td className="py-2.5 align-top">AI asistent, prepoznavanje govora i analiza fotografija</td>
              </tr>
              <tr>
                <td className="py-2.5 pr-4 align-top font-medium">Resend</td>
                <td className="py-2.5 pr-4 align-top">Vaša e-pošta i sadržaj poruke</td>
                <td className="py-2.5 align-top">Slanje e-pošte (potvrda adrese, reset lozinke, obavijesti)</td>
              </tr>
              <tr>
                <td className="py-2.5 pr-4 align-top font-medium">Open-Meteo</td>
                <td className="py-2.5 pr-4 align-top">Koordinate pčelinjaka</td>
                <td className="py-2.5 align-top">Vremenska prognoza i upozorenja o mrazu</td>
              </tr>
              <tr>
                <td className="py-2.5 pr-4 align-top font-medium">OpenStreetMap</td>
                <td className="py-2.5 pr-4 align-top">IP adresa vašeg uređaja, jer se karta učitava direktno iz preglednika</td>
                <td className="py-2.5 align-top">Prikaz karte pri odabiru lokacije</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p className="mt-3">
          Podaci se čuvaju na serveru u Evropskoj uniji. Navedeni servisi mogu obrađivati podatke
          i izvan EU, u skladu s vlastitim uslovima.
        </p>
        <p className="mt-2">
          Podatke ćemo otkriti i kada to od nas zatraži nadležni organ na osnovu zakona.
        </p>
      </LegalSection>

      <LegalSection title="5. Kolačići i pohrana u pregledniku">
        <p>
          Ne koristimo kolačiće za praćenje ni analitiku, pa nema ni banera za pristanak.
          Aplikacija u vašem pregledniku čuva samo ono što joj treba da radi: token prijave,
          izabranu temu i postavke pomoći. Ako unosite preglede bez signala, oni se do
          sinhronizacije čuvaju lokalno na vašem uređaju.
        </p>
      </LegalSection>

      <LegalSection title="6. Koliko dugo čuvamo podatke">
        <p>
          Podatke vašeg računa i vaše evidencije čuvamo dok postoji vaš račun, odnosno
          organizacija kojoj pripadaju. Tokeni sesije ističu automatski (prijava nakon 30 minuta,
          obnova nakon 14 dana). Kada obrišete račun, brisanje je opisano u sljedećoj tački.
        </p>
      </LegalSection>

      <LegalSection title="7. Vaša prava">
        <p>Imate pravo:</p>
        <ul className="mt-2 space-y-1.5 list-disc pl-5">
          <li>zatražiti uvid u podatke koje o vama imamo i njihovu kopiju,</li>
          <li>ispraviti netačne podatke — ime, e-poštu i telefon mijenjate sami na stranici profila,</li>
          <li>zatražiti brisanje,</li>
          <li>uložiti prigovor na obradu i zatražiti ograničenje obrade,</li>
          <li>povući saglasnost za kameru, mikrofon i lokaciju, u postavkama uređaja.</li>
        </ul>
        <p className="mt-3">
          <strong>Brisanje računa radite sami</strong>, u aplikaciji: <em>Profil → Brisanje
          računa</em>. Brišu se vaš račun i vaši lični podaci — prijave, notifikacije, razgovori
          s AI asistentom i vaše dodjele košnica. Podaci organizacije ostaju, jer pripadaju
          organizaciji, a zapisi o tome ko je šta unio postaju anonimni.
        </p>
        <p className="mt-2">
          Ako ste jedini član svoje organizacije, s vašim računom briše se i cijela organizacija
          sa svim podacima, uključujući registar tretmana. Prije potvrde vam tačno prikažemo šta
          se briše.
        </p>
        <p className="mt-2">
          Za sva ostala prava pišite nam na{' '}
          <a className="link" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>. Odgovaramo u
          roku od 24 sata, a zahtjev rješavamo najkasnije u roku od 30 dana. Ako smatrate da
          vaše podatke obrađujemo protivno propisima, imate pravo podnijeti pritužbu nadležnom
          tijelu za zaštitu ličnih podataka.
        </p>
      </LegalSection>

      <LegalSection title="8. Sigurnost">
        <p>
          Sav promet ide preko šifrovane veze (HTTPS). Lozinke čuvamo kao BCrypt sažetke, a
          tokene za prijavu, potvrdu e-pošte i reset lozinke kao SHA-256 sažetke — što znači da
          ni mi ne možemo vidjeti vašu lozinku. Promjena lozinke odjavljuje sve uređaje.
          Pristup podacima organizacije ograničen je vašom ulogom.
        </p>
      </LegalSection>

      <LegalSection title="9. Djeca">
        <p>
          Melarium je namijenjen punoljetnim osobama i ne prikupljamo svjesno podatke djece.
        </p>
      </LegalSection>

      <LegalSection title="10. Izmjene ove politike">
        <p>
          Ako je promijenimo, ažuriramo datum na vrhu stranice, a o značajnim izmjenama
          obavijestićemo vas u aplikaciji. Uz ovu politiku vrijede i naši{' '}
          <Link className="link" to="/uslovi">uslovi korištenja</Link>.
        </p>
      </LegalSection>
    </LegalPage>
  )
}
