# Brisanje računa i prenos vlasništva

> Status: ✅ Implemented (2026-08-31). Izdvojeno iz [SPEC-23](../specs/SPEC-23-mobile-apps.md) i
> isporučeno prije njega, jer su obje stvari korisne na webu same po sebi.

Do ovoga korisnik nije mogao obrisati svoj račun nigdje u aplikaciji — jedini način je bio pisati
Asimu, koji bi to uradio kroz `/admin`. Prodavnice aplikacija to traže kao ugrađenu funkciju
(Apple 5.1.1(v), Google Play), ali razlog zašto je napisano ovako nije prodavnica nego to što jedan
korisnik ne smije moći obrisati tuđi rad.

## Tri ishoda, i zašto baš tri

| Ko briše | Šta se dešava |
|---|---|
| ApiaryAdmin / pčelar | Briše se korisnik i njegovi lični zapisi. Podaci organizacije ostaju. |
| OrganizationAdmin **sam** u organizaciji | Briše se korisnik **i organizacija sa svim podacima**, uz potvrdu upisivanjem naziva. |
| OrganizationAdmin **s članovima** | Odbijeno dok ne prenese vlasništvo. |

Ono što je lako "pojednostaviti" u pogrešnom smjeru: pravilo **nije** "zadnji admin briše
organizaciju", nego "OrganizationAdmin koji je zadnji član briše organizaciju". Usamljeni pčelar u
organizaciji koju je napravio i vodi SystemAdmin **ne** ruši tu organizaciju kad ode — ti zapisi
nisu njegovi da ih briše.

I obrnuto: admin s pet pčelara ne smije jednim dugmetom uništiti njihove pčelinjake, njihove
preglede i njihove naloge za prijavu. Zato treći red postoji, i zato prenos vlasništva postoji —
Apple traži da brisanje bude *uvijek* moguće, pa trajna zabrana bez izlaza ne bi bila rješenje.

Pravilo živi u **jednoj** privatnoj metodi `ProfileService.ResolveDeletionMode`, koju zovu i pregled
i samo brisanje. Pregled koji kaže `account` dok brisanje odluči `organization` obrisao bi
organizaciju o kojoj niko nije upozoren — zato je test `Preview_AndDeletion_AgreeOnTheMode`.

## Šta nestaje, a šta ostaje

Kaskada briše: sesije, notifikacije, oznake pročitanog, historiju AI asistenta, postavke kalendara i
dodjele košnica.

`SetNull` čuva, anonimizirano: prijave problema, pozivnice, i `CreatedBy` na pčelinjacima, košnicama,
vrcanjima, tretmanima, troškovima, prehranama, selidbama i sastavljanjima. To nije nova odluka —
tako su te veze bile konfigurisane i prije ove funkcije; ovo je prvo mjesto koje ih koristi.

Pčelarski podaci (`Inspection`, `Harvest`, `Beehive`, `Apiary`, `TreatmentEntry`) **nemaju strani
ključ na korisnika** — vise o organizaciji, pa ih brisanje računa ne može dotaći.

### Jedan strani ključ koji bi srušio brisanje

`Todo.AssignedToId` je konfigurisan sa `DeleteBehavior.NoAction`, pa PostgreSQL odbije brisanje
umjesto da ga očisti. Zato `DeleteMyAccountAsync` **prvo** oslobodi dodjele zadataka. Isti kvar i
dalje postoji u `AdminService.DeleteUserAsync` i **nije** popravljen ovdje — vidi SPEC-16 Fazu C.

### Atomičnost

Kad ide i organizacija, oba brisanja idu u **jedan** `SaveChanges`. `Organization.Users` je
`Restrict`, a EF briše zavisne prije glavnih, pa je redoslijed već ispravan. Dva odvojena spremanja
mogla bi ostaviti obrisanog korisnika pored organizacije do koje niko ne može doći.

### Registar tretmana

Brisanje organizacije kaskadira u pčelinjake i time u **registar tretmana** — evidenciju koju je
SPEC-19 posebno štitio od nestanka. To je ovdje ispravno, jer korisnik traži da njegovi podaci odu,
ali mora biti **napisano u potvrdi**, ne otkriveno poslije. Zato `deletesTreatmentRegister` postoji
kao zasebno polje, a ne kao nešto što klijent izvodi iz `mode`.

## Lozinka

Brisanje traži ponovni unos lozinke. Bez toga je otključan telefon ostavljen na stolu dva dodira od
uništenja računa. Pogrešna lozinka je `422`, istog oblika i cijene kao svako drugo odbijanje —
`BusinessRuleException`, kao i kod promjene lozinke na profilu.

Zahtjev nosi lozinku u **tijelu** `DELETE` poziva. Alternativa (lozinka u query stringu) završila bi
u logovima servera i historiji preglednika.

## Prenos vlasništva

Organizacija ima **tačno jednog** `OrganizationAdmin`-a i prije ove funkcije nije postojao način da
se napravi drugi: `MembersPage` tipizira ulogu člana kao `'ApiaryAdmin' | 'Beekeeper'`, a
`OrgManagementService` nije imao nijednu metodu za promjenu uloge.

- Nasljednik postaje `OrganizationAdmin`, `ApiaryId` mu se briše (samo `ApiaryAdmin` smije nositi
  pčelinjak — pravilo `ValidateRoleOrgApiaryConsistency`).
- Dosadašnji admin postaje **`Beekeeper`**, ne `ApiaryAdmin`: ta uloga mora biti vezana za konkretan
  pčelinjak, a nema poštenog načina da se izabere koji. Kao pčelar bez dodjela ne vidi ništa dok mu
  novi vlasnik ne da pristup — što i jeste smisao predaje organizacije.
- **Obje sesije se ukidaju** (`ISessionRevoker`), jer su uloga, organizacija i pčelinjak JWT claimovi.
  Frontend odmah odjavljuje korisnika: pristupni token još do 30 minuta nosi staru ulogu, pa čekanje
  na prvi `401` ne bi bilo isto.
- `memberId` iz druge organizacije vraća **404**, ne 403 — inače endpoint odgovara na pitanje
  "postoji li korisnik 412?" bilo kome.

Uzgredna korist koja nije bila motiv: rješava i "šta ako vlasnik napusti gazdinstvo", gdje je
organizacija do sad ostajala bez ikoga ko je može voditi.

## Gdje je u aplikaciji

- **Profil → Brisanje računa** (`DeleteAccountSection`) — posljednja sekcija na stranici namjerno:
  niko ne treba sresti to dugme na putu do promjene broja telefona.
- **Članovi → ikona krune** uz člana (`MembersPage`) — vidi je samo OrganizationAdmin, i samo uz
  članove koji to već nisu.

Oba dijaloga traže potvrdu prepisivanjem (naziv organizacije, odnosno ime člana) i drže dugme
onemogućenim dok se ne poklopi.

## Testovi

`AccountDeletionTests` (14) i `TransferOwnershipTests` (8). Zaključavaju upravo ono što bi bilo tiho
ili nepovratno da se pokvari: ko ruši organizaciju a ko ne, oslobađanje zadataka prije brisanja,
jedan `SaveChanges`, zaštita zadnjeg SystemAdmina, i da pregled i brisanje ne mogu odlučiti različito.
