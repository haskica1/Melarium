import { Link } from 'react-router-dom'
import { format } from 'date-fns'
import { Leaf, Plus } from 'lucide-react'
import { useDiets } from '../../core/services/dietQueries'
import { DietStatus, FoodType } from '../../core/models'
import type { Diet } from '../../core/models'
import { ErrorMessage } from '../../shared/components'
import { usePermissions } from '../../core/hooks/usePermissions'
import { amountLabel } from './FeedingsPage'

/**
 * Compact feeding-status card for the beehive detail page (SPEC-12), aligned with HiveTreatmentCard.
 *
 * Fed by `GET /feedings?beehiveId=` rather than `/feedings/active`: that endpoint answers for a whole
 * apiary in one request and belongs to the badge/chip work, while this card needs one hive's
 * programmes plus a history count.
 */
export function HiveFeedingCard({ beehiveId }: { beehiveId: number }) {
  const { data: diets = [], isLoading, isError } = useDiets({ beehiveId })
  // Narrowed by SPEC-12: a programme is chosen across an apiary's hives, which a Beekeeper cannot see.
  const { canManageDiets } = usePermissions()

  if (isLoading) return null

  const active = diets.filter(
    d => d.status === DietStatus.InProgress || d.status === DietStatus.NotStarted,
  )

  return (
    <div className="card">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Leaf className="w-5 h-5 text-honey-500" />
          <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Prehrana</h2>
        </div>
        <div className="flex items-center gap-3">
          {diets.length > 0 && (
            <Link
              to={`/feedings?beehiveId=${beehiveId}`}
              className="text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium"
            >
              Historija ({diets.length})
            </Link>
          )}
          {/* Kept on the card: otherwise the only way to start feeding one hive is to go to the
              apiary and untick everything else. */}
          {canManageDiets && (
            <Link
              to={`/feedings/new?beehiveId=${beehiveId}`}
              className="text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium inline-flex items-center gap-1"
            >
              <Plus className="w-3.5 h-3.5" /> Dodaj
            </Link>
          )}
        </div>
      </div>

      {/* A failed request must never read as "this hive isn't being fed". */}
      {isError ? (
        <ErrorMessage message="Greška pri učitavanju prehrane." />
      ) : active.length === 0 ? (
        <p className="text-sm text-gray-400 dark:text-slate-500">Ova košnica trenutno nije na prehrani.</p>
      ) : (
        <div className="space-y-3">
          {active.map(d => <ActiveProgramme key={d.id} diet={d} />)}
        </div>
      )}
    </div>
  )
}

function ActiveProgramme({ diet: d }: { diet: Diet }) {
  const food = d.foodType === FoodType.Custom && d.customFoodType ? d.customFoodType : d.foodTypeName
  const amount = amountLabel(d)
  const progressPct = d.totalEntries > 0 ? Math.round((d.completedEntries / d.totalEntries) * 100) : 0

  return (
    <Link to={`/feedings/${d.id}`} className="block group">
      <div className="flex items-center gap-2 flex-wrap">
        <span className="font-semibold text-gray-900 dark:text-slate-100 group-hover:underline">{d.name}</span>
        <span className="text-xs rounded-full px-2 py-0.5 bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300">
          {d.statusName}
        </span>
      </div>
      <p className="text-sm text-gray-500 dark:text-slate-400 mt-0.5">
        {food}{amount ? `, ${amount}` : ''}
        {d.nextFeedingDate && ` · sljedeće ${format(new Date(d.nextFeedingDate), 'dd.MM.yyyy')}`}
      </p>
      <div className="flex items-center gap-2 mt-1.5">
        <div className="flex-1 h-1.5 bg-gray-100 dark:bg-slate-700 rounded-full overflow-hidden">
          <div className="h-full rounded-full bg-honey-400 transition-all" style={{ width: `${progressPct}%` }} />
        </div>
        <span className="text-xs text-gray-400 dark:text-slate-500 shrink-0">
          {d.completedEntries}/{d.totalEntries} rundi
        </span>
      </div>
    </Link>
  )
}
