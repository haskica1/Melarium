import { Link } from 'react-router-dom'
import { format } from 'date-fns'
import { Loader2 } from 'lucide-react'
import { CollapsibleSection } from '../../shared/components/CollapsibleSection'
import { ErrorMessage } from '../../shared/components'
import { useDiets } from '../../core/services/dietQueries'
import { DietStatus } from '../../core/models'

const STATUS_STYLE: Record<DietStatus, string> = {
  [DietStatus.NotStarted]:   'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
  [DietStatus.InProgress]:   'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  [DietStatus.Completed]:    'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  [DietStatus.StoppedEarly]: 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
}

const isLive = (s: DietStatus) => s === DietStatus.NotStarted || s === DietStatus.InProgress

/** "Prehrana" section for the apiary detail page — this apiary's programmes, newest first (SPEC-12). */
export function ApiaryFeedingsSection({ apiaryId }: { apiaryId: number }) {
  const { data: diets = [], isLoading, isError } = useDiets({ apiaryId })

  const sorted = [...diets].sort((a, b) => b.startDate.localeCompare(a.startDate))

  return (
    <CollapsibleSection
      title="Prehrana"
      icon="🍯"
      count={diets.length}
      defaultOpen={diets.some(d => isLive(d.status))}
      action={
        <Link to="/feedings" className="inline-flex items-center gap-1 text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium">
          Sva prehrana
        </Link>
      }
    >
      {isLoading ? (
        <div className="flex justify-center py-6"><Loader2 className="w-5 h-5 animate-spin text-honey-500" /></div>
      ) : isError ? (
        <ErrorMessage message="Greška pri učitavanju prehrane za ovaj pčelinjak." />
      ) : sorted.length === 0 ? (
        <p className="text-center py-6 text-sm text-gray-400 dark:text-slate-500">Još nema programa prehrane za ovaj pčelinjak.</p>
      ) : (
        <div className="space-y-2">
          {sorted.slice(0, 10).map(d => (
            <Link
              key={d.id}
              to={`/feedings/${d.id}`}
              className="flex items-center gap-3 px-3 py-2 rounded-xl bg-gray-50 dark:bg-slate-800/60 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
            >
              <span className="text-sm text-gray-500 dark:text-slate-400 w-20 shrink-0">{format(new Date(d.startDate), 'dd.MM.yyyy')}</span>
              <span className="text-sm font-medium text-gray-800 dark:text-slate-100 truncate">{d.name}</span>
              <span className={`text-xs rounded-full px-2 py-0.5 shrink-0 ${STATUS_STYLE[d.status]}`}>{d.statusName}</span>
              <span className="ml-auto text-xs text-gray-400 dark:text-slate-500 shrink-0 whitespace-nowrap">
                {d.completedEntries}/{d.totalEntries} · {d.hiveCount} košn.
              </span>
            </Link>
          ))}
        </div>
      )}
    </CollapsibleSection>
  )
}
