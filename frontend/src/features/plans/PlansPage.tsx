import { Check, Crown, Loader2, Lock, Mail, Minus } from 'lucide-react'
import { useMyPlan, PLAN_PRICING, UPGRADE_EMAIL } from '../../core/services/planService'
import { useAuth } from '../../core/context/AuthContext'
import { PlanType, PlanTypeLabels, type MyPlan } from '../../core/models'

const PUBLIC_PLANS: PlanType[] = [PlanType.Free, PlanType.Standard, PlanType.Pro, PlanType.Max]

interface FeatureRow {
  label: string
  values: Record<PlanType, string | boolean>
}

// Mirrors the SPEC-09 tier table. `true`/`false` render as a check/dash; strings render as text.
const FEATURES: FeatureRow[] = [
  { label: 'Pčelinjaci',              values: { [PlanType.Free]: '1', [PlanType.Standard]: 'neograničeno', [PlanType.Pro]: 'neograničeno', [PlanType.Max]: 'neograničeno', [PlanType.Partner]: 'neograničeno' } },
  { label: 'Košnice',                 values: { [PlanType.Free]: '7', [PlanType.Standard]: '30', [PlanType.Pro]: '100', [PlanType.Max]: 'neograničeno', [PlanType.Partner]: 'neograničeno' } },
  { label: 'Dodatni članovi',         values: { [PlanType.Free]: '0', [PlanType.Standard]: '2', [PlanType.Pro]: '5', [PlanType.Max]: 'neograničeno', [PlanType.Partner]: 'neograničeno' } },
  { label: 'Evidencije + PDF registri', values: { [PlanType.Free]: true, [PlanType.Standard]: true, [PlanType.Pro]: true, [PlanType.Max]: true, [PlanType.Partner]: true } },
  { label: 'Kalendar, prognoza, statistika', values: { [PlanType.Free]: true, [PlanType.Standard]: true, [PlanType.Pro]: true, [PlanType.Max]: true, [PlanType.Partner]: true } },
  { label: 'Alarmi i edukacija',      values: { [PlanType.Free]: true, [PlanType.Standard]: true, [PlanType.Pro]: true, [PlanType.Max]: true, [PlanType.Partner]: true } },
  { label: 'Pašnjaci i selidbe',      values: { [PlanType.Free]: false, [PlanType.Standard]: true, [PlanType.Pro]: true, [PlanType.Max]: true, [PlanType.Partner]: true } },
  { label: 'Glasovni unos pregleda',  values: { [PlanType.Free]: false, [PlanType.Standard]: true, [PlanType.Pro]: true, [PlanType.Max]: true, [PlanType.Partner]: true } },
  { label: 'Sedmični AI sažetak',     values: { [PlanType.Free]: false, [PlanType.Standard]: true, [PlanType.Pro]: true, [PlanType.Max]: true, [PlanType.Partner]: true } },
  { label: 'AI savjetnik',            values: { [PlanType.Free]: false, [PlanType.Standard]: '10 poruka/mj', [PlanType.Pro]: 'neograničeno', [PlanType.Max]: 'neograničeno', [PlanType.Partner]: 'neograničeno' } },
  { label: 'Foto + AI analiza okvira', values: { [PlanType.Free]: false, [PlanType.Standard]: false, [PlanType.Pro]: true, [PlanType.Max]: true, [PlanType.Partner]: true } },
  { label: 'Prioritetna podrška',     values: { [PlanType.Free]: false, [PlanType.Standard]: false, [PlanType.Pro]: false, [PlanType.Max]: true, [PlanType.Partner]: true } },
]

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('bs-BA', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

export default function PlansPage() {
  const { data: plan, isLoading, isError } = useMyPlan()
  const { user } = useAuth()

  if (isLoading) {
    return (
      <div className="flex justify-center py-20">
        <Loader2 className="w-6 h-6 animate-spin text-honey-500" />
      </div>
    )
  }

  // The org-less SystemAdmin has no plan of its own.
  if (isError || !plan) {
    return (
      <div className="max-w-2xl mx-auto animate-fade-in">
        <h1 className="font-display text-2xl font-bold text-gray-800 dark:text-slate-100 mb-2">Paketi</h1>
        <p className="text-gray-500 dark:text-slate-400">Vaš nalog nije vezan za organizaciju s pretplatom.</p>
      </div>
    )
  }

  const isTrial = plan.planNotes === 'Probni period'
  const isPartner = plan.plan === PlanType.Partner

  return (
    <div className="max-w-5xl mx-auto animate-fade-in space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold text-gray-800 dark:text-slate-100">Paketi i pretplata</h1>
        <p className="text-sm text-gray-500 dark:text-slate-400 mt-1">
          Trenutni paket:{' '}
          <span className="font-semibold text-honey-700 dark:text-honey-400">
            {isTrial ? `Pro (probni period do ${plan.planValidUntil ? formatDate(plan.planValidUntil) : ''})` : plan.planName}
          </span>
          {!isTrial && plan.planValidUntil && (
            <span className="text-gray-400 dark:text-slate-500"> — važi do {formatDate(plan.planValidUntil)}</span>
          )}
        </p>
      </div>

      <LockSummary plan={plan} />

      <UsageMeters plan={plan} />

      {isPartner ? (
        <PartnerCard />
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {PUBLIC_PLANS.map(p => (
              <PlanCard key={p} planType={p} current={plan.effectivePlan === p} />
            ))}
          </div>
          <PaymentInstructions orgId={user?.organizationId} orgName={user?.organizationName} />
        </>
      )}
    </div>
  )
}

/**
 * What the current plan no longer reaches (SPEC-24). This is the one page that has to say it in
 * full: the padlocks on the lists say *that* something is locked, this says how much and why, and
 * makes the point that nothing was deleted — the single most common fear after a plan expires.
 * Renders nothing for an organization that fits inside its plan, which is most of them.
 */
function LockSummary({ plan }: { plan: MyPlan }) {
  const u = plan.usage
  const nothingLocked = u.lockedApiaries === 0 && u.lockedBeehives === 0 && u.readOnlyMembers === 0
  if (nothingLocked) return null

  const parts: string[] = []
  if (u.lockedApiaries > 0) parts.push(`${u.lockedApiaries} ${u.lockedApiaries === 1 ? 'pčelinjak' : 'pčelinjaka'}`)
  if (u.lockedBeehives > 0) parts.push(`${u.lockedBeehives} ${u.lockedBeehives === 1 ? 'košnica' : 'košnica'}`)
  if (u.readOnlyMembers > 0) parts.push(`${u.readOnlyMembers} ${u.readOnlyMembers === 1 ? 'član' : 'članova'} bez prava unosa`)

  return (
    <div className="flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-500/30 dark:bg-amber-500/10">
      <Lock className="mt-0.5 h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400" />
      <div className="text-sm text-amber-900 dark:text-amber-200">
        <p className="font-medium">
          {plan.effectivePlanName} paket ne pokriva sve vaše podatke — {parts.join(', ')}{' '}
          {parts.length === 1 ? 'je zaključan' : 'su zaključani'}.
        </p>
        <p className="mt-0.5 text-amber-800/80 dark:text-amber-200/70">
          Ništa nije obrisano. Sve se otključava čim nadogradite paket, a možete i obrisati košnice
          koje više ne koristite da se ostale otključaju.
        </p>
      </div>
    </div>
  )
}

function UsageMeters({ plan }: { plan: MyPlan }) {
  const u = plan.usage
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <Meter label="Košnice" used={u.beehives} limit={u.beehivesLimit} />
      <Meter label="Dodatni članovi" used={u.members} limit={u.membersLimit} />
      {u.aiInteractionsLimit != null && u.aiInteractionsLimit > 0 ? (
        <Meter label="AI poruke ovog mjeseca" used={u.aiInteractionsThisMonth} limit={u.aiInteractionsLimit} />
      ) : (
        <div className="card flex flex-col justify-center">
          <p className="text-xs text-gray-500 dark:text-slate-400">AI asistent</p>
          <p className="text-sm font-semibold text-gray-800 dark:text-slate-100 mt-1">
            {plan.effectivePlan >= PlanType.Pro ? 'Neograničeno' : plan.effectivePlan === PlanType.Standard ? '30 poruka/mj' : 'Nedostupno'}
          </p>
        </div>
      )}
    </div>
  )
}

function Meter({ label, used, limit }: { label: string; used: number; limit?: number | null }) {
  const unlimited = limit == null
  const pct = unlimited ? 0 : Math.min(100, Math.round((used / Math.max(1, limit!)) * 100))
  const atLimit = !unlimited && used >= limit!
  return (
    <div className="card">
      <p className="text-xs text-gray-500 dark:text-slate-400">{label}</p>
      <p className="text-sm font-semibold text-gray-800 dark:text-slate-100 mt-1">
        {used}{unlimited ? '' : ` / ${limit}`} {unlimited && <span className="text-gray-400 dark:text-slate-500 font-normal">(neograničeno)</span>}
      </p>
      {!unlimited && (
        <div className="mt-2 h-1.5 rounded-full bg-gray-100 dark:bg-slate-800 overflow-hidden">
          <div className={`h-full rounded-full ${atLimit ? 'bg-red-500' : 'bg-honey-500'}`} style={{ width: `${pct}%` }} />
        </div>
      )}
    </div>
  )
}

function PlanCard({ planType, current }: { planType: PlanType; current: boolean }) {
  const pricing = PLAN_PRICING[planType]
  return (
    <div className={`card flex flex-col ${current ? 'ring-2 ring-honey-400 dark:ring-honey-500' : ''}`}>
      <div className="flex items-center justify-between">
        <h3 className="font-display text-lg font-bold text-gray-800 dark:text-slate-100">{PlanTypeLabels[planType]}</h3>
        {current && (
          <span className="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-honey-100 text-honey-700 dark:bg-honey-500/20 dark:text-honey-300">
            Vaš paket
          </span>
        )}
      </div>

      <div className="mt-2 mb-4">
        {pricing.monthly === 0 ? (
          <p className="text-2xl font-bold text-gray-800 dark:text-slate-100">Besplatno</p>
        ) : (
          <>
            <p className="text-2xl font-bold text-gray-800 dark:text-slate-100">
              {pricing.monthly} KM<span className="text-sm font-normal text-gray-400 dark:text-slate-500">/mj</span>
            </p>
            <p className="text-xs text-gray-400 dark:text-slate-500">godišnja uplata {pricing.yearly} KM</p>
          </>
        )}
      </div>

      <ul className="space-y-2 text-sm flex-1">
        {FEATURES.map(f => {
          const v = f.values[planType]
          if (v === false) {
            return (
              <li key={f.label} className="flex items-start gap-2 text-gray-300 dark:text-slate-600">
                <Minus className="w-4 h-4 shrink-0 mt-0.5" />
                <span className="line-through decoration-gray-200 dark:decoration-slate-700">{f.label}</span>
              </li>
            )
          }
          return (
            <li key={f.label} className="flex items-start gap-2 text-gray-600 dark:text-slate-300">
              <Check className="w-4 h-4 shrink-0 mt-0.5 text-emerald-500" />
              <span>{f.label}{typeof v === 'string' && v !== 'neograničeno' ? <span className="text-gray-400 dark:text-slate-500"> — {v}</span> : ''}</span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}

function PartnerCard() {
  return (
    <div className="card ring-2 ring-honey-400 dark:ring-honey-500">
      <div className="flex items-center gap-2 mb-2">
        <Crown className="w-5 h-5 text-honey-500" />
        <h3 className="font-display text-lg font-bold text-gray-800 dark:text-slate-100">Partner paket — sve uključeno</h3>
      </div>
      <p className="text-sm text-gray-600 dark:text-slate-300">
        Vaša organizacija koristi Partner paket: sve funkcije su otključane bez ograničenja. Hvala što ste dio Melarium zajednice! 🐝
      </p>
    </div>
  )
}

function PaymentInstructions({ orgId, orgName }: { orgId?: number | null; orgName?: string | null }) {
  const subject = encodeURIComponent(`Nadogradnja paketa — ${orgName ?? 'organizacija'} (ID ${orgId ?? '?'})`)
  const body = encodeURIComponent(
    `Poštovani,\n\nželim nadograditi paket za organizaciju "${orgName ?? ''}" (ID: ${orgId ?? ''}).\n\nŽeljeni paket: \nBroj košnica: \n\nHvala.`,
  )
  return (
    <div className="card">
      <h3 className="font-display text-base font-semibold text-gray-800 dark:text-slate-100 mb-2">Kako nadograditi</h3>
      <p className="text-sm text-gray-600 dark:text-slate-300 leading-relaxed">
        Naplata je trenutno ručna i godišnja. Za nadogradnju nas kontaktirajte, a nakon uplate aktiviramo paket u roku od jednog radnog dana.
        <br />
        <span className="text-gray-500 dark:text-slate-400">
          Prilikom uplate <strong>u svrhu uplate obavezno navedite ID organizacije: {orgId ?? '—'}</strong> radi bržeg uparivanja.
        </span>
      </p>
      <a
        href={`mailto:${UPGRADE_EMAIL}?subject=${subject}&body=${body}`}
        className="mt-4 inline-flex items-center gap-2 px-4 py-2.5 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
      >
        <Mail className="w-4 h-4" />
        Kontaktirajte nas za nadogradnju
      </a>
    </div>
  )
}
