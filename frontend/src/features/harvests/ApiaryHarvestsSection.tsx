import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { format } from 'date-fns'
import { Loader2 } from 'lucide-react'
import { CollapsibleSection } from '../../shared/components/CollapsibleSection'
import { ErrorMessage } from '../../shared/components'
import { useHarvests } from '../../core/services/harvestQueries'
import { sumByType } from '../../shared/utils/hiveProductUnits'
import { ProductChip, ProductTotalsChips } from './ProductChips'

/**
 * "Prinosi" on the apiary detail page — this apiary's honey and other products, newest first
 * (SPEC-02, SPEC-30). Totals are per product, never one kg figure; records of the whole
 * organization belong to no apiary and are not here.
 */
export function ApiaryHarvestsSection({ apiaryId }: { apiaryId: number }) {
  const { data: harvests = [], isLoading, isError } = useHarvests({ apiaryId, allProducts: true })
  const totals = useMemo(() => sumByType(harvests), [harvests])

  return (
    <CollapsibleSection
      title="Prinosi"
      icon="🍯"
      count={harvests.length}
      defaultOpen={harvests.length > 0}
      action={
        <Link to="/harvests" className="inline-flex items-center gap-1 text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium">
          Svi prinosi
        </Link>
      }
    >
      {isLoading ? (
        <div className="flex justify-center py-6"><Loader2 className="w-5 h-5 animate-spin text-honey-500" /></div>
      ) : isError ? (
        <ErrorMessage message="Greška pri učitavanju prinosa za ovaj pčelinjak." />
      ) : harvests.length === 0 ? (
        <p className="text-center py-6 text-sm text-gray-400 dark:text-slate-500">Još nema evidencije prinosa za ovaj pčelinjak.</p>
      ) : (
        <>
          <div className="mb-3">
            <ProductTotalsChips totals={totals} small />
          </div>
          <div className="space-y-2">
            {harvests.slice(0, 10).map(h => (
              <div key={h.id} className="flex items-center gap-2 px-3 py-2 rounded-xl bg-gray-50 dark:bg-slate-800/60">
                <ProductChip productType={h.productType} kg={h.totalKg} small />
                {h.honeyTypeName && (
                  <span className="hidden sm:inline text-xs text-honey-700 dark:text-honey-300">{h.honeyTypeName}</span>
                )}
                <span className="ml-auto text-sm text-gray-500 dark:text-slate-400 whitespace-nowrap">{format(new Date(h.date), 'dd.MM.yyyy')}</span>
              </div>
            ))}
          </div>
        </>
      )}
    </CollapsibleSection>
  )
}
