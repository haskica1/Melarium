import { useNavigate } from 'react-router-dom'
import { Combine } from 'lucide-react'
import { format } from 'date-fns'
import { CollapsibleSection } from '../../shared/components/CollapsibleSection'
import { useMergedBeehives } from '../../core/services/beehiveMergeQueries'

/**
 * The apiary's archive (SPEC-19 §7.3) — hives whose colony was merged into another one. This is the
 * only path to them in the UI; every other list filters them out. Collapsed by default and absent
 * entirely when empty, so an apiary that never merged anything looks exactly as it did before.
 */
export function MergedHivesSection({ apiaryId }: { apiaryId: number }) {
  const navigate = useNavigate()
  const { data: merged = [] } = useMergedBeehives(apiaryId)

  if (merged.length === 0) return null

  return (
    <CollapsibleSection
      title="Sastavljene košnice"
      icon={<Combine className="w-5 h-5 text-slate-500" />}
      count={merged.length}
      defaultOpen={false}
    >
      <div className="grid gap-3 sm:grid-cols-2">
        {merged.map(hive => (
          <div
            key={hive.id}
            className="card opacity-90 hover:shadow-honey hover:-translate-y-0.5 transition-all duration-200 cursor-pointer"
            onClick={() => navigate(`/beehives/${hive.id}`)}
          >
            <div className="flex items-start gap-3">
              <span className="text-2xl shrink-0 mt-0.5 grayscale">🏠</span>
              <div className="min-w-0">
                <p className="font-semibold text-gray-800 dark:text-slate-100 truncate">{hive.name}</p>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
                  {hive.mergedAt ? `${format(new Date(hive.mergedAt), 'dd.MM.yyyy.')} · ` : ''}
                  društvo pripojeno košnici{' '}
                  <span className="font-medium">{hive.mergedIntoBeehiveName ?? '—'}</span>
                </p>
              </div>
            </div>
          </div>
        ))}
      </div>
    </CollapsibleSection>
  )
}
