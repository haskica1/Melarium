# Plans & Billing (SPEC-09)

Per-organization subscription plans with limit + AI gating. **v1 billing is manual and annual**
(SystemAdmin activates the plan after a bank transfer; Stripe is unavailable to BiH merchants, so
automated payments via Paddle are Phase 2). Implemented 2026-07-05.

## Plans

| | Besplatni | Standard 20 KM/mj | Pro 35 KM/mj | Max 50 KM/mj | Partner (skriven) |
|---|---|---|---|---|---|
| Pčelinjaci | 1 | ∞ | ∞ | ∞ | ∞ |
| Košnice | 7 | 30 | 100 | ∞ | ∞ |
| Dodatni članovi (uz vlasnika) | 0 | 2 | 5 | ∞ | ∞ |
| Pašnjaci i selidbe | ✗ | ✓ | ✓ | ✓ | ✓ |
| Glasovni unos, sedmični AI sažetak | ✗ | ✓ | ✓ | ✓ | ✓ |
| AI savjetnik | ✗ | 10 poruka/mj | ∞ | ∞ | ∞ |
| Foto + AI analiza okvira (SPEC-05) | ✗ | ✗ | ✓ | ✓ | ✓ |
| Prioritetna podrška | ✗ | ✗ | ✗ | ✓ | ✓ |

Annual collection with a "2 mjeseca gratis" discount: 200 / 350 / 500 KM/god. **Partner** is a
hidden plan (= Max in enforcement) that only SystemAdmin can assign and that never appears in any
public plan list or checkout — for friends/family/associations/demo accounts.

## Domain

- `Organization` += `Plan PlanType { Free=1, Standard=2, Pro=3, Max=4, Partner=5 }` (default Free),
  `PlanValidUntil DateTime?` (null = doživotno), `PlanNotes string(300)?`. Migration
  `AddOrganizationPlan` (existing rows default to Free).
- **Effective plan is computed, never stored** — `PlanHelper.Effective(plan, validUntil, utcNow)`:
  a paid/Partner plan whose `PlanValidUntil` is before today (date compare — valid through end of
  the expiry day) behaves as **Free**. No background job flips anything.
- **Trial**: registration creates the org as `Plan = Pro`, `PlanValidUntil = today + Plans:Trial:Days`
  (30), `PlanNotes = "Probni period"` — a pre-set expiring Pro, no extra machinery.

## Enforcement — `IPlanGuard` (Common/Security, `IAccessGuard` precedent)

Limits live in config (`Plans:{PlanType}:{Key}`, absent key = unlimited); Max/Partner have no
entries. Enforced **on create only** — downgrades never lock existing data. The org-less
SystemAdmin bypasses all gates.

| Method | Called by | Config key |
|---|---|---|
| `EnsureCanAddApiaryAsync` | `ApiaryService.CreateAsync` | `MaxApiaries` |
| `EnsureCanAddBeehiveAsync` | `BeehiveService.CreateAsync` | `MaxBeehives` |
| `EnsureCanAddMemberAsync` | `OrgManagementService.CreateMemberAsync` | `MaxMembers` (counts accounts beyond the owner) |
| `EnsureFeatureAsync(feature)` | voice parse; advisor transcribe; pasture/move create; photo analyze; weekly worker | boolean by tier |
| `EnsureAdvisorMessageAsync` | advisor create/send | `AdvisorMessagesPerMonth` (COUNT org user-messages in current UTC month) |
| `GetMyPlanAsync` | `/organizations/my-plan` | — |

`PlanFeature`: `VoiceInput`/`WeeklySummary`/`Pastures` need Standard+; `PhotoAnalysis` needs Pro+.
Violations throw `PlanLimitException` → **402** with `code: "plan-limit"` + Bosnian message
(`GlobalExceptionMiddleware`). `WeeklySummaryService` skips orgs whose effective plan < Standard
(no Groq call).

## Endpoints

- `GET /api/organizations/my-plan` → `MyPlanDto` (plan, effectivePlan, validUntil, notes, usage
  meters). Org-less SystemAdmin → 404.
- `PUT /api/admin/organizations/{id}/plan` (SystemAdmin) → `UpdateOrganizationPlanDto { plan,
  planValidUntil?, planNotes? }`; accepts all five plans incl. Partner.
- Admin org DTOs (`AdminOrganizationDto`) gained `plan`/`planName`/`planValidUntil`/`planNotes`.

## Alert (SPEC-04 rule table)

`PlanExpiring` (NotificationType **18**): in `AlertRuleService.RunDailyScanAsync`, org loop — an
org whose effective plan ≠ Free and `PlanValidUntil` is within 7 days notifies its OrganizationAdmins
(dedup 7 days, toggle `Alerts:PlanExpiring:Enabled`). Covers trial expiry too.

## Frontend

- `core/models`: `PlanType` enum + `PlanTypeLabels`, `MyPlan`/`PlanUsage`, `UpdateOrganizationPlanPayload`.
- `core/services/planService.ts`: `useMyPlan` (retry:false — org-less 404 is expected), `PLAN_PRICING`,
  `isFeatureLocked`, `UPGRADE_EMAIL`.
- **402 upsell**: a *new* response interceptor in `apiClient.ts` (added before the frozen 401 one —
  ignore.md allows adding interceptors) recognises 402 + `code:"plan-limit"`, dispatches a
  `window` `CustomEvent('plan-limit', { detail })`, and rejects with the Bosnian message.
  `UpsellModal` (mounted once in `App` inside the router) listens and shows the message + a
  "Pogledaj pakete" link to `/plans`.
- **`/plans`** (`features/plans/PlansPage.tsx`, all authenticated users): 4 public plan cards +
  usage meters (košnice / članovi / AI poruke X/10) + payment instructions (mailto; **svrha uplate
  = ID organizacije**). A Partner org sees only a "Partner paket — sve uključeno" card. Trial shows
  "Pro (probni period do {datum})".
- Proactive gating: `AdvisorPage` shows a lock hint (Free) or the remaining-quota counter (Standard).
- Admin: `OrganizationFormPage` gains a plan section (edit-only: select all 5 incl. Partner +
  validUntil + notes → `useUpdateOrganizationPlan`); the admin org table shows a `PlanBadge` column
  (marks expired plans red).
- Profile dropdown → "Paket i pretplata" link to `/plans`.

## Phase 2 (out of v1)

Paddle (merchant-of-record, BiH-capable) checkout on `/plans` → `POST /api/billing/webhook` sets
`Plan`/`PlanValidUntil`; monthly collection becomes viable then. Partner stays outside checkout.
The v1 seam is exactly those two fields. See ADR-028.

## Tests

`PlanHelperTests`, `PlanGuardTests` (config-driven limits, feature tiers, monthly quota + reset,
SystemAdmin bypass, GetMyPlan), `RegistrationTrialTests`, `WeeklySummaryPlanTests` (Free/expired
skip), `AdminPlanUpdateTests` (Partner accepted, lifetime expiry). Live-verified E2E: /plans meters,
402 upsell modal on the 2nd apiary of a Free org, admin plan change reflected in the org table.
