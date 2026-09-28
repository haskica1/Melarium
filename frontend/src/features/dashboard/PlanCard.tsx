import { BadgeCheck } from 'lucide-react'
import { PlanType } from '../../core/models'
import { useMyPlan } from '../../core/services/planService'
import ProgressRing from '../../shared/components/ProgressRing'
import { Skeleton } from '../../shared/components'
import DashboardCard from './DashboardCard'
import { dayMonth } from './format'

/** The organization's plan and how much of it is used — owner only (SPEC-29). */
export default function PlanCard() {
  const { data: plan, isPending, isError } = useMyPlan()

  return (
    <DashboardCard icon={<BadgeCheck className="w-4 h-4" />} title="Paket" action={{ to: '/plans', label: 'Paketi' }}>
      {isPending ? (
        <Skeleton className="h-24 rounded-xl" />
      ) : isError || !plan ? (
        <p className="text-sm text-gray-500 dark:text-slate-400">Podaci o paketu trenutno nisu dostupni.</p>
      ) : (
        <>
          <p className="text-sm text-gray-700 dark:text-slate-300 mb-4">
            <span className="font-semibold text-gray-900 dark:text-slate-100">{plan.effectivePlanName}</span>
            {plan.planValidUntil
              ? <> · važi do {dayMonth(plan.planValidUntil.slice(0, 10))}{plan.planValidUntil.slice(0, 4)}.</>
              : plan.effectivePlan !== PlanType.Free && <> · bez isteka</>}
          </p>
          <div className="flex flex-wrap gap-5">
            <Usage label="košnice" used={plan.usage.beehives} limit={plan.usage.beehivesLimit} />
            <Usage label="pčelinjaci" used={plan.usage.apiaries} limit={plan.usage.apiariesLimit} />
            {/* Same wording as /plans: the limit counts members beyond the owner. */}
            <Usage label="dodatni članovi" used={plan.usage.members} limit={plan.usage.membersLimit} />
          </div>
        </>
      )}
    </DashboardCard>
  )
}

function Usage({ label, used, limit }: { label: string; used: number; limit?: number | null }) {
  const unlimited = limit == null
  // Over the limit is the SPEC-24 lock (red); exactly at it only means nothing more can be added.
  const stroke = unlimited || used < limit ? 'stroke-honey-500' : used > limit ? 'stroke-red-500' : 'stroke-orange-500'
  return (
    <div className="flex flex-col items-center gap-1">
      <ProgressRing
        segments={[{ value: unlimited ? 0 : used, strokeClassName: stroke }]}
        total={unlimited ? 1 : Math.max(limit, 1)}
        center={unlimited ? `${used}` : `${used}/${limit}`}
        sub={unlimited ? 'bez limita' : undefined}
        size={76}
        thickness={8}
        label={unlimited ? `${label}: ${used}, bez limita` : `${label}: ${used} od ${limit}`}
      />
      <span className="text-xs text-gray-600 dark:text-slate-400">{label}</span>
    </div>
  )
}
