import { Link } from 'react-router-dom'
import { ArrowRight, Droplets } from 'lucide-react'
import { useHiveYield } from '../../core/services/harvestQueries'

const fmtKg = (kg: number) => `${kg.toFixed(1).replace(/\.0$/, '')} kg`

/** Compact honey-yield card for the beehive detail sidebar (SPEC-02). */
export function HiveYieldCard({ beehiveId }: { beehiveId: number }) {
  const { data, isLoading } = useHiveYield(beehiveId)

  if (isLoading || !data) return null

  const currentYear = new Date().getFullYear()
  const prior = data.byYear.filter(y => y.year !== currentYear && y.kg > 0)

  return (
    <div className="card">
      <div className="flex items-center gap-2 mb-4">
        <Droplets className="w-5 h-5 text-honey-500" />
        <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Prinos</h2>
      </div>

      <div className="flex items-baseline gap-2">
        <span className="text-3xl font-bold text-honey-700 dark:text-honey-300">{fmtKg(data.currentSeasonKg)}</span>
        <span className="text-sm text-gray-500 dark:text-slate-400">sezona {currentYear}.</span>
      </div>

      {prior.length > 0 && (
        <div className="mt-4 pt-3 border-t border-gray-100 dark:border-slate-800 space-y-1.5">
          {prior.map(y => (
            <div key={y.year} className="flex items-center justify-between text-sm">
              <span className="text-gray-500 dark:text-slate-400">{y.year}.</span>
              <span className="font-medium text-gray-700 dark:text-slate-200">{fmtKg(y.kg)}</span>
            </div>
          ))}
        </div>
      )}

      {data.currentSeasonKg === 0 && prior.length === 0 && (
        <p className="mt-2 text-sm text-gray-400 dark:text-slate-500">Još nema zabilježenog vrcanja za ovu košnicu.</p>
      )}

      <Link
        to={`/harvests?beehiveId=${beehiveId}`}
        className="mt-4 pt-3 border-t border-gray-100 dark:border-slate-800 flex items-center gap-1 text-sm font-medium text-honey-600 dark:text-honey-400 hover:underline"
      >
        Svi prinosi ove košnice <ArrowRight className="w-3.5 h-3.5" />
      </Link>
    </div>
  )
}
