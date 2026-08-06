# SPEC-15 — "Pozovi prijatelja" (Invite a friend)

| | |
|---|---|
| **Status** | 📋 Planned — **ready to implement**, all decisions taken (§13) |
| **Effort** | L (~4–5 days) — two invitation kinds, a reward that touches billing, an auth-flow change |
| **Depends on** | nothing new; reuses the email queue (ADR-021), the token model (ADR-029) and the flat non-tenant authorization split from SPEC-13/ADR-030 |
| **New secrets / packages** | no packages. Config only: `Invitations:*` (caps, cooldown, reward, token days, blocked domains) |
| **Breaking** | **Yes** — `POST /api/org/members` is **removed** (§4.3). One new table and one nullable `User` column are additive; the removal is not. Phases C and D must deploy together. |
| **Reserves** | `NotificationType.InvitationAccepted = 24` (22 = SPEC-13, 23 = SPEC-12 Phase D) |

> **Provenance.** Designed 2026-07-30 and recovered verbatim from that session's transcript on the same
> day — the session hit its usage limit before the file was written. The body is unchanged from the
> design pass except where §13's decisions required it: §0 D6, §4.3, §5.2 #5, §10, §11 and §12 were
> edited to match the two calls that overrode the design's recommendations (honest global-cap message;
> delete `POST /api/org/members` rather than deprecate it). Each of those spots names what was traded
> away, so the reasoning behind the rejected option is not lost.

## 0. Executive summary of the decisions

| # | Decision | Recommendation |
|---|---|---|
| D1 | Scope | **Both kinds**, one table, one `Kind` discriminator. Kind A = platform referral (own org). Kind B = organization member. |
| D2 | Persistence | **Persist.** Fire-and-forget is impossible once you need a per-recipient cooldown, a per-user cap, a reward ledger, or an "accepted" state. |
| D3 | Kind A link | **Reusable personal referral code** on `User`, 128-bit, **plaintext** (calendar-feed model), no expiry, no rotation. |
| D4 | Kind B link | **Per-invitation single-use token, SHA-256 hashed, 7-day expiry, email-bound, revocable** (ADR-029 model). Never displayed, never shareable. |
| D5 | Reward | Kind A only. `+N` days on the **inviter's** org, granted at the invitee's **email verification**, never at registration. |
| D6 | `POST /api/org/members` | **Deleted** (PO override, 2026-07-30 — the design recommended deprecation). Kind B becomes the only way to add a member, which forces a phase-ordering rule: see §4.3 and §11. |
| D7 | Email template | Add `Greeting`/`Footer` overrides to `QueuedEmail` + fix `\n → <br>` in the worker. ~10 lines total, fixes SPEC-13's mail as a side effect. |
| D8 | Frontend | **Page** (`/invite`), not modal, plus a new public `/join` page for kind B acceptance. |

---

## 1. Scope: what "pozovi prijatelja" means

The user's phrasing "**da se pridružite nama**" is unambiguous — "join *us*", the platform, not "join my organization". Kind A is the feature they asked for. The PO has since added kind B, which is a different feature wearing the same word, and the design must keep that distinction sharp because the two have **opposite security properties**:

- A **kind A link is not a capability.** Worst case a stranger creates their own organization on a free trial — which is what the marketing site invites anyone to do anyway.
- A **kind B link is a credential.** Whoever redeems it gets a real account *inside the inviter's organization*, with read access to their apiaries, hives, inspections, harvests and treatments. It is functionally a password-reset link for an account that does not exist yet.

Everything downstream (token storage, expiry, sharing, revocation, reward eligibility) follows from that one asymmetry, and the design should say so out loud in the spec's opening.

Kind B also fixes a real existing gap: `OrgManagementService.CreateMemberAsync` (`backend/Melarium.Application/Features/OrgManagement/OrgManagementService.cs:215-313`) makes the OrgAdmin type a password for the new member and then sends a notification saying *"Vaš račun je kreiran. Možete se prijaviti s e-poštom: X"* — with no password anywhere. The member literally cannot log in unless the admin tells them out of band, and the admin knows their password. Kind B replaces that with the invitee setting their own.

---

## 2. Data model

### 2.1 Why persist at all

Worth arguing explicitly in the spec, because "just send the email" is the obvious first instinct:

1. **A per-recipient cooldown is impossible without a record.** With only the IP rate limiter, one user can mail the same stranger 3×/minute forever. That is a harassment vector, not a theoretical one.
2. **IP-partitioned limits are the wrong unit in BiH.** Carrier-grade NAT means one mobile IP is thousands of users (too tight for legitimate users) while an attacker rotates IPs by toggling airplane mode (too loose for abuse). The control that actually works is per-user, which needs rows.
3. **The reward has cash value** → it needs an auditable ledger, a per-org cap, and a one-reward-per-invitee guard. All of those are queries over rows.
4. **Kind B's token must be stored** by definition.
5. The user needs to see what they already sent, or they re-send.

### 2.2 `Invitation` — one table, `Kind` discriminator

One table rather than two, citing the exact precedent already accepted in this repo: **ADR-029 chose one `UserToken` table with a purpose discriminator rather than a table per flow**, because the lifecycle is the same. Here too: create → email → (accept | expire). The anti-abuse controls, the "my invitations" list, and the ledger are all shared; only the token half differs.

```
Invitation : BaseEntity                     // Id, CreatedAt, UpdatedAt
  Kind                   InvitationKind      // PlatformReferral = 1 | OrganizationMember = 2
  Source                 InvitationSource    // EmailInvite = 1 | ShareLink = 2
  InviterUserId          int?   FK User          SET NULL
  InviterOrganizationId  int?   FK Organization  SET NULL   // the org that receives any reward
  Email                  string(256)          // normalized: Trim().ToLower()
  EmailCanonical         string(256)          // Email with any "+tag" stripped — dedupe key only
  PersonalMessage        string(300)?
  Status                 InvitationStatus     // Sent=1 | Accepted=2 | Suppressed=3 | Revoked=4
  // ── kind B only (all null for kind A) ──
  TokenHash              string(64)?          // SHA-256 hex of the raw token
  ExpiresAt              DateTime?
  UsedAt                 DateTime?
  MemberRole             UserRole?            // ApiaryAdmin | Beekeeper
  MemberApiaryId         int?   FK Apiary      SET NULL
  // ── acceptance + reward ──
  AcceptedAt             DateTime?
  AcceptedByUserId       int?   FK User          SET NULL
  AcceptedOrganizationId int?   FK Organization  SET NULL   // one-reward-per-org guard
  RewardDays             int?
  RewardGrantedAt        DateTime?
```

**`User.ReferralCode`** — one new nullable `string(64)` column with a unique filtered index, minted lazily the first time the user opens `/invite`. Not its own table: it is a 1:1 scalar attribute, and the alternative (a `ReferralCode` table with one row per user) buys nothing. It is the **exact model of `CalendarSettings.FeedToken`**, whose own doc-comment already states the rule this design reuses: *"Stored as-is (not hashed) so the URL can be shown again to the user — the 'secret address' model used by public iCal feeds."*

### 2.3 Field-by-field rationale for the non-obvious ones

- **`Source`.** A kind A row is born one of two ways: *sent* (you typed an address) or *accepted* (a stranger used your share link — there was never a "send"). One ledger, two birth paths. The per-user daily send cap counts only `Source = EmailInvite`, otherwise share-link signups would eat the inviter's own budget.
- **`EmailCanonical`.** `marko+1@gmail.com`, `marko+2@gmail.com` … is the cheapest possible evasion of both the cooldown and the reward cap. Strip the `+tag` for comparison only; never for the address we actually mail. **Do not strip dots** — dots are significant on most providers and stripping them creates false collisions between genuinely different people.
- **`Suppressed`.** A distinct terminal status for "we accepted the request and deliberately sent nothing" (recipient already has an account / global cap). Keeping this in the DB rather than lying with `Sent` means the operator can always tell what happened, even though the API deliberately does not distinguish (see §5.3).
- **No `Expired` status.** Expiry is **computed**, following `TreatmentStatusHelper` (status computed, never stored) and `PlanHelper` (effective plan computed, never stored) — the repo's established idiom, and ADR-028's reasoning. `IsExpired` on the DTO = `Status == Sent && (ExpiresAt ?? CreatedAt.AddDays(cooldown)) < now`.
- **No beehive assignments on kind B invitations.** Carrying them means a junction table, and hives can be deleted between invite and accept. The invitee arrives as a Beekeeper with **zero** assigned hives and the OrgAdmin assigns them on the existing `MemberAssignmentPage`. Least privilege on arrival, one less table. `MemberApiaryId` **is** carried, because `AdminService`'s consistency rule requires an ApiaryAdmin to have an apiary and there is no OrgAdmin-facing "change role" endpoint to fix it afterwards.

  ⚠ **This is a regression from today's behaviour, and it was confirmed deliberately (2026-07-30).** The current `MembersPage` create form sends `assignedBeehiveIds` alongside the new member (`frontend/src/features/members/MembersPage.tsx:99`) and `CreateMemberAsync` writes the assignments immediately (`backend/Melarium.Application/Features/OrgManagement/OrgManagementService.cs:274-276`), so **an OrgAdmin creates the member and assigns their hives in one step**. After this spec that becomes two steps, and the second one is only possible **after the invitee accepts** — until then there is no user row to assign anything to. An admin who invites and forgets leaves a member with access to nothing. Accepted anyway: the junction table plus the deleted-hive-between-invite-and-accept case cost more than the second visit to `MemberAssignmentPage`. Do not silently "fix" this during implementation by adding assignments back to the invitation — it is the decision, not an oversight.

### 2.4 EF configuration

```
InvitationConfiguration
  Email/EmailCanonical  required, 256
  TokenHash             64;  PersonalMessage 300
  Kind/Source/Status    required
  HasOne(Inviter).WithMany().HasForeignKey(InviterUserId).OnDelete(SetNull)
  ... same SetNull for InviterOrganization, AcceptedBy, AcceptedOrganization, MemberApiary
  HasIndex(TokenHash).IsUnique()                        // filtered — kind A rows are null
  HasIndex(new { InviterUserId, CreatedAt })            // "my list" + daily cap count
  HasIndex(new { EmailCanonical, CreatedAt })           // cooldown + accept-by-email match
  HasIndex(new { InviterOrganizationId, RewardGrantedAt })  // per-org reward cap
  ToTable("Invitations")
```

`SetNull` everywhere, for the same reason SPEC-13 gave: deleting a user must not delete the referral ledger that justifies an organization's extended plan.

**Deliberately not unique:** `(InviterUserId, EmailCanonical)`. A hard unique index makes re-inviting after the cooldown impossible forever. The cooldown is a service rule; re-invites create a new row, and the history stays honest.

One migration, `AddInvitations`, covering the table + `User.ReferralCode`, created in Phase A even though the kind B columns sit unused until Phase C. Two migrations over one table inside one spec is churn; every added column is nullable so there is no backfill and no data risk.

### 2.5 Repository

```csharp
public interface IInvitationRepository : IRepository<Invitation>
{
    Task<IEnumerable<Invitation>> GetByInviterAsync(int inviterUserId);
    Task<Invitation?> GetByTokenHashAsync(string tokenHash);
    Task<IEnumerable<Invitation>> GetPendingByEmailAsync(string emailCanonical, int inviterUserId);
    Task<int> CountSentByInviterSinceAsync(int inviterUserId, DateTime since);
    Task<bool> ExistsRecentForEmailAsync(string emailCanonical, int inviterUserId, DateTime since);
    Task<int> CountDistinctInvitersForEmailSinceAsync(string emailCanonical, DateTime since);
    Task<int> CountPendingMemberInvitationsAsync(int organizationId);
    Task<bool> AnyRewardForOrganizationAsync(int acceptedOrganizationId);
    Task<(int Sent, int Accepted, int RewardDays)> GetSummaryForInviterAsync(int inviterUserId);
    Task<int> SumRewardDaysForOrganizationAsync(int organizationId, DateTime? since = null);
}
```

`ExistsRecentForEmailAsync` mirrors `INotificationRepository.ExistsRecentAsync`, the alert-dedupe method — same shape, same naming.

---

## 3. Attribution

### 3.1 Kind A — three signals, in priority order

1. **`?ref={User.ReferralCode}`** on `/register` → resolves the inviter. Primary signal.
2. **Email match**: if the registrant's `EmailCanonical` matches a pending kind A invitation *from that inviter*, that specific row flips to `Accepted`. This is what makes the status column honest.
3. **No matching row** (share-link signup) → create a ledger row with `Source = ShareLink`, `Status = Accepted`.

The fallback matters more than it looks: people are invited at one address and sign up with another, or they lose the link and type the URL by hand. Without signal 2, half of all email invitations sit at "Poslano" forever and the feature looks broken.

### 3.2 Is guessability actually the threat? (say this explicitly)

The PO's instruction is to make the code unguessable now that there is a reward. **Do it — 128-bit — but do not let anyone believe entropy is the control that matters**, because it is not:

- Guessing Marko's code lets you *credit Marko* with your own signup. It gives him days; it takes nothing from you and nothing from anyone else. It is not an attack.
- The real reward-fraud vector is **"create many fake signups under my own code"**, which is entirely unaffected by how long the code is.

So: 128-bit (`Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()`, 32 chars) because it costs nothing and it also prevents walking the code space to count the user base. The controls that actually defend the reward are the verification gate and the caps in §6. Write that sentence into the spec so a future reader does not mistake the one for the other.

**No rotation endpoint for kind A.** If your referral link leaks into a public Facebook group, the consequence is that you get *more* rewards, bounded by the per-org cap. There is nothing to remedy.

### 3.3 The `RegisterAsync` change — keep it to four lines

`RegisterDto` gains a trailing `string? ReferralCode = null` (positional record; a defaulted trailing parameter keeps every existing call site compiling). `RegisterValidator` gets `MaximumLength(64)` and **nothing else** — the single most important rule in this section is:

> **An unknown, expired, malformed or missing referral code must never fail a registration.**

Implementation: after the account and organization are saved, `RegisterAsync` calls one injected seam and swallows everything:

```csharp
try { await _invitations.TryAttributeRegistrationAsync(user.Id, organization.Id, email, dto.ReferralCode); }
catch (Exception ex) { _logger... }   // attribution is never worth a failed sign-up
```

`AuthService` gains one constructor dependency and one guarded call. It touches **neither JWT claims nor refresh-token rotation**, which is what `ignore.md` actually freezes on that file (`docs/ignore.md:56-76` scopes the rule to claims and rotation, not the whole file). No DI cycle: `InvitationService` does not depend on `IAuthService`.

**Rejected alternative:** calling attribution from `AuthController` after `RegisterAsync` returns, to leave `AuthService` byte-identical. Rejected because it puts business logic in a controller, which `CLAUDE.md` forbids outright.

### 3.4 `RegisterPage` change

`useSearchParams()` → `params.get('ref')` → passed straight through `RegisterPayload`. Plus an inviter banner fed by `GET /api/invites/ref/{code}` returning only `{ inviterFirstName }`.

Include the banner in v1. Not for conversion — for **observability**: an invisible query parameter is one nobody notices has stopped working. The banner is the manual test for the whole attribution path, and it returns a first name only (never the invitee's email, never a full name), 404 on unknown, under the existing `auth-token` 10/min policy.

Note the deliberate departure from ADR-029's non-enumeration rule and why it is not a departure at all: a referral code maps to an *invitation*, not to an account, so confirming one exists reveals nothing about any address. Contrast that with forgot-password, where the same 404 would be an account oracle.

**Do not** put the invitee's email in the URL, and do not prefill from a query param. No personal data in query strings.

---

## 4. Kind B — organization member invitations

### 4.1 Token model: ADR-029, verbatim

Because this token *is* a credential:

- 256-bit raw value, **SHA-256 hashed at rest** (`TokenHash`), so a database leak is not a bag of working links.
- **Single use** (`UsedAt`), **7-day expiry** (`Invitations:MemberTokenDays`, default 7 — long enough to survive a weekend and a person who reads mail weekly, short enough to bound how long a live capability sits in an inbox).
- **Email-bound**: the account it creates always uses the invited address.
- **Revocable**: `DELETE /api/invites/{id}` sets `Status = Revoked` and the token stops resolving. This earns its place only for kind B; "revoking" a kind A invitation is meaningless.
- **Superseded on re-invite**: a new invitation to the same address marks the outstanding one used, exactly as `AuthService.IssueUserTokenAsync` already does ("a newly requested link supersedes earlier ones, so an old email can't be replayed").
- **Never shareable, never displayed.** The `/invite` page renders no copy-link button on kind B rows, and `InvitationDto` never carries the token. Make that an acceptance criterion — it is the single easiest thing to get wrong by accident while building the kind A share card next to it.

### 4.2 Acceptance flow

New **public** route `/join?token=…` and two endpoints:

| Method | Path | Auth | Returns |
|---|---|---|---|
| GET | `/api/invites/join/{token}` | anonymous, `auth-token` | `{ organizationName, inviterFirstName, email, roleName }`, else 404 |
| POST | `/api/auth/accept-invitation` | anonymous, `auth-token` | `LoginResponseDto` (auto-login, same as register) |

`/join` is a separate page from `/register` on purpose: the form has no organization field, the copy must name the organization *before* asking for a password, and keeping it off `/register` means `RegisterAsync` — the riskiest method in the app, with its documented org↔user insert-cycle workaround — never grows a second mode.

`AcceptInvitationAsync` lives on **`IAuthService`**, because it creates a user and issues tokens and `IssueTokensAsync` must never be duplicated. It delegates invitation resolution to `IInvitationService`. Rules, in order:

1. Resolve by `SHA256(token)` + `Kind = OrganizationMember`. Unknown / used / expired / revoked → **one** message: `"Pozivnica nije važeća ili je istekla. Zatražite novu."` Never distinguish which — no oracle value in it, and one message is one code path.
2. Inviter's organization still exists.
3. `MemberRole == ApiaryAdmin` ⇒ `MemberApiaryId` still resolves to a live apiary in that org. If the apiary was deleted meanwhile, reject with a clear message rather than creating an ApiaryAdmin of nothing (which `AdminService`'s consistency rules forbid).
4. **Seat re-check** — `IPlanGuard.EnsureCanAddMemberAsync(orgId)`. Confirmed safe from an anonymous context: `CurrentUser.Role` is null-safe (`Melarium.API/Security/CurrentUser.cs:31`), so `PlanGuard.Bypass()` correctly returns false. **But catch `PlanLimitException` here and rethrow as `BusinessRuleException`** — a 402 telling an anonymous invitee to "nadogradite paket" is nonsense; they cannot upgrade anything. Correct copy: *"Organizacija trenutno nema slobodnih mjesta. Javite se osobi koja vas je pozvala."*
5. Email already has an account → 422 *"Račun s ovom e-poštom već postoji. Prijavite se."* **Enumeration is not a concern here** and the spec should say why: the token holder was invited at that exact address, so they already know it. Contrast with kind A (§5.3), where the caller types an arbitrary address and honesty *would* be an oracle. Same product, opposite answer, for a principled reason.
6. Password via the existing `PasswordRules` (min 8), same validator shape as `ResetPasswordDto`.
7. On success: create the `User` with `Role = MemberRole`, `OrganizationId`, `ApiaryId` when applicable, and **`EmailVerifiedAt = now`** — receiving the token proved control of the mailbox, which is ADR-029's own reset-marks-verified reasoning. Mark the invitation `UsedAt`/`Accepted`/`AcceptedByUserId`. Notify the inviter. Issue tokens.

Authority to send kind B: **`OrganizationAdmin` only**, identical to `CreateMemberAsync`'s existing `ForbiddenAccessException` check. Kind A: any authenticated role, including a Beekeeper in someone else's org — the invitation creates a *separate* organization, so it touches neither their org's data nor its seats.

### 4.3 What happens to `POST /api/org/members`

**Delete it.** (PO decision 2026-07-30; the design had recommended deprecation, and the reasoning for that recommendation is kept below so a future reader knows what was traded away.) Concretely:

- `OrgManagementService.CreateMemberAsync` (`backend/Melarium.Application/Features/OrgManagement/OrgManagementService.cs:215-313`), the endpoint, its DTO and its validator all go.
- `MembersPage` loses the create form entirely; "Novi član" routes to `/invite?kind=member`.
- `api-contracts.md` records the removal, not a deprecation.

**The phase-ordering rule this creates — the one thing that must not be got wrong.** Deleting the endpoint makes a kind B invitation the *only* way to add an organization member. So the deletion must land in the **same deploy** as its replacement (Phase C backend + the Phase D `MembersPage` repoint), never in Phase A. Ship the deletion early and an OrgAdmin simply cannot add anyone until the rest arrives. This is the reason the phase list marks C and D as no longer independently shippable once D6 is "delete".

What the deprecation option would have preserved, now consciously given up: the endpoint is broken *for the member*, not for the admin — small outfits genuinely hand the password over in person, and it was the only path that worked when the member has no working mailbox. After this change, **an OrgAdmin can no longer onboard a member who has no reachable email address, and can no longer put someone to work within the minute** (today: create the account, say the password out loud, done).

**The one remaining escape hatch, and its limit.** `AdminService.CreateUserAsync` (`backend/Melarium.Application/Features/Admin/AdminService.cs:164-186`, behind `POST /api/admin/users`) still creates a user with an admin-chosen password **and** assigns beehives in the same call. This spec does not touch it, so the no-mailbox case is not completely unserviceable. But it is **SystemAdmin-only** — an OrgAdmin cannot reach it, so in practice it means "the customer phones the operator". Do not treat it as a substitute for the deleted flow; treat it as the manual fallback it is. If that turns out to matter in the field, the fix is an OrgAdmin-visible "kopiraj link" on a pending kind B invitation (letting the admin deliver it over Viber) — *not* resurrecting the password path. Note that this deliberately contradicts §4.1's "never shareable, never displayed" rule for kind B tokens, so it is a follow-up spec with its own security reasoning, not a quick patch.

**Explicitly out of scope:** retrofitting `CreateMemberAsync` to email a password-set link — it is being deleted, not fixed.

---

## 5. Anti-abuse — first class

This endpoint makes the application send mail to an arbitrary address a user types. Treat it as a mail relay with a Melarium logo on it.

### 5.1 Threat model

| | Threat | Primary control |
|---|---|---|
| T1 | Repeated invites to one victim (harassment) | per-inviter 30-day cooldown + global cap |
| T2 | Bulk spam (address-book dump) | per-user 10/24h cap |
| T3 | Abusive/phishing content in the personal message | 200 chars, no URLs, attributed quote block |
| T4 | Account enumeration via "already has an account" | silent suppression (kind A) |
| T5 | **Sender-reputation damage** | verification gate + URL ban + honest footer |
| T6 | Draining the Resend quota | per-user cap; note the shared quota in deploy docs |
| T7 | Reward farming | §6 |

**T5 is the highest-impact and the easiest to miss.** `melarium.app`'s sending reputation is shared with password-reset and verification mail. One phishing campaign run through this feature gets the domain blocklisted, and account recovery silently stops working for every existing customer. Every control below that looks like over-caution is really protecting that.

### 5.2 The control set

1. **`[Authorize]`, never anonymous.** Precedent is already in the codebase with the reason attached — `AuthController.ResendVerification`: *"Authenticated (not anonymous by address) so it cannot be used to send mail to arbitrary people."*
2. **New rate-limit policy `"invite"`, 3/min per IP.** A *separate* policy, not a reuse of `auth-email`, following the codebase's own precedent for keeping `login` and `auth-refresh` apart ("so a burst of refreshes can't lock out sign-ins") — an invite burst must never block a password reset from the same NAT. The number matches `feedback` and `auth-email` and the same one-line reasoning applies: each accepted request lands in someone's inbox, so this is an abuse budget, not a usability one.
3. **Per-user rolling 24h cap**, `Invitations:MaxPerUserPerDay`, default **10**. This is the control that actually works, since IP limits are defeated by mobile IP rotation and shared under CGNAT. Rolling window, not calendar day — no midnight reset to game, same single indexed count either way. Exceeded → **422**, not 429: `BusinessRuleException` is how this app returns an explainable Bosnian message, whereas the framework's 429 carries no body through the existing pipeline. Someone who genuinely wants to invite thirty people uses the share link.
   **Do not scale the cap by plan.** It couples growth to billing and punishes exactly the behaviour we are paying for. Out of scope, and say so.
4. **Per-recipient cooldown**, `Invitations:RecipientCooldownDays`, default **30**, **scoped to the inviter**, honest message: *"Već ste pozvali ovu adresu prije X dana."* No leak — it is the caller's own history. Kind A only; kind B legitimately re-invites when a token expires, and supersedes instead (§4.1).
5. **Global per-recipient cap**: more than **3 distinct inviters** to the same `EmailCanonical` in 30 days → recorded `Suppressed`, no mail. This is the control against coordinated harassment from three accounts, which the per-inviter cooldown alone does not stop.
   **PO decision 2026-07-30: tell the sender honestly that the address has already been invited** — 422, *"Ova adresa je nedavno već pozvana."* (The design had recommended a deliberately vague *"Trenutno nije moguće poslati pozivnicu na ovu adresu."*)
   Two implementation notes follow from that wording, and both are deliberate:
   - **The message does not say _by whom_, and must not.** "Već pozvana" is true whether the earlier invitation was the caller's own or a stranger's, so one sentence covers both controls (this cap and the per-inviter cooldown of #4) without ever naming a third party. Do not "improve" it later into *"neko drugi je već pozvao ovu adresu"* — that turns a weak inference into a statement.
   - **The accepted leak, stated plainly so nobody rediscovers it as a bug:** a caller who has never invited `x@y.com` and gets this message learns that somebody else recently did. That is the price of not lying to the user, and it was the product owner's call. It stays acceptable only because the message is silent about who and when.
6. **Verified-email gate to send.** `EmailVerifiedAt is null` → 422 *"Potvrdite svoju e-poštu prije slanja pozivnica."* with a link to the profile (the verification banner and `POST /auth/resend-verification` already exist). This is a strong, cheap, non-obvious control: it forces an account-farmer to control a real mailbox per account. **Safe to ship** because ADR-029's migration backfilled every pre-existing account as verified, so it cannot lock out the current user base.
7. **No account-age gate.** Redundant next to #6 and it frictions exactly the person most likely to invite — someone who just discovered the app. Rejected deliberately.
8. **Personal message: allowed, 200 chars, HTML-escaped, and no URLs.** The URL ban is the important half: without it, this feature is a machine that sends attacker-chosen links from a domain with our SPF/DKIM on it — the classic abuse of invite features and the direct path to T5. Reject on `https?://`, `www.`, and bare `domain.tld` patterns with *"Poruka ne smije sadržavati linkove."*
   Allowing free text at all is justified: *"Zdravo Ivane, ovo je ona aplikacija o kojoj sam ti pričao"* is what makes an invitation convert instead of reading as a brand blast, and it costs one validator rule.
9. **Attribute the message visibly.** Render it as a quoted block prefixed *"Poruka od {Ime}:"*, never inline in Melarium's own voice. Same reasoning: the inviter's display name is self-chosen, and this feature is the first place a user-chosen name reaches a stranger's inbox. The template frames it — *"Ovu poruku ste primili zato što vam je pozivnicu poslao korisnik {Ime} {Prezime} ({email})"* — so a name like "Melarium Support" cannot stand alone as an identity claim.
10. **Operator escape hatch:** `Invitations:BlockedEmailDomains` (comma-separated, **empty by default**). Five lines that turn "we need a hotfix deploy" into "set an env var". A curated disposable-domain blocklist is explicitly **not** shipped — it is a maintenance treadmill that is always stale; the verification gate and the caps do the real work.
11. **Observability:** log every send at Information with inviter id and invitation id, **never the full recipient address at Information level**. The row is in the database anyway, which is where an operator investigates. A SystemAdmin abuse dashboard is out of scope v1 precisely because the data is already queryable.

### 5.3 When the invited address already has an account (kind A)

Two sub-cases:

- **Own address** → 422 *"Ne možete poslati pozivnicu na vlastitu adresu."* No leak; it is their own address.
- **Someone else's** → **accept the request, record `Suppressed`, send nothing, and return a response byte-identical to a real send.**

Justification: answering honestly turns this into an account-enumeration oracle that is *worse* than forgot-password — it accepts any address, it is available to every authenticated user, and it is trivially scriptable. ADR-029 pays real UX cost to avoid exactly this. The secondary benefit is that we do not mail existing customers unsolicited invitations.

The cost is real and should be stated: the inviter sees "Poslano" for a mail that was never sent. That is the only place in this design where the UI knowingly shows something untrue, and it is a conscious trade.

**Alternative a PO may reasonably prefer:** don't check at all — just send it. Bob gets an invitation, clicks, hits "user already exists" on `/register`, and uses the "Već imate račun? Prijavite se" link that is already on that page. Simpler by ~15 lines and it drops `Suppressed` entirely. **Recommendation: suppress.** But flag it as the PO's call.

---

## 6. The reward

### 6.1 Eligibility

**Kind A only. Kind B earns nothing.** Recommendation with three reasons:

1. It is trivially farmable *and the farm self-verifies*: acceptance sets `EmailVerifiedAt = now`, so the verification gate — the whole basis of the anti-fraud model — does not apply to kind B at all.
2. It is not growth. No new customer appears, and members are already a **paid** feature (`MaxMembers` is 0 on Free), so we would be paying an org in Pro days for filling seats it is paying us for. Economically backwards.
3. It creates an incentive to hand out organization access to people who do not need it — a security anti-pattern paid for in plan days.

### 6.2 When it is granted

**At the invitee's email verification, never at registration.** Hook one call into `AuthService.VerifyEmailAsync` after `user.EmailVerifiedAt = now`:

```csharp
try { await _invitations.TryGrantRewardForVerifiedUserAsync(user.Id); } catch { log }
```

Granting at registration would let an attacker mint N × 30 days from N disposable addresses with no mailbox at all. Granting at verification forces control of a real, receiving mailbox per fake org.

Known gap, worth one line in the spec: an invitee who never clicks verify but later does a password reset also gets `EmailVerifiedAt` set (in `ResetPasswordAsync`) and will not trigger the reward. Rare enough to accept in exchange for a single call site.

Note the two separate moments are both truthful: `Status = Accepted` flips at **registration** (they did join), `RewardGrantedAt` fills at **verification**. The `/invite` page shows both numbers separately.

### 6.3 The grant algorithm — the dangerous part

`Organization.PlanValidUntil` on the **inviter's** organization only. Never the invitee's. Write it as one function with the invariants stated as invariants:

```
GrantReward(org, days, now):

  // ⚠ Case 0 — LIFETIME PLAN. PlanValidUntil == null means "bez isteka"
  //   (early adopters / Partner). Setting it to today+days would CONVERT AN
  //   UNLIMITED PLAN INTO ONE THAT EXPIRES IN 30 DAYS. Grant nothing.
  if (org.PlanValidUntil is null && org.Plan != Free)
      → no change; record RewardDays = 0, RewardGrantedAt = now (counted, not paid)

  effective = PlanHelper.Effective(org.Plan, org.PlanValidUntil, now)

  // Case (ii) — expired or Free → this is an upgrade
  if (effective == Free)
      org.Plan           = Pro
      org.PlanValidUntil = now.Date.AddDays(days)

  // Cases (i) trial and (iii) paying customer → extend only, never touch Plan
  else
      org.PlanValidUntil = Max(org.PlanValidUntil.Value, now.Date).AddDays(days)
```

Invariants, each of which must be an acceptance criterion:

- **Never lowers `PlanValidUntil`.** The `Max(existing, today)` makes the operation monotonic.
- **Never changes `Plan` downward, and never converts a paid plan.** `Plan` is only ever *raised*, only ever to `Pro`, and only when the effective plan is `Free`. A Max or Partner customer keeps their plan and simply gets more days. Note the rule is written as `effective == Free` rather than an ordinal comparison, so it stays correct regardless of `PlanType`'s numeric ordering.
- **A lifetime plan is never given an expiry date.** This is the one that silently destroys a Partner org, and it is the reason the null check comes first.

### 6.4 Audit trail

`PlanNotes` is **`HasMaxLength(300)`** (`Melarium.Entity/Configurations/OrganizationConfiguration.cs`). Appending a line per reward overflows it after a handful of grants and throws on `SaveChanges` — which, without care, would fail the *invitee's verification request*. Two consequences:

- **Marker-line rewrite, not append.** `PlanNotes` = `{everything before the marker}` + `\n` + `Bonus od pozivnica: +{totalDays} dana ({count})`. Bounded, idempotent, and it preserves the operator's own manual text. If the result still exceeds 300, truncate the operator part — never drop the marker.
- **The itemised truth lives in the ledger**, not in `PlanNotes`: `SELECT … FROM Invitations WHERE InviterOrganizationId = X AND RewardGrantedAt IS NOT NULL`. `PlanNotes` is the at-a-glance summary a SystemAdmin sees on the org row; the table is the receipt.
- Surface the same number on the user's own `/plans` page via `MyPlanDto` — one extra field, `RewardDaysFromInvitations`.
- Wrap the whole grant in try/catch. **A reward failure must never fail the invitee's verification.**

### 6.5 Anti-fraud

| Vector | Control |
|---|---|
| Self-invite | reject `email == inviter's email` at send (table stakes only — farms use other addresses) |
| `+tag` aliases | `EmailCanonical` (§2.3) |
| Disposable addresses | **no blocklist by default**; verification gate + caps + `Invitations:BlockedEmailDomains` config escape hatch. Add a curated list only if abuse is observed. |
| Same person, many accounts | **no IP tracking in v1** — the app stores no registration IPs and adding them is a privacy decision that deserves its own discussion. Rely on caps + ledger + clawback via the existing `PUT /admin/organizations/{id}/plan`. |
| Invite farms / rings | per-org lifetime cap. Cycle detection is over-engineering; note and skip. |
| Same invitee rewarded twice | **at most one reward per accepted organization** — `AnyRewardForOrganizationAsync(newOrgId)` before granting. Closes "invite the same person from two of my accounts". |
| Volume | `Invitations:Reward:DaysPerAccepted` = 30, `MaxDaysPerOrganization` = **180 lifetime**, `MaxRewardedPer30Days` = **5**. Two caps because they bound different attacks: the lifetime cap bounds total loss per org, the rolling cap makes any attack slow enough to notice. |

---

## 7. The emails

### 7.1 Fixing the shared template — the minimal change

The problem, precisely: `EmailNotificationWorker.BuildHtml` (`backend/Melarium.Infrastructure/Email/EmailNotificationWorker.cs:94-136`) greets `Pozdrav <strong>{name}</strong>,` — where `name` falls back to the raw email address for `ForAddress` mail — and footers with *"Ovu poruku ste primili jer imate nalog na Melarium aplikaciji."* For an invitee both are false, and the footer is worse than cosmetic: a false statement about **why you received this mail** is precisely what gets a sender reported as spam (T5).

**Proposal — two optional fields, ~10 lines total:**

```csharp
public sealed record QueuedEmail(
    int? UserId, string Title, string Message,
    string? ActionUrl = null, string? ActionLabel = null,
    string? ToEmail = null, string? ToName = null,
    string? Greeting = null,      // null → "Pozdrav {resolvedName},"
    string? Footer   = null);     // null → the account-holder footer
```

`ForAddress` gains the two optional parameters; `ForUser` is untouched; all **four** existing production call sites keep compiling and behaving identically — `AuthService.cs:182` (password reset), `AuthService.cs:270` (email verification), `NotificationService.cs:43` (every bell notification) and `FeedbackService.cs:254` (the SPEC-13 operator mail). *(An earlier draft said five; verified against the code on 2026-07-30, there are four.)* `BuildHtml` becomes `item.Greeting is { Length: > 0 } g ? Escape(g) : $"Pozdrav <strong>{safeName}</strong>,"` and the same shape for the footer.

Note the design detail that avoids a null-vs-empty-string smell: the invitation passes `Greeting = "Pozdrav,"` — a perfectly natural Bosnian greeting to an unnamed person — so `null` can keep meaning "default" and there is no "empty means omit" special case.

**Rejected alternatives:** a second `BuildExternalHtml` (duplicates the layout; one of the two drifts when the brand changes); an `EmailAudience` enum (hardcodes product copy in an Infrastructure worker, when every other feature's Bosnian copy lives next to the feature that sends it — `AuthService` writes its own bodies); calling `IEmailService` directly (puts SMTP back on the request path, which is exactly what ADR-021 removed).

### 7.2 The `\n → <br>` fix — yes, fix it

`BuildHtml` HTML-escapes the message but never converts newlines, so multi-line bodies collapse into one paragraph. SPEC-13's operator email already suffers this today. The invitation body genuinely needs paragraphs (intro / quoted personal message / value proposition), so:

```csharp
var safeMessage = Escape(item.Message).Replace("\r\n", "\n").Replace("\n", "<br>");
```

One line. **The order is load-bearing and must be spelled out in the spec**: escape *first*, then insert `<br>` — reversed, it is an XSS hole. Strictly additive (no current message contains `\n` except SPEC-13's, which is currently broken and gets fixed for free), and the alternative condemns every future email to a single paragraph.

The personal message is rendered as a quoted block **inside** `Message` (`Poruka od {Ime}: „…"`) rather than as a new `QuotedBlock` field — minimal, and it inherits the worker's escaping for free.

### 7.3 Kind A email — copy

| | |
|---|---|
| **Title** | `{Ime} {Prezime} vas poziva` |
| **Subject** (derived) | `Melarium — Marko Marković vas poziva` |
| **Greeting** | `Pozdrav,` |
| **Body** | `{Ime} {Prezime} ({email}) koristi Melarium — aplikaciju za vođenje pčelinjaka — i poziva vas da se pridružite.`<br>`Poruka od {Ime}: „…"` *(only when present)*<br>`U Melariumu vodite evidenciju pregleda, matica, tretmana i vrcanja, dobijate upozorenja (mraz, kašnjenje pregleda, kraj karence) i imate AI savjetnika na bosanskom. Registracija je besplatna, a novi računi dobijaju 30 dana Pro paketa.` |
| **Button** | `Prihvati poziv` → `{FrontendUrl}/register?ref={code}` |
| **Footer** | `Ovu poruku ste primili zato što vam je pozivnicu poslao korisnik {Ime} {Prezime} ({email}). Ako ne poznajete tu osobu, slobodno zanemarite ovu poruku — nismo napravili nikakav račun na vaše ime.` |

Two deliberate choices worth defending in the spec:

- **Title carries the friend's name, not the product's.** The worker prefixes every subject with `Melarium — `; to a stranger "Melarium" means nothing, and the friend's name is the entire reason the mail gets opened. `"{Ime} {Prezime} vas poziva"` reads correctly in Bosnian after the prefix and needs **no subject-override mechanism** — a smaller diff than adding one.
- **The footer claims only what is true.** Not "we will never write to you again" (the global cap makes that only mostly true) — just "we did not create an account in your name". The inviter's address appears as display text, not a `Reply-To` (extending `IEmailService` for one header is not worth it).

Grammar note: `"Ovu poruku ste primili zato što vam je pozivnicu poslao korisnik {Ime}"` — `poslao` agrees with the masculine noun *korisnik*, so it stays correct regardless of the inviter's gender, avoiding "poslao/la".

### 7.4 Kind B email — copy

| | |
|---|---|
| **Title** | `Pozivnica u organizaciju {Naziv}` |
| **Body** | `{Ime} {Prezime} vas poziva da se pridružite organizaciji „{Naziv}" u Melariumu, u ulozi: {Uloga}.`<br>`Poruka od {Ime}: „…"`<br>`Kliknite na dugme ispod da postavite svoju lozinku i aktivirate račun. Link vrijedi {N} dana i može se iskoristiti samo jednom.` |
| **Button** | `Postavi lozinku i pridruži se` → `{FrontendUrl}/join?token={raw}` |
| **Footer** | `Ovu poruku ste primili zato što vas je korisnik {Ime} {Prezime} ({email}) pozvao u organizaciju „{Naziv}". Ako ovo niste očekivali, zanemarite poruku — bez klika na link račun se ne kreira.` |

The "vrijedi N dana / samo jednom" sentence is deliberate: it is standard credential-email hygiene and it matches the language of the existing reset-password mail.

All URLs built with `BuildFrontendUrl`'s exact fallback chain — `_config["FrontendUrl"] ?? _config["App:PublicBaseUrl"] ?? "http://localhost:5173"` (`AuthService.cs:304-308`). Extract it once rather than copying the chain into a second service.

---

## 8. API surface

`InvitesController`, `api/invites`, `[Authorize]` at class level.

| Method | Path | Auth / limiter | Body → Returns |
|---|---|---|---|
| POST | `/api/invites` | `[Authorize]`, `invite` | `{ kind, email, personalMessage?, memberRole?, memberApiaryId? }` → `201 InvitationDto` |
| GET | `/api/invites/mine` | `[Authorize]` | → `InvitationDto[]`, newest first |
| GET | `/api/invites/summary` | `[Authorize]` | → `{ sentCount, acceptedCount, rewardDaysEarned, rewardDaysCapRemaining, shareUrl }` |
| DELETE | `/api/invites/{id}` | `[Authorize]` | → `204`; **kind B only**, owner or the org's OrgAdmin; sets `Revoked` |
| GET | `/api/invites/ref/{code}` | `[AllowAnonymous]`, `auth-token` | → `{ inviterFirstName }` \| 404 |
| GET | `/api/invites/join/{token}` | `[AllowAnonymous]`, `auth-token` | → `{ organizationName, inviterFirstName, email, roleName }` \| 404 |
| POST | `/api/auth/accept-invitation` | `[AllowAnonymous]`, `auth-token` | `{ token, firstName, lastName, password }` → `LoginResponseDto` |

Distinct literal path segments (`mine`, `summary`, `ref/…`, `join/…`) so nothing relies on literal-beats-parameter route precedence.

**DTOs**

```csharp
public record CreateInvitationDto(
    InvitationKind Kind, string Email, string? PersonalMessage,
    UserRole? MemberRole, int? MemberApiaryId);

public record InvitationDto(
    int Id, InvitationKind Kind, string KindName,
    string Email, InvitationStatus Status, string StatusName, bool IsExpired,
    string? PersonalMessage, string? MemberRoleName,
    DateTime? AcceptedAt, int? RewardDays, DateTime CreatedAt);
```

`InvitationDto` **never** carries a kind B token, and `Suppressed` is mapped to `Sent`/"Poslano" for the caller (§5.3). The kind A `shareUrl` lives on the **summary** DTO, not per-row, because the referral code is personal, not per-invitation — and it is built server-side because the client does not know `FrontendUrl` (which differs from the browser origin under the dev proxy).

**Validation** (`CreateInvitationValidator`, Bosnian messages per the newer `CreateFeedbackValidator` convention, not `RegisterValidator`'s English):

- `Kind` `IsInEnum`.
- `Email` `NotEmpty`, `EmailAddress()`, `MaximumLength(256)`.
- `PersonalMessage` `MaximumLength(200)` + the no-URL rule.
- `MemberRole` required when `Kind == OrganizationMember`, and **must be `ApiaryAdmin` or `Beekeeper`** — an explicit set, never an ordinal comparison, because `UserRole`'s numeric values are not ordered by privilege (`ApiaryAdmin = 1`, `SystemAdmin = 2`, `OrganizationAdmin = 3`, `Beekeeper = 4`).
- `MemberApiaryId` required when `MemberRole == ApiaryAdmin`.

**Status codes:** `201` for create (no `Location` — there is no single-invitation GET, and adding one purely to satisfy `CreatedAtAction` is ceremony); `422` for every business rule (matching `BusinessRuleException`'s existing mapping); `402 plan-limit` only for the *authenticated* OrgAdmin seat check, never for the anonymous invitee (§4.2 rule 4); `429` from the limiter.

`IAccessGuard` is **not** used, for the same reason SPEC-13/ADR-030 gave: an invitation is not in the Organization → Apiary → Beehive hierarchy. Scoping is "your own rows" via `InviterUserId`. `IPlanGuard` **is** used, but only for kind B seats — never to gate the ability to refer, which would be self-defeating.

---

## 9. Frontend

### 9.1 Page, not modal

Modal (the `FeedbackFormModal` shape) loses on four counts: no route means **no `helpRoutes` entry and therefore no help icon** (SPEC-14 resolves help by route); no deep link, so `MembersPage` cannot send an admin to the member-invite flow; no room for the history list, the share card and the stats strip; and the user literally asked for a *"page"*. **`/invite`**, inside the protected `Layout` block, all authenticated roles.

### 9.2 `/invite` layout

1. **Kind selector** — two radio cards. "Pozovi na Melarium" (default, everyone). "Pozovi u moju organizaciju" (**rendered only for `isOrgAdmin`**), carrying the warning *"Osoba koja prihvati ovu pozivnicu vidjeće podatke vaše organizacije."* Preselected by `?kind=member`.
2. **Form** — React Hook Form (ADR-007): email; optional message with a live character counter; for kind B, role select (Pčelar / Admin pčelinjaka) and an apiary select shown only for the admin role. On failure `setError('root', …)` or a toast, **not** a per-field error dictionary — `apiClient`'s interceptor flattens backend errors to a single `Error.message`, exactly as SPEC-13 documented.
3. **Share card — kind A only.** Read-only input with `shareUrl`, **Kopiraj** (the `navigator.clipboard.writeText` + 2-second `copied` state pattern copied verbatim from `CalendarSettingsPage.tsx:25-32`) and **Podijeli** via `navigator.share({ title, text, url })` behind a capability check. No hardcoded `wa.me` / `viber://` deep links — they break, and the native share sheet already lists both. In BiH this card will out-convert the email channel and it costs ten lines.
4. **Stats strip** — "Poslano N · Pridružilo se M · Osvojeno +K dana".
5. **History list** — kind badge, email, status badge (the `STATUS_STYLE` record pattern from `MyFeedbackSection`/`TreatmentsPage`), date; kind B pending rows get **Poništi**. **No copy-link button on kind B rows, ever.**
6. Empty state via the shared `EmptyState` with `onHelp` (SPEC-14).

### 9.3 `/join` — new public page

Modelled directly on `ResetPasswordPage`: reads `?token=`, and with no token renders the `AuthCard` "Link nije potpun" branch instead of a form guaranteed to fail. With a token it calls the preview endpoint and shows *"Pozvani ste u organizaciju „{Naziv}" kao {Uloga}"*, the invited email read-only, then first name / last name / password / confirm. On success the response is a normal `LoginResponse` → `persistSession` → navigate to `/apiaries`.

### 9.4 Wiring checklist

- `core/models/index.ts` — new `// ── Invitations (SPEC-15) ──` section: `InvitationKind` + labels, `InvitationStatus` + `InvitationStatusLabels`, `Invitation`, `InvitationSummary`, `CreateInvitationPayload`.
- `core/services/inviteService.ts` + `core/services/inviteQueries.ts` (per-feature convention, matching `feedbackService`/`feedbackQueries`).
- `features/invites/InvitePage.tsx`, `features/auth/JoinPage.tsx`.
- `App.tsx` — `<Route path="/join" element={<JoinPage />} />` in the **public** block next to `/reset-password`; `<Route path="invite" element={<InvitePage />} />` inside `Layout`.
- `Layout.tsx` — "Pozovi prijatelja" (`UserPlus`) in the **desktop profile dropdown *and* the mobile panel**. There are two copies of that menu (lines ~248 and ~356) and missing the second is the classic bug here. Not the sidebar: `getNavItems` is the daily working set, and a referral link is not a daily tool.
- `core/help/helpRoutes.ts` — add `'/invite'` (no prefix conflicts; place near `/plans`/`/profile`). `/join` gets none — it is outside `Layout`, like `/reset-password`.
- `core/help/helpContent.ts` — add the `'/invite'` entry. **Required**: `HELP_CONTENT` is `Record<HelpKey, HelpEntry>`, so omitting it is a compile error.
- `core/services/authService.ts` — `RegisterPayload` gains `referralCode?`; new `acceptInvitation()` on the bare `authApi` client (it must not pass through the refresh-on-401 interceptor, same as every other auth call).
- `features/auth/RegisterPage.tsx` — read `?ref=`, pass through, render the inviter banner.
- `features/members/MembersPage.tsx` — remove the password field from the create form; route "Novi član" to `/invite?kind=member`.
- `shared/components/CommandPalette.tsx` — one nav entry, "Pozovi prijatelja" (optional, one line).
- Backend registration: `IInvitationRepository` on `IUnitOfWork` + `UnitOfWork`, `IInvitationService` in `Program.cs`, `BsLabels.Label(InvitationKind/InvitationStatus)`, `NotificationType.InvitationAccepted = 24` (**23 is reserved by SPEC-12 Phase D**), config placeholders in `appsettings.json` + `.env.example`.

---

## 10. Improvements the raw idea did not include

Earning their place in v1:

1. **Shareable link + Web Share** — Bosnian beekeepers coordinate on Viber/WhatsApp, not email, and it sidesteps deliverability entirely.
2. **Email-match attribution fallback** — without it the status column is fiction (§3.1).
3. **Verified-email gate to send** — the highest-leverage anti-abuse control and it costs one `if`.
4. **URL ban in the personal message** — protects the sending reputation that password reset depends on.
5. **`\n → <br>` template fix** — one line, fixes SPEC-13's operator mail as a side effect.
6. **`InvitationAccepted` notification** — one enum value, two trigger points, two messages: at acceptance *"{Ime} se pridružio Melariumu preko vaše pozivnice."*, and at reward grant *"Potvrdio je e-poštu — dobili ste +{N} dana Pro paketa."* This is the emotional payoff that produces a second invitation. **Use the friend's first name only, never their chosen email address** — if they registered with a different address than the one invited, echoing it back leaks their private address to the inviter.
7. **Deploy note:** this feature materially raises the stakes on SPF/DKIM/DMARC and shares the Resend quota with password-reset mail. It belongs in the deploy checklist next to `FEEDBACK_NOTIFY_EMAIL`.

Explicitly **out of scope v1** (name them so they are decisions, not omissions): reward tiers/leaderboards/cash payouts; a referral analytics dashboard; bulk or CSV invite; contact-book import; a recipient opt-out/suppression list (`POST /invites/{code}/opt-out` is the obvious v2 shape); disposable-domain blocklist curation; registration-IP fraud signals; reminder/resend of a kind A invitation (the share link is a better answer to "it went to spam"); carrying beehive assignments on a kind B invitation; localising the invitation email. (*Removing `POST /api/org/members` was on this list until 2026-07-30; it is now **in** scope — see §4.3.*)

---

## 11. Phases

- **Phase A — invitations core (kind A) + email layer.** Entity + 3 enums, the single `AddInvitations` migration (table + `User.ReferralCode`), repository + UoW, `InvitationService`, `InvitesController` (`POST`, `mine`, `summary`), `QueuedEmail` greeting/footer + the `<br>` fix, the `invite` rate-limit policy, config, `BsLabels`.
- **Phase B — attribution + reward.** `RegisterDto.ReferralCode` + validator, `TryAttributeRegistrationAsync` seam, `GET /invites/ref/{code}`, the verification hook, `GrantReward` + `PlanNotes` marker line + caps, `NotificationType.InvitationAccepted = 24`, `MyPlanDto.RewardDaysFromInvitations`.
- **Phase C — kind B.** Seat checks (invite-time courtesy + acceptance-time authority), `GET /invites/join/{token}`, `POST /auth/accept-invitation`, revoke, org-member notification. **Plus the removal of `POST /org/members`** + `CreateMemberAsync` + its DTO/validator, and the `api-contracts.md` entry recording it.
- **Phase D — frontend.** Models, service + queries, `/invite`, `/join`, `/register` banner, Layout ×2, help ×2, `MembersPage` repoint (**delete the create form, not just repoint the button** — the endpoint behind it is gone).

**A and B must deploy together** (A alone leaves every invitation permanently "Poslano" — a visible lie), mirroring SPEC-12's "Phases A and B must deploy together".

**C and D must also deploy together**, and this is a consequence of the "delete `POST /org/members`" decision rather than anything intrinsic to the design. Removing the endpoint (C) while `MembersPage` still calls it (D) leaves every OrgAdmin unable to add a member, with a broken form as the only visible sign. Had the endpoint merely been deprecated, C would have been independently shippable. Concretely: **A+B first, then C+D as one deploy.**

---

## 12. Acceptance criteria

- [ ] Any authenticated user can send a kind A invitation from `/invite`; the recipient gets one email naming the inviter.
- [ ] A user whose `EmailVerifiedAt` is null cannot send invitations and is told why, with a route to fix it.
- [ ] Sending is rate-limited (`invite` 3/min per IP) **and** capped per user (10 per rolling 24 h → 422 with a Bosnian message, not 429).
- [ ] Inviting the same address twice within the cooldown is refused with the caller's own history as the reason; inviting an address that already has an account returns a response **indistinguishable** from a successful send and sends no email.
- [ ] A fourth distinct inviter to the same address within 30 days is refused with *"Ova adresa je nedavno već pozvana."* — and that message **never names or implies who** sent the earlier invitation.
- [ ] A personal message containing a URL is rejected; a message over 200 chars is rejected; a message that is sent renders as an attributed quote block, HTML-escaped.
- [ ] Registering with `?ref={code}` credits the inviter; registering with an **unknown, expired or malformed** code still succeeds.
- [ ] Registering with no code but an email matching a pending invitation still flips that invitation to `Accepted`.
- [ ] The reward is granted only when the invitee **verifies their email**, never at registration, and never for kind B.
- [ ] Reward on an org that is (i) on an active Pro trial → `PlanValidUntil` extended, `Plan` unchanged; (ii) expired/Free → `Plan = Pro`, `PlanValidUntil = today + N`; (iii) a paying Standard/Max/Partner customer → `PlanValidUntil` extended, **`Plan` untouched**.
- [ ] **An organization with `PlanValidUntil = null` (bez isteka) is never given an expiry date by a reward.**
- [ ] `PlanValidUntil` never moves backwards; the per-org lifetime cap (180 d) and rolling cap (5 / 30 d) are enforced; one invitee organization can be rewarded at most once.
- [ ] `PlanNotes` carries a single rewritten `Bonus od pozivnica: …` line, never exceeds 300 characters, and never destroys an operator's manual note. A reward failure never fails the invitee's verification request.
- [ ] Kind B is offered only to OrganizationAdmins; its token is stored **hashed**, is single-use, expires in 7 days, and is **never** returned by any API or shown in any UI.
- [ ] Seats are checked at invite time (counting pending invitations) **and again at acceptance**; a seat that filled in between yields an invitee-appropriate 422, not a 402 telling an anonymous stranger to upgrade a plan.
- [ ] Accepting a kind B invitation creates the member with the invited email, `EmailVerifiedAt` set, **zero** beehive assignments, and auto-logs them in. The token cannot be reused.
- [ ] A revoked or expired kind B token, and a kind B token for a deleted apiary, are all refused with one message.
- [ ] `POST /api/org/members` and `CreateMemberAsync` are **gone** (404), with no dead DTO, validator or frontend call left behind, and `MembersPage` no longer asks an OrgAdmin to choose another person's password.
- [ ] Adding a member end-to-end works through kind B alone — verified after C+D deploy together, since that is the only remaining path.
- [ ] The **four** existing `QueuedEmail` call sites (password reset, email verification, bell notifications, SPEC-13 operator mail) still produce byte-identical mail after the `Greeting`/`Footer`/`<br>` change.
- [ ] An OrgAdmin who invites a Beekeeper ends up with a member holding **zero** hive assignments until they visit `MemberAssignmentPage` — verified as the intended flow, not reported as a bug (§2.3).
- [ ] All user-facing strings Bosnian; enum labels via `BsLabels` (backend) and label maps (frontend); `/invite` has both a `helpRoutes` and a `helpContent` entry.
- [ ] Docs updated: `features/invitations.md` (new), `api-contracts.md` (incl. the `POST /org/members` **removal**), `context.md`, `decisions.md` (new ADR), `features/plans-billing.md` (the only feature doc that describes the deleted member-create flow), `.env.example`.

---

## 13. Decisions taken (2026-07-30, Asim)

All six of the design's open questions are answered, plus one more that a regression review raised
afterwards. Nothing here is open; start Phase A.

1. **Already-has-an-account (kind A)** → **silent suppression**, as recommended. Record `Suppressed`, send nothing, return a response byte-identical to a real send. The known cost stands and is accepted: the inviter's list shows "Poslano" for a mail that was never sent — the one place this design knowingly shows something untrue (§5.3).
2. **Global per-recipient cap message** → **honest, not vague** (PO override of the recommendation). 422, *"Ova adresa je nedavno već pozvana."*, worded so it never says by whom. Full reasoning and the accepted leak are in §5.2 #5.
3. **Numbers** → **the proposed defaults, unchanged**: 10 invites/user/24 h · 30-day per-recipient cooldown · +30 reward days per accepted-and-verified invitation · 180-day lifetime cap per organization · max 5 rewards per rolling 30 days · 7-day kind B token. All of these are config keys with these values as defaults, so tuning later needs no deploy.
4. **Kind B reward** → **none**, as recommended. Organization-member invitations earn no plan days, for the three reasons in §6.1 — the decisive one being that acceptance sets `EmailVerifiedAt` itself, so the verification gate that the whole anti-fraud model rests on does not apply to kind B at all.
5. **`POST /api/org/members`** → **delete now** (PO override of the recommendation). See §4.3 for what this gives up and §11 for the phase-ordering rule it forces (C and D must deploy together).
6. **Referral code entropy** → 128-bit, as instructed. Recorded here as the design asked: **the reward is defended by the verification gate (§6.2) and the caps (§6.5), not by the code's length.** Do not relax a cap later on the grounds that the code is unguessable — guessing someone's code only credits *them*, and the real fraud vector (many fake signups under your own code) is entirely unaffected by entropy (§3.2).
7. **Beehive pre-assignment on kind B** *(raised by the regression review, not in the original design)* → **no**. Keep §2.3 as written. Today's `MembersPage` creates the member and assigns their hives in one step; after this spec that is two steps, and the second is only possible once the invitee accepts. Accepted rather than adding an `InvitationBeehive` junction table and handling hives deleted between invite and accept.

### Known regressions this spec accepts

Collected in one place so they are answered once, in review, rather than rediscovered as bugs:

| What is lost | Where | Mitigation |
|---|---|---|
| OrgAdmin creating a member with a password they choose | §4.3 | none for OrgAdmin; SystemAdmin keeps `POST /api/admin/users` |
| Onboarding a member with no reachable email | §4.3 | operator does it manually |
| Putting someone to work within the minute (password handed over in person) | §4.3 | none — the invitee must receive mail and click |
| Assigning hives in the same step as creating the member | §2.3 | second visit to `MemberAssignmentPage` after acceptance |

### Critical files for implementation

- `backend/Melarium.Application/Features/Auth/AuthService.cs`
- `backend/Melarium.Infrastructure/Email/EmailNotificationWorker.cs`
- `backend/Melarium.Application/Common/Security/PlanGuard.cs`
- `backend/Melarium.Application/Features/OrgManagement/OrgManagementService.cs`
- `frontend/src/features/auth/RegisterPage.tsx`
- `backend/Melarium.API/Program.cs` (rate-limit policies, lines 176-285)
