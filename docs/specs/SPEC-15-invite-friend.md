# SPEC-15 — "Pozovi prijatelja" (Invite a friend)

| | |
|---|---|
| **Status** | 🔨 **Phase 1 implemented 2026-08-06** (link + attribution + reward, ADR-032, `features/invitations.md`) · Phase 2 (e-mail channel) planned |
| **Effort** | M — Phase 1 ~2 days, Phase 2 ~1.5 days |
| **Depends on** | nothing new. Reuses the email queue (ADR-021), `PlanHelper`/`PlanGuard` (SPEC-09), and the `CalendarSettings.FeedToken` "secret address" precedent |
| **New secrets / packages** | none. Config only: `Invitations:*` |
| **Breaking** | **No.** One new table, one nullable `User` column, one optional trailing `RegisterDto` parameter. Nothing is removed and no existing endpoint changes shape. |
| **Reserves** | `NotificationType.InvitationAccepted = 24` (verified free 2026-08-06: 22 = SPEC-13, 23 = SPEC-12) |
| **Rate-limit policy** | new `"invite"`, 3/min per IP (Phase 2 only) |

> **How this spec was written.** The product decisions in §1 were settled one at a time with the PO on
> 2026-08-06 before any of this was drafted; the technical shape in §3–§5 was agreed in the same pass,
> against the code as it stands on `main` that day. This document is the **record** of those decisions,
> not the place where they were made. Where a decision closed off an option, the option is named so a
> future reader knows it was considered.
>
> Every claim about existing code here was verified on 2026-08-06. This codebase moves week to week
> (`feat: login with phone` shipped 2026-07-31, SPEC-12 on 2026-08-05) — **re-check the cited lines
> before implementing**, and treat a mismatch as the code being right and this document being stale.

---

## 1. What was decided

| # | Decision | |
|---|---|---|
| D1 | **Scope** | Inviting people **to the platform**. The invitee gets their own organization. Adding a member to your *own* organization is a different feature and is **not** in this spec. |
| D2 | **Channel** | Personal **share link** (Viber/WhatsApp) **and** an emailed invitation. Link first — see D9. |
| D3 | **Personal message** | Allowed, ~200 chars, **no URLs**. |
| D4 | **What the invitee gets** | **60 days** of Pro trial instead of the standard 30, granted at registration. |
| D5 | **What the inviter gets** | **+30 days** on the plan their organization already has, granted when the invitee **verifies their email** — never at registration. |
| D6 | **Cap** | **180 days lifetime** per organization (6 successful invites), and at most **5 rewards per rolling 30 days**. |
| D7 | **Reward never touches `PlanNotes`** | The ledger is the invitation rows. See §3.2 for the concrete bug this avoids. |
| D8 | **Already-has-an-account** | Send a **different** email ("you already have an account, here's the login link"), never silence and never a lie in the UI. See §6.3. |
| D9 | **Phase order** | Phase 1 = link + attribution + reward. Phase 2 = the email channel. Each deploys independently. |

**The one invariant that outranks everything else in this spec:**

> **An unknown, expired or malformed referral code must never fail a registration.** If anything about
> the invitation is wrong, the person registers normally and gets the standard 30-day trial. Losing
> attribution is cheap; losing a sign-up is not.

---

## 2. The flow

**Inviter.** Profile menu → "Pozovi prijatelja" (`/invite`). One field for the friend's email, one optional short message, one button. Below it: their personal link with **Kopiraj** / **Podijeli**. Below that: "Poslano N · Pridružilo se M · Osvojeno +K dana" and the list of who they invited and where each one stands.

**Invitee.** Receives the mail (or the link over Viber) → clicks → `/register?ref=CODE` shows a banner naming the inviter → registers normally → gets **60 days** instead of 30 → verifies their email → **the inviter is rewarded** and gets a notification.

That notification is not decoration. It is the only thing that closes the loop and produces a second invitation, so it ships in Phase 1:

- at acceptance — *"{Ime} se pridružio Melariumu preko vaše pozivnice."*
- at reward — wording derived from what was actually granted (§3.1), never a fixed string.

Use the invitee's **first name only**, never the address they registered with — if they signed up with a different address than the one invited, echoing it back leaks their private address to the inviter.

---

## 3. The reward

### 3.1 Where it lives, and the algorithm

Today `Organization.PlanValidUntil` has exactly **two** writers, both absolute assignment:
`AdminService.cs:79` (SystemAdmin, manual) and `AuthService.cs:110` (the registration trial). The
reward is the **third writer and the first that does arithmetic on an existing value** — which is
where all the risk in this feature sits.

It therefore does **not** live in the invitation feature. It goes next to `PlanGuard`, which already
owns plan semantics:

```csharp
// Melarium.Application/Common/Security/ — beside PlanGuard
Task<PlanCreditResult> GrantDaysAsync(int organizationId, int days);

public readonly record struct PlanCreditResult(int DaysGranted, PlanType ResultingPlan, bool WasUpgrade);
```

```
GrantDays(org, days, now):

  // Lifetime plan (bez isteka — Partner / early adopters). There is nothing to extend:
  // writing today+days here would CONVERT AN UNLIMITED PLAN INTO ONE THAT EXPIRES.
  if (org.PlanValidUntil is null && org.Plan != Free)
      → DaysGranted = 0, nothing changes

  effective = PlanHelper.Effective(org.Plan, org.PlanValidUntil, now)

  if (effective == Free)                 // expired or genuinely Free → this is an upgrade
      org.Plan           = Pro
      org.PlanValidUntil = now.Date.AddDays(days)

  else                                   // trial or paying customer → extend only
      org.PlanValidUntil = Max(org.PlanValidUntil.Value, now.Date).AddDays(days)
```

Three details that are not stylistic:

- **The condition is `effective == Free`, not an ordinal comparison.** `PlanType` is
  `Free=1, Standard=2, Pro=3, Max=4, Partner=5`, so writing `Plan = Pro` on a Partner organization is
  a **downgrade from 5 to 3**. A `Plan < Pro` test would do exactly that.
- **`Max(existing, today)`** — an organization whose plan expired two days ago must not receive
  30 days counted from two days ago.
- **The result drives the copy.** `DaysGranted == 0` → **no reward notification at all** (not "+0
  dana", which reads as a bug). `WasUpgrade` → *"Dobili ste 30 dana Pro paketa."* Otherwise →
  *"Vaš paket je produžen za 30 dana."* — plan-neutral, and correct for a trial, a Standard customer
  and a Partner alike. A Max customer must never be told they received "Pro".

Putting this beside `PlanGuard` rather than inside `InvitationService` is deliberate: if a second
feature ever grants days (a promotion, a goodwill credit), it reuses one guarded function instead of
copying the arithmetic. It is also the unit the tests in §9 target.

### 3.2 The reward must not touch `PlanNotes`

`PlansPage.tsx:55` detects the registration trial with **exact string equality**:

```ts
const isTrial = plan.planNotes === 'Probni period'
```

So writing anything into `PlanNotes` — including a tidy `"Bonus od pozivnica: +30 dana"` line —
**silently removes the trial notice from the plans page** for every organization still on trial, which
is precisely the population most likely to be inviting people. `RegistrationTrialTests` locks the same
exact string.

The itemised truth lives in the invitation rows instead: each row records the days it granted and
when. That is a better ledger anyway — queryable, per-invitation, and it is what the cap in §3.3 is
computed from. `PlanNotes` stays what it is: the operator's manual bookkeeping field.

If a SystemAdmin ever needs an at-a-glance number per organization, it is a computed column in the
admin list (a sum over invitation rows), never a string written into the org.

### 3.3 Caps

| Key | Default | What it bounds |
|---|---|---|
| `Invitations:Reward:DaysPerAccepted` | 30 | one successful invite |
| `Invitations:Reward:MaxDaysPerOrganization` | 180 | total exposure per organization, ever |
| `Invitations:Reward:MaxPerRolling30Days` | 5 | how fast an attack can run — slow enough to notice |

Plus **one reward per invited organization, ever**: before granting, check that no earlier invitation
already paid out for that newly created organization. This closes "invite the same person from two of
my own accounts".

Note the real arithmetic the PO signed off on: each successful invite gives away **60 days of Pro** —
30 to the inviter and 30 extra trial days to the invitee. The 180-day cap is therefore chosen against
that number, not against 30.

### 3.4 When it runs

**After the invitee's email verification is committed — not before.**

`InvitationService` shares the request's `IUnitOfWork`. If the grant's `SaveChangesAsync` throws, the
`Organization` and `Invitation` entities stay tracked and `Modified` on the same `DbContext`; the next
`SaveChangesAsync` in the method re-attempts them and throws again, **outside** the `try`. A
`try/catch` around the grant does not isolate that. So:

```csharp
user.EmailVerifiedAt = now;                 // AuthService.cs:269
stored.UsedAt        = now;
await _uow.SaveChangesAsync();              // AuthService.cs:272 — verification is durable

try { await _invitations.TryGrantRewardForVerifiedUserAsync(user.Id); } catch { log }
```

The same rule applies to attribution in `RegisterAsync`: call it **after** `IssueTokensAsync`
(`AuthService.cs:151`), so a failure there cannot take out refresh-token issuance and leave someone
registered but not signed in.

**Known gap, accepted:** an invitee who never clicks verify but later does a password reset also gets
`EmailVerifiedAt` set (in `ResetPasswordAsync`) and will not trigger the reward. Rare enough to accept
in exchange for a single call site.

---

## 4. What is stored

### 4.1 `User.ReferralCode`

One nullable `string(64)` with a unique filtered index, minted lazily the first time the user opens
`/invite`. 128-bit: `Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()`.

Stored **as-is, not hashed**, exactly like `CalendarSettings.FeedToken`, whose own doc-comment states
the rule this reuses: *"Stored as-is (not hashed) so the URL can be shown again to the user — the
'secret address' model used by public iCal feeds."*

**No rotation endpoint.** If the link leaks into a public Facebook group the consequence is that the
owner earns *more* invitations, bounded by the cap in §3.3. There is nothing to remedy.

Entropy is not the control that protects the reward, and nobody should later relax a cap on the
grounds that the code is long: guessing someone's code only credits *them*. The real fraud vector is
"many fake signups under my own code", which is unaffected by code length and is bounded by §3.3.

### 4.2 The `Invitation` table

```
Invitation : BaseEntity                    // Id, CreatedAt, UpdatedAt
  Source                 InvitationSource   // EmailInvite = 1 | ShareLink = 2
  InviterUserId          int?  FK User          SET NULL
  InviterOrganizationId  int?  FK Organization  SET NULL   // the org that receives the reward
  Email                  string(256)         // normalized: Trim().ToLower()
  EmailCanonical         string(256)         // "+tag" stripped — comparison key only
  PersonalMessage        string(300)?
  Status                 InvitationStatus    // Sent = 1 | Accepted = 2
  AcceptedAt             DateTime?
  AcceptedByUserId       int?  FK User          SET NULL
  AcceptedOrganizationId int?  FK Organization  SET NULL   // one-reward-per-org guard
  RewardDays             int?
  RewardGrantedAt        DateTime?
```

**Why persist at all** — worth stating, because "just send the mail" is the obvious first instinct.
Without rows there is no per-recipient cooldown (one user can mail the same stranger forever), no cap
(the only thing bounding the cost), no "one reward per invited organization", no list for the inviter,
and no audit trail for plan days we gave away.

**`EmailCanonical`.** `marko+1@gmail.com`, `marko+2@gmail.com` … is the cheapest possible evasion of
both the cooldown and the cap. Strip the `+tag` **for comparison only**, never for the address we
actually mail. **Do not strip dots** — they are significant at most providers, and stripping them
collides genuinely different people.

**`Source`.** A row is born one of two ways: *sent* (you typed an address) or *accepted* (a stranger
used your share link — there was never a send). The daily send cap counts only `EmailInvite`,
otherwise share-link signups would eat the inviter's own sending budget.

**No `Suppressed` status.** It was in an earlier design to hide "this address already has an account";
D8 replaced that with sending a *different* email, so there is nothing to suppress and nothing for the
UI to lie about.

**Expiry is computed, never stored.** Same idiom as `PlanHelper` (effective plan) and
`TreatmentStatusHelper` (treatment status) — nothing in this app runs a job to flip a column.

**EF configuration.** `SetNull` on every FK, for the reason SPEC-13 gave: deleting a user must not
delete the ledger that justifies an organization's extended plan. Indexes:
`(InviterUserId, CreatedAt)` for the list and the daily count, `(EmailCanonical, CreatedAt)` for the
cooldown and the accept-by-email match, `(InviterOrganizationId, RewardGrantedAt)` for the caps.

**Deliberately not unique:** `(InviterUserId, EmailCanonical)`. A hard unique index makes re-inviting
after the cooldown impossible forever; the cooldown is a service rule and re-invites create new rows,
so the history stays honest.

One migration, `AddInvitations`, covering the table + `User.ReferralCode`. Additive and nullable
throughout — no backfill, no data risk on the live database.

---

## 5. Attribution

Three signals, in priority order:

1. **`?ref={code}` on `/register`** — the primary signal. `RegisterDto` gains a trailing
   `string? ReferralCode = null`; as a defaulted trailing parameter it keeps every existing call site
   compiling, including `RegistrationTrialTests`.
2. **Email match** — no code, but the registrant's canonical address matches an invitation we sent:
   that row flips to `Accepted`.
3. **Neither** — a share-link signup with a code we recognise creates a fresh row with
   `Source = ShareLink, Status = Accepted`.

Signal 2 matters more than it looks. People are invited at one address and sign up with another, or
lose the mail and type the URL by hand. Without it, half of all emailed invitations sit at "Poslano"
forever and the feature looks broken.

`GET /api/invites/ref/{code}` returns `{ inviterFirstName }` or 404, feeding the banner on
`/register`. Include it in v1 — not for conversion but for **observability**: an invisible query
parameter is one nobody notices has stopped working. First name only, never a full name, never an
email. This is not the enumeration risk that ADR-029 guards against: a referral code maps to an
invitation, not to an account, so confirming one exists reveals nothing about any address.

**Never put the invitee's email in a URL and never prefill it from a query parameter.**

### The 60-day trial

The invitee's extended trial is granted at registration from
`Invitations:InviteeTrialDays` (default 60), falling back to `Plans:Trial:Days` (default 30) when
there is no valid referral. It does **not** wait for verification: only the *inviter's* reward is
gated (§3.4), and delaying the invitee's benefit would punish the person we are trying to attract.

There is no fraud in this direction worth guarding: a farmer spraying their own link earns 60-day
trials for *other people's* organizations, which does nothing for theirs, and their own take is
capped at 180 days regardless.

---

## 6. Protecting it (Phase 2, except where noted)

### 6.1 The risk that justifies the rest

The moment a user can type any address and have our server mail it, we are running a mail relay with a
Melarium logo on it. The expensive failure is not a rude invitation — it is that
**`melarium.app`'s sending reputation is shared with password-reset and verification mail**. One abuse
campaign gets the domain blocklisted and **account recovery silently stops working for every existing
customer**. Every control below exists for that, not for tidiness.

This is also the reason Phase 1 ships without the email channel at all (D9): the machinery that
touches billing goes to production first, alone, and the abuse surface arrives separately.

### 6.2 The controls

| | Control | Phase |
|---|---|---|
| 1 | **`[Authorize]`, never anonymous.** Precedent with the reasoning already attached: `AuthController.ResendVerification`. | 1 |
| 2 | **The sender's own email must be verified.** `EmailVerifiedAt is null` → 422 *"Potvrdite svoju e-poštu prije slanja pozivnica."* with a route to fix it. Cheap, strong, and non-obvious: it forces an account-farmer to control a real mailbox per account. Safe to ship — ADR-029 backfilled every pre-existing account as verified. | 2 |
| 3 | **Rate-limit policy `"invite"`, 3/min per IP.** A separate policy, not a reuse of `auth-email`, following the codebase's own reason for keeping `login` and `auth-refresh` apart — an invite burst must never block a password reset from the same NAT. Matches `feedback` and `auth-email` at 3/min. | 2 |
| 4 | **Per-user rolling 24h cap**, `Invitations:MaxPerUserPerDay`, default **10**. This is the control that actually works: IP limits are defeated by mobile IP rotation and shared under CGNAT, which in BiH means one carrier IP is thousands of people. Exceeded → **422** with a Bosnian message, not 429 — the framework's 429 carries no body through the existing pipeline. Someone who genuinely wants to invite thirty people uses the share link. | 2 |
| 5 | **Per-recipient cooldown**, `Invitations:RecipientCooldownDays`, default **30**, scoped to the inviter: *"Već ste pozvali ovu adresu prije X dana."* No leak — it is the caller's own history. | 2 |
| 6 | **No self-invite.** Own address → 422 *"Ne možete poslati pozivnicu na vlastitu adresu."* Table stakes only; real farms use other addresses. | 2 |
| 7 | **Personal message: no URLs.** Reject `https?://`, `www.` and bare `domain.tld` with *"Poruka ne smije sadržavati linkove."* Without this, the feature is a machine that sends attacker-chosen links from a domain carrying our SPF/DKIM — the direct path to §6.1. | 2 |
| 8 | **Attribute the message visibly** — a quoted block prefixed *"Poruka od {Ime}:"*, never inline in Melarium's own voice. The inviter's display name is self-chosen, and this is the first place a user-chosen name reaches a stranger's inbox. | 2 |
| 9 | **Operator escape hatch:** `Invitations:BlockedEmailDomains`, comma-separated, **empty by default**. Turns "we need a hotfix" into "set an env var". A curated disposable-domain blocklist is explicitly **not** shipped — it is a maintenance treadmill that is always stale. | 2 |
| 10 | **Logging:** every send at Information with inviter id and invitation id, **never the full recipient address at Information level**. The row is in the database, which is where an operator investigates. | 2 |

**No account-age gate** — redundant next to #2 and it frictions exactly the person most likely to
invite: someone who just discovered the app. Rejected deliberately.

**Do not scale the daily cap by plan.** It couples growth to billing and punishes the behaviour we are
paying for.

### 6.3 When the invited address already has an account (D8)

Send them a **different email**: *"{Ime} misli da bi vam Melarium koristio — vi već imate račun"*, with
a login link instead of a registration link.

This is better than the two obvious alternatives and it is worth recording why:

- **Silent suppression** (record it, send nothing, show "Poslano") means the UI knowingly shows the
  inviter something untrue. That was an earlier design's choice and it was rejected here.
- **Telling the sender** *"that person already uses Melarium"* turns the endpoint into an
  account-enumeration oracle that is worse than forgot-password: it accepts any address, it is
  available to every authenticated user, and it is trivially scriptable.

The different-email answer gives up neither. The sender sees the same response either way — and it is
true, because something really was sent. The existing customer is not mailed an invitation to sign up
for something they already have. No row needs a special status.

---

## 7. The emails (Phase 2)

### 7.1 Two small fixes to the shared template

`EmailNotificationWorker.BuildHtml` (`EmailNotificationWorker.cs:94-136`) greets
`Pozdrav <strong>{name}</strong>,` — where `name` falls back to the raw email address for
`ForAddress` mail — and footers *"Ovu poruku ste primili jer imate nalog na Melarium aplikaciji."*
For someone who is not a customer both are false, and the footer is worse than cosmetic: a false
statement about **why you received this mail** is what gets a sender reported as spam.

```csharp
public sealed record QueuedEmail(
    int? UserId, string Title, string Message,
    string? ActionUrl = null, string? ActionLabel = null,
    string? ToEmail = null, string? ToName = null,
    string? Greeting = null,      // null → "Pozdrav {resolvedName},"
    string? Footer   = null);     // null → the account-holder footer
```

All **four** existing call sites keep compiling and behaving identically — `AuthService.cs:209`
(password reset), `AuthService.cs:297` (verification), `NotificationService.cs:43` (bell
notifications), `FeedbackService.cs:254` (SPEC-13 operator mail). The invitation passes
`Greeting = "Pozdrav,"` — a natural Bosnian greeting to an unnamed person — so `null` keeps meaning
"default" and there is no empty-string special case.

**And the newline fix.** `BuildHtml` escapes the message but never converts newlines, so multi-line
bodies collapse into one paragraph (SPEC-13's operator mail suffers this today):

```csharp
var safeMessage = Escape(item.Message).Replace("\r\n", "\n").Replace("\n", "<br>");
```

**The order is load-bearing: escape first, then insert `<br>`.** Reversed, it is an XSS hole.

**Rejected:** a second `BuildExternalHtml` (duplicates the layout; one copy drifts when the brand
changes); an `EmailAudience` enum (hardcodes product copy into an Infrastructure worker when every
other feature's Bosnian copy lives beside the feature that sends it).

### 7.2 Copy

**Invitation — to someone with no account**

| | |
|---|---|
| **Title** | `{Ime} {Prezime} vas poziva` |
| **Greeting** | `Pozdrav,` |
| **Body** | `{Ime} {Prezime} ({email}) koristi Melarium — aplikaciju za vođenje pčelinjaka — i poziva vas da se pridružite.`<br>`Poruka od {Ime}: „…"` *(only when present)*<br>`U Melariumu vodite evidenciju pregleda, matica, tretmana i vrcanja, dobijate upozorenja (mraz, kašnjenje pregleda, kraj karence) i imate AI savjetnika na bosanskom. Registracija je besplatna, a preko ove pozivnice dobijate {N} dana Pro paketa umjesto uobičajenih {M}.` |
| **Button** | `Prihvati poziv` → `{FrontendUrl}/register?ref={code}` |
| **Footer** | `Ovu poruku ste primili zato što vam je pozivnicu poslao korisnik {Ime} {Prezime} ({email}). Ako ne poznajete tu osobu, slobodno zanemarite ovu poruku — nismo napravili nikakav račun na vaše ime.` |

**Invitation — to someone who already has an account (§6.3)**

| | |
|---|---|
| **Title** | `{Ime} {Prezime} vam preporučuje Melarium` |
| **Body** | `{Ime} {Prezime} ({email}) vam je poslao pozivnicu za Melarium. Vi već imate račun s ovom adresom, pa vam ne treba nova registracija.`<br>`Poruka od {Ime}: „…"` |
| **Button** | `Prijavite se` → `{FrontendUrl}/login` |

Both `{N}` and `{M}` come from configuration, never hard-coded — the trial length is a one-env-var
change specifically designed not to need a deploy, and hard-coding it means the mail starts lying the
day it is tuned.

The title carries the friend's name, not the product's: the worker already prefixes every subject with
`Melarium — `, and to a stranger "Melarium" means nothing while the friend's name is the entire reason
the mail gets opened. This needs **no** subject-override mechanism.

Grammar note: *"…zato što vam je pozivnicu poslao korisnik {Ime}"* — `poslao` agrees with the
masculine noun *korisnik*, so it stays correct regardless of the inviter's gender and avoids
"poslao/la".

All URLs via the existing fallback chain in `AuthService.BuildFrontendUrl` (`AuthService.cs:331-333`).
Extract it once rather than copying the chain into a second service.

---

## 8. Surface

`InvitesController`, `api/invites`, `[Authorize]` at class level.

| Method | Path | Auth / limiter | Phase |
|---|---|---|---|
| GET | `/api/invites/summary` | `[Authorize]` | 1 |
| GET | `/api/invites/mine` | `[Authorize]` | 1 |
| GET | `/api/invites/ref/{code}` | `[AllowAnonymous]`, `auth-token` | 1 |
| POST | `/api/invites` | `[Authorize]`, `invite` | 2 |

`summary` returns `{ sentCount, acceptedCount, rewardDaysEarned, rewardDaysCapRemaining, shareUrl }`.
`shareUrl` is built **server-side** — the client does not know `FrontendUrl`, which differs from the
browser origin under the dev proxy.

Distinct literal path segments (`mine`, `summary`, `ref/…`) so nothing relies on
literal-beats-parameter route precedence. `201` for create; `422` for every business rule, matching
`BusinessRuleException`'s existing mapping; `429` from the limiter.

`IAccessGuard` is **not** used — an invitation is not in the Organization → Apiary → Beehive
hierarchy; scoping is "your own rows" via `InviterUserId`. `IPlanGuard` is **not** used either:
gating the ability to refer would be self-defeating.

**Frontend** — `/invite` is a page inside the protected `Layout`, not a modal: a route means a
`helpRoutes` entry and therefore a help icon (SPEC-14 resolves help by route), and there is no room in
a modal for the share card, the stats strip and the history list.

- Share card: read-only input + **Kopiraj** (the `navigator.clipboard.writeText` + 2-second `copied`
  pattern from `CalendarSettingsPage.tsx:16-31`, including its `catch` for a blocked clipboard) and
  **Podijeli** via `navigator.share` behind a capability check. No hardcoded `wa.me` / `viber://`
  links — they break, and the native share sheet already lists both.
- History list: email, status badge (the `STATUS_STYLE` record pattern from `MyFeedbackSection`),
  date. A registered-but-unverified invitee reads **"Registrovao se — čeka potvrdu"**, never blank and
  never "pridružio se".
- `Layout.tsx` — "Pozovi prijatelja" (`UserPlus`) in the desktop profile dropdown **and** the mobile
  panel. There are two copies of that menu and missing the second is the classic bug here. Not the
  sidebar: that is the daily working set.
- `helpRoutes.ts` + `helpContent.ts` — both required; `HELP_CONTENT` is `Record<HelpKey, HelpEntry>`,
  so omitting the entry is a compile error.
- `RegisterPage.tsx` — read `?ref=`, pass it through `RegisterPayload`, render the inviter banner.

---

## 9. Phases

**Phase 1 — link, attribution, reward.** Entity + 2 enums, the `AddInvitations` migration (table +
`User.ReferralCode`), repository + `IUnitOfWork`, `InvitationService`, `GrantDaysAsync` beside
`PlanGuard`, `RegisterDto.ReferralCode` + the 60-day trial, the verification hook,
`NotificationType.InvitationAccepted = 24`, `GET summary|mine|ref/{code}`, `/invite` with the share
card + stats + list, `/register` banner, Layout ×2, help ×2, `InvitationRewardTests`.

Ships alone and works end-to-end over Viber/WhatsApp. No mail is sent by this feature in Phase 1, so
none of §6 or §7 is needed yet.

**Phase 2 — the email channel.** `POST /api/invites`, the `QueuedEmail` greeting/footer + `<br>` fix,
both email bodies (§7.2), the already-has-an-account branch, the `invite` rate-limit policy, the daily
cap, the cooldown, the self-invite check, the personal message + URL ban, `BlockedEmailDomains`.

Ships alone: it adds a second way to create a row that Phase 1 already knows how to process.

**Why this order.** The reward machinery touches billing and the email channel carries the
domain-reputation risk (§6.1). Shipping them separately means each goes to the live database on its
own, and if the email channel ever has to be switched off after an incident, the link keeps working.

### Tests — part of Phase 1, not a follow-up

`Melarium.Application.Tests` already carries a file per feature that can silently corrupt data
(`AdminPlanUpdateTests`, `EmailVerificationTests`, `RegistrationTrialTests`). This is the first
feature that **writes plan value automatically**, and §3.1's dangerous branches need a Partner or an
expired organization in front of you — no manual pass catches them. `InvitationRewardTests` covers:

- lifetime plan (`PlanValidUntil is null`) → **no expiry date written**, 0 days, no notification;
- active trial → date extended, `Plan` unchanged;
- expired / Free → `Plan = Pro`, date = today + 30;
- paying Standard / Max / Partner → date extended, **`Plan` untouched**, plan-neutral message;
- `PlanValidUntil` in the past → `Max(existing, today)` applies;
- the 180-day lifetime cap and the 5-per-30-days rolling cap;
- one reward per invited organization;
- **`PlanNotes` is byte-identical before and after a grant** (§3.2);
- a registration with an unknown or malformed code still returns a `LoginResponseDto` with a
  30-day trial;
- a grant that throws still leaves `EmailVerifiedAt` persisted (§3.4).

---

## 10. Outcomes considered

| Situation | What happens |
|---|---|
| Invitee clicks and registers with the invited address | Attributed by `?ref`, row flips to `Accepted` |
| Invitee registers with a **different** address | Still attributed — the link carries the code |
| Invitee loses the mail and types the URL by hand | Attributed by the email-match fallback (§5) |
| Invitee never opens the mail | Row stays `Sent`. **No automatic reminder in v1** — the share link is the answer to "it went to spam", not a second mail |
| Invitee registers but never verifies | Inviter sees *"Registrovao se — čeka potvrdu"*. No reward, no cap consumed |
| Invited address already has an account | A different email (§6.3). No oracle, no lie, no unsolicited sign-up pitch |
| Mail bounces | Not surfaced in v1. Resend webhooks are their own piece of work |
| Recipient reports us as spam | The footer states plainly why they received it and that no account was created in their name — the single best defence available |
| Inviter's link leaks publicly | Bounded by the 180-day cap. Nothing to remedy — no rotation endpoint (§4.1) |
| Inviter invites the same person repeatedly | 30-day cooldown, with their own history as the stated reason |
| Inviter invites 50 people at once | Daily cap of 10 → 422 with a Bosnian message |
| Inviter invites themselves | Rejected |
| Inviter farms fake accounts | Must control a real mailbox per fake org (reward at verification) **and** is capped at 180 days total, ≤5 per 30 days |
| Inviter is on a **lifetime** plan | Recorded, 0 days, **no reward notification**. The plan is never given an expiry date |
| Inviter is Free or expired | Reward returns them to **Pro for 30 days** — the feature doubles as a reactivation path |
| Inviter is a paying Standard/Max/Partner | Date extended, `Plan` untouched, message says "produžen", never "Pro" |
| Inviter is a Beekeeper inside someone else's org | Allowed. The reward goes to **their** organization — a member brings a friend, the company gets the days |
| The same person is invited by two of my accounts | One reward per invited organization, ever |
| Referral code is unknown, expired or malformed | **Registration still succeeds**, standard 30-day trial. Never an error |
| The reward throws mid-grant | Verification is already committed (§3.4). The user is verified; only the reward is lost, and it is logged |
| Inviter's account is deleted | `SET NULL` — the ledger survives, because it is what justifies the organization's extended plan |

---

## 11. Acceptance criteria

**Locked by automated tests** (`dotnet test`, 401/401 green on 2026-08-06):

- [x] Reward on an org that is (i) on an active trial → date extended, `Plan` unchanged; (ii) expired/Free → `Plan = Pro`, date = today + 30; (iii) a paying Standard/Max/Partner → date extended, **`Plan` untouched**.
- [x] **An organization with `PlanValidUntil = null` is never given an expiry date**, and 0 days are recorded.
- [x] `PlanValidUntil` never moves backwards, including when the plan lapsed yesterday.
- [x] **`PlanNotes` is unchanged by any reward** — asserted for `"Probni period"`, an operator note, and null.
- [x] A reward failure never fails the invitee's verification (`EmailVerificationTests`), and an attribution failure never breaks registration (`RegistrationTrialTests`).
- [x] Registering with an **unknown or malformed** code still succeeds with the standard 30-day trial.
- [x] Registering through a valid referral gives the new organization **60** days instead of 30.
- [x] `InvitationRewardTests` covers every branch in §9.

**Verified in the browser** (dev server, no backend — which is what made the second one a real test):

- [x] `/register?ref=…` passes the code through and calls `GET /api/invites/ref/{code}`.
- [x] A failed referral lookup leaves the sign-up form **completely intact** — verified against a hard 500, which is stronger than the 404 an unknown code produces.
- [x] A resolved code renders the banner: *"**Marko** vas poziva u Melarium — dobijate **60 dana** Pro paketa umjesto uobičajenih 30."*

**Code complete, still to confirm against a real database and a signed-in session** (no local Postgres — see §11 note):

- [ ] Any authenticated user can open `/invite`, see their personal link, copy it and share it.
- [ ] Registering through the link credits the inviter; the row appears in their list.
- [ ] Registering with no code but an address matching a sent invitation still flips that invitation to `Accepted`.
- [ ] The reward lands only when the invitee **verifies their email**, and `Status = Accepted` flips at registration — both moments shown separately ("Registrovao se — čeka potvrdu" vs "Pridružio se").
- [ ] The 180-day lifetime cap and the 5-per-30-days cap hold; one invited organization is rewarded at most once.
- [ ] The reward notification never over-promises: "Pro" only on an upgrade, otherwise "produžen", and nothing at all for 0 days.

**Done:**

- [x] All user-facing strings Bosnian; `/invite` has both a `helpRoutes` and a `helpContent` entry; the profile menu carries it in **both** its desktop and mobile copies.
- [x] Docs updated: `features/invitations.md` (new), `api-contracts.md`, `context.md`, `decisions.md` (ADR-032), `.env.example`, `docker-compose.yml`.

**Phase 2 — not built:**

- [ ] A user whose own email is unverified cannot send invitations and is told how to fix it.
- [ ] Sending is limited to 3/min per IP **and** 10 per rolling 24h per user, the latter as a 422 with a Bosnian message.
- [ ] Re-inviting the same address inside 30 days is refused; inviting an address that already has an account sends the **login-link** variant, not the registration invitation.
- [ ] A message containing a URL is rejected; a sent message renders as an attributed, HTML-escaped quote block with working line breaks.
- [ ] The four existing `QueuedEmail` call sites still produce byte-identical mail.

> **Note on what could not be verified locally.** There is no local PostgreSQL on this machine, so
> nothing that needs a live database or a signed-in session was exercised end-to-end. The migration
> is additive (one nullable column, one new table, no backfill) and applies cleanly to the model, but
> **it has not been run against a real database.** Everything above that depends on persistence is
> marked unticked rather than assumed.

---

## 12. Deliberately out of scope, and one open item

**Out of scope v1** — named so they are decisions, not omissions: reward tiers, leaderboards and cash
payouts; a referral analytics dashboard; bulk or CSV invite; contact-book import; disposable-domain
blocklist curation; registration-IP fraud signals; reminders for an unanswered invitation; bounce
handling; localising the invitation email; adding an **existing** Melarium user to another
organization (a different feature entirely — see D1).

**Open item — retention of non-users' addresses.** This is the first feature that stores personal data
about people who are **not** customers and agreed to nothing: the address of an invitation that is
never accepted, kept indefinitely, with no opt-out. Every other table in this app holds data about
someone who signed up. The GDPR work (account deletion, data export) is the postponed Phase 4 of the
July 2026 refactor, so there is no existing machinery to hang this on, and SPEC-16 is already the
retention spec.

**The v1 mitigation is real but partial:** the invitation footer states plainly that no account was
created in the recipient's name, and the address is used for that one message and nothing else.

**The decision still to take** — before or alongside SPEC-16, not inside this spec: whether unaccepted
invitations get a hard expiry-and-delete (12 months is the obvious default), and whether the footer
gains a one-click *"ne želim više pozivnice"* link that writes a suppression row. Recorded here so it
stays a scheduled decision rather than a discovery.
