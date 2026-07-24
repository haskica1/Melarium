import { Link } from 'react-router-dom'
import { format } from 'date-fns'
import { Loader2 } from 'lucide-react'
import { CollapsibleSection } from '../../shared/components/CollapsibleSection'
import { useHarvests } from '../../core/services/harvestQueries'

const fmtKg = (kg: number) => `${kg.toFixed(1).replace(/\.0$/, '')} kg`

/** "Vrcanja" section for the apiary detail page — this apiary's harvests, newest first (SPEC-02). */
export function ApiaryHarvestsSection({ apiaryId }: { apiaryId: number }) {
  const { data: harvests = [], isLoading } = useHarvests({ apiaryId })

  const totalKg = harvests.reduce((s, h) => s + h.totalKg, 0)

  return (
    <CollapsibleSection
      title="Vrcanja"
      icon="🍯"
      count={harvests.length}
      defaultOpen={harvests.length > 0}
      action={
        <Link to="/harvests" className="inline-flex items-center gap-1 text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium">
          Sva vrcanja
        </Link>
      }
    >
      {isLoading ? (
        <div className="flex justify-center py-6"><Loader2 className="w-5 h-5 animate-spin text-honey-500" /></div>
      ) : harvests.length === 0 ? (
        <p className="text-center py-6 text-sm text-gray-400 dark:text-slate-500">Još nema evidencije vrcanja za ovaj pčelinjak.</p>
      ) : (
        <>
          <div className="flex items-center justify-between mb-3 text-sm">
            <span className="text-gray-500 dark:text-slate-400">Ukupno zabilježeno</span>
            <span className="font-semibold text-honey-700 dark:text-honey-300">{fmtKg(totalKg)}</span>
          </div>
          <div className="space-y-2">
            {harvests.slice(0, 10).map(h => (
              <div key={h.id} className="flex items-center gap-3 px-3 py-2 rounded-xl bg-gray-50 dark:bg-slate-800/60">
                <span className="text-sm font-medium text-gray-800 dark:text-slate-100 w-20 shrink-0">{fmtKg(h.totalKg)}</span>
                <span className="text-xs text-honey-700 dark:text-honey-300 bg-honey-100 dark:bg-honey-500/15 rounded-full px-2 py-0.5">{h.honeyTypeName}</span>
                <span className="ml-auto text-sm text-gray-500 dark:text-slate-400">{format(new Date(h.date), 'dd.MM.yyyy')}</span>
              </div>
            ))}
          </div>
        </>
      )}
    </CollapsibleSection>
  )
}
