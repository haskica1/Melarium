import clsx from 'clsx'
import { HiveProductType, HiveProductTypeLabels } from '../../core/models'
import { PRODUCT_ICON, fmtProductQty } from '../../shared/utils/hiveProductUnits'

/** One product type's quantity, in its own unit — "🟤 Propolis 350 g". `bare` drops the icon where the row already shows one. */
export function ProductChip({ productType, kg, small = false, bare = false }: {
  productType: HiveProductType
  kg: number
  small?: boolean
  bare?: boolean
}) {
  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1.5 rounded-full whitespace-nowrap bg-honey-100 text-honey-800 dark:bg-honey-500/15 dark:text-honey-300',
        small ? 'px-2 py-0.5 text-xs' : 'px-3 py-1 text-sm font-medium',
      )}
    >
      {!bare && <span aria-hidden>{PRODUCT_ICON[productType]}</span>}
      {HiveProductTypeLabels[productType]} {fmtProductQty(kg, productType)}
    </span>
  )
}

/** A row of per-type chips. Never a single "total kg" — the types do not add up (SPEC-30). */
export function ProductTotalsChips({ totals, small = false }: {
  totals: Array<{ productType: HiveProductType; kg: number }>
  small?: boolean
}) {
  if (totals.length === 0) return null
  return (
    <div className="flex flex-wrap gap-1.5">
      {totals.map(t => <ProductChip key={t.productType} productType={t.productType} kg={t.kg} small={small} />)}
    </div>
  )
}
