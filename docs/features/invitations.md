# Invitations — "Pozovi prijatelja" (SPEC-15)

> **Phase 1 implemented 2026-08-06** — the personal share link, attribution and the reward.
> **Phase 2 (sending invitations by e-mail) is not built yet.**
> Spec: [`specs/SPEC-15-invite-friend.md`](../specs/SPEC-15-invite-friend.md) · ADR-032.

## What it is

An existing customer shares a personal link. Whoever registers through it gets their **own**
organization with a longer trial, and the inviter's organization earns plan days once the invitee
proves their e-mail address is real.

This is **not** a way to add a member to your own organization. The invitee is a new customer with a
separate organization; nothing in this feature grants access to anyone's data.

| | |
|---|---|
| Invitee gets | `Invitations:InviteeTrialDays` = **60** days of Pro instead of the standard 30, at registration |
| Inviter gets | **+30** days on the plan their organization already has, when the invitee **verifies their e-mail** |
| Ceiling | **180** days lifetime per organization, ≤ 5 rewards per rolling 30 days, one reward per invited organization ever |

## Model

`User.ReferralCode` — nullable, unique (filtered index), 128-bit hex, minted lazily the first time the
user opens `/invite`. Stored **as-is, not hashed**, because the URL must be shown to its owner again —
the same "secret address" model as `CalendarSettings.FeedToken`. There is no rotation endpoint: a
leaked link only earns its owner more invitations, bounded by the cap.

`Invitation` (table `Invitations`) — one flat table.

| Field | Notes |
|---|---|
| `Source` | `EmailInvite` \| `ShareLink`. A share-link row is born **already accepted** — there was never a send |
| `Status` | `Sent` \| `Accepted`. No `Expired` (computed, per `PlanHelper`'s idiom) and no `Suppressed` |
| `InviterUserId?`, `InviterOrganizationId?` | The organization is snapshotted: a user can move, but the days were earned by the org they were in |
| `Email`, `EmailCanonical` | Canonical strips a `+tag` **for comparison only** — never for the address we mail. Dots are **not** stripped: significant at most providers |
| `AcceptedByUserId?`, `AcceptedOrganizationId?` | The accepted organization is the "one reward per invited organization" guard |
| `RewardDays?`, `RewardGrantedAt?` | `RewardGrantedAt` is set **even when 0 days were granted**, so a capped or lifetime-plan case is settled and not re-examined forever |

Every FK is `ON DELETE SET NULL`: deleting an account must not delete the ledger that justifies an
organization's extended plan.

## Attribution

Three signals, in priority order:

1. **`?ref={code}`** on `/register` — the primary signal.
2. **Address match** — no code, but we had already invited that canonical address; the most recent
   still-`Sent` row is credited. Without this fallback, anyone who loses the mail and types the URL by
   hand leaves the inviter's list stuck at "Poslano" forever.
3. Neither → nothing is credited.

The link wins over the address when they disagree: following someone's link is an explicit act, a
matching address is an inference.

**Only one invitation is credited per registration.** Other pending rows to the same address stay
`Sent` on purpose — two people being paid for one sign-up is exactly what the caps exist to prevent.

> **Invariant:** an unknown, expired or malformed code **never fails a registration**. The account is
> created with the standard 30-day trial and attribution is simply lost.

## The reward

`IPlanCredit.GrantDaysAsync` (beside `PlanGuard`, not in this feature) is the only code allowed to do
arithmetic on `Organization.PlanValidUntil`. Four states:

| Inviter's organization | What happens |
|---|---|
| Lifetime plan (`PlanValidUntil is null`, plan ≠ Free) | **0 days, nothing written.** Writing `today + 30` would convert an unlimited plan into one that expires |
| Expired or Free | `Plan = Pro`, `PlanValidUntil = today + 30` — doubles as a reactivation path |
| Active trial | Date extended, `Plan` untouched |
| Paying Standard / Max / Partner | Date extended, `Plan` untouched — never "upgraded" to Pro, which for Max or Partner is a downgrade |

Two rules that look like details and are not:

- The upgrade test is `effective == Free`, **not** an ordinal comparison. `PlanType` runs Free=1 …
  Partner=5, so `Plan < Pro` would silently downgrade a Partner organization.
- **The reward never touches `PlanNotes`.** `PlansPage` detects the trial with
  `planNotes === 'Probni period'` — an exact match — so an audit line written there would remove the
  trial notice from the plans page. The itemised record lives on the invitation rows.

**Ordering:** attribution runs after `IssueTokensAsync`, the reward after the verification
`SaveChangesAsync`. Both share the request's `IUnitOfWork`, so running either earlier would let a
failure leave dirty entities that re-throw on the next save — taking down token issuance, or leaving
the user unverified and unable to fix it by retrying. `try/catch` does not cover that; ordering does.

## Notifications

`NotificationType.InvitationAccepted = 24`, two moments:

- at registration — *"{Ime} se pridružio Melariumu preko vaše pozivnice."*
- at the reward — *"…dobili ste 30 dana Pro paketa."* on an upgrade, *"…vaš paket je produžen za 30
  dana."* otherwise, and **nothing at all when 0 days were granted**.

Always the invitee's **first name only** — if they registered with a different address than the one
invited, echoing it back would leak their private address to the inviter.

## Surface

`/invite` (page, inside `Layout`, all roles) — share card with **Kopiraj** / **Podijeli**, a stats
strip, and the caller's own history. Reached from the profile menu in **both** its desktop and mobile
copies. Help entry: `/invite`.

Endpoints: see [`api-contracts.md`](../api-contracts.md).

## Configuration

`Invitations:InviteeTrialDays` (60) · `Invitations:Reward:DaysPerAccepted` (30) ·
`MaxDaysPerOrganization` (180) · `MaxPerRolling30Days` (5) · `RollingWindowDays` (30). All have
working defaults in `appsettings.json` and are overridable from `.env` — tuning the offer needs no
redeploy.

## Tests

`InvitationRewardTests` locks every branch of the grant algorithm, including the lifetime-plan case and
`PlanNotes` being untouched. `RegistrationTrialTests` covers the 60-day invited trial and that a
throwing attribution still leaves a working registration; `EmailVerificationTests` covers that a
throwing reward still leaves the user verified.

## Not built yet (Phase 2)

Sending an invitation by e-mail: `POST /invites`, the invitation e-mail itself, the different mail for
an address that already has an account, the per-user daily cap, the per-recipient cooldown, the
no-URLs rule on the personal message, and the `invite` rate-limit policy. Until then `Source` is always
`ShareLink` in practice.

**Open policy item:** retention of the addresses of people who never accept — see SPEC-15 §12.
