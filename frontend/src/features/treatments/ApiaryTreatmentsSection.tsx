import { Link } from 'react-router-dom'
import { format } from 'date-fns'
import { Loader2 } from 'lucide-react'
import { CollapsibleSection } from '../../shared/components/CollapsibleSection'
import { useTreatments } from '../../core/services/treatmentQueries'
import { TreatmentStatus } from '../../core/models'

const STATUS_STYLE: Record<TreatmentStatus, string> = {
  [TreatmentStatus.InProgress]: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  [TreatmentStatus.Karenca]:    'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300',
  [TreatmentStatus.Completed]:  'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
}

/** "Tretmani" section for the apiary detail page — this apiary's treatments, newest first (SPEC-08). */
export function ApiaryTreatmentsSection({ apiaryId }: { apiaryId: number }) {
  const { data: treatments = [], isLoading } = useTreatments({ apiaryId })

  const sorted = [...treatments].sort((a, b) => b.startDate.localeCompare(a.startDate))

  return (
    <CollapsibleSection
      title="Tretmani"
      icon="💊"
      count={treatments.length}
      defaultOpen={treatments.some(t => t.status !== TreatmentStatus.Completed)}
      action={
        <Link to="/treatments" className="inline-flex items-center gap-1 text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium">
          Svi tretmani
        </Link>
      }
    >
      {isLoading ? (
        <div className="flex justify-center py-6"><Loader2 className="w-5 h-5 animate-spin text-honey-500" /></div>
      ) : sorted.length === 0 ? (
        <p className="text-center py-6 text-sm text-gray-400 dark:text-slate-500">Još nema evidencije tretmana za ovaj pčelinjak.</p>
      ) : (
        <div className="space-y-2">
          {sorted.slice(0, 10).map(t => {
            const statusText = t.status === TreatmentStatus.Karenca
              ? `Karenca do ${format(new Date(t.karencaUntil), 'dd.MM.yyyy')}`
              : t.statusName
            return (
              <div key={t.id} className="flex items-center gap-3 px-3 py-2 rounded-xl bg-gray-50 dark:bg-slate-800/60">
                <span className="text-sm text-gray-500 dark:text-slate-400 w-20 shrink-0">{format(new Date(t.startDate), 'dd.MM.yyyy')}</span>
                <span className="text-sm font-medium text-gray-800 dark:text-slate-100 truncate">{t.productName}</span>
                <span className={`text-xs rounded-full px-2 py-0.5 shrink-0 ${STATUS_STYLE[t.status]}`}>{statusText}</span>
                <span className="ml-auto text-xs text-gray-400 dark:text-slate-500 shrink-0">{t.hiveCount} košn.</span>
              </div>
            )
          })}
        </div>
      )}
    </CollapsibleSection>
  )
}
