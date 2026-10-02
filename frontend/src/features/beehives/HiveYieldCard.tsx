import { Link } from 'react-router-dom'
import { ArrowRight, Droplets } from 'lucide-react'
import { useHiveHarvestSummary } from '../../core/services/harvestQueries'
import { HiveProductType, HiveProductTypeLabels } from '../../core/models'
import { fmtProductQty } from '../../shared/utils/hiveProductUnits'
import { ProductTotalsChips } from '../harvests/ProductChips'

/**
 * Yield card for the beehive detail sidebar (SPEC-02, every product since SPEC-30). Only this hive's
 * own lines count — a record kept as one figure for the apiary or organization cannot be pinned on a hive.
 */
export function HiveYieldCard({ beehiveId }: { beehiveId: number }) {
  const { data, isLoading } = useHiveHarvestSummary(beehiveId)

  if (isLoading || !data) return null

  const currentYear = new Date().getFullYear()
  const current = data.byYear.find(y => y.year === currentYear)
  const prior = data.byYear.filter(y => y.year !== currentYear)
  const honeyNow = current?.items.find(i => i.productType === HiveProductType.Honey)?.kg ?? 0
  const othersNow = current?.items.filter(i => i.productType !== HiveProductType.Honey) ?? []

  return (
    <div className="card">
      <div className="flex items-center gap-2 mb-4">
        <Droplets className="w-5 h-5 text-honey-500" />
        <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Prinos</h2>
      </div>

      <div className="flex items-baseline gap-2">
        <span className="text-3xl font-bold text-honey-700 dark:text-honey-300">{fmtProductQty(honeyNow, HiveProductType.Honey)}</span>
        <span className="text-sm text-gray-500 dark:text-slate-400">meda, sezona {currentYear}.</span>
      </div>

      {othersNow.length > 0 && (
        <div className="mt-3">
          <ProductTotalsChips totals={othersNow} small />
        </div>
      )}

      {prior.length > 0 && (
        <div className="mt-4 pt-3 border-t border-gray-100 dark:border-slate-800 space-y-1.5">
          {prior.map(y => (
            <div key={y.year} className="flex items-start justify-between gap-3 text-sm">
              <span className="text-gray-500 dark:text-slate-400 shrink-0">{y.year}.</span>
              <span className="text-right font-medium text-gray-700 dark:text-slate-200">
                {y.items.map(i => `${HiveProductTypeLabels[i.productType]} ${fmtProductQty(i.kg, i.productType)}`).join(' · ')}
              </span>
            </div>
          ))}
        </div>
      )}

      {!current && prior.length === 0 && (
        <p className="mt-2 text-sm text-gray-400 dark:text-slate-500">Još nema zabilježenog prinosa za ovu košnicu.</p>
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
