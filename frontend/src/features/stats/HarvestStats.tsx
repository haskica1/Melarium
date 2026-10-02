import { useState } from 'react'
import { HiveProductTypeLabels } from '../../core/models'
import type { NamedProductTotals, ProductTypeTotal, StatsData } from '../../core/services/statsService'
import { PRODUCT_ICON, fmtProductQty, productColumns, productKgIn, type ProductRow } from '../../shared/utils/hiveProductUnits'

const fmtKm = (n: number) => n.toLocaleString('bs-BA', { maximumFractionDigits: 0 })

// Building blocks of the "Prinosi" section on the stats page (SPEC-30), current year like the honey
// charts it frames.
//
// Deliberately no chart across products: a bar or pie over kg would put 200 g of royal jelly next to
// 20 kg of wax on one axis, and the jelly — often the more valuable of the two — would simply vanish.
// Each product keeps its own unit; revenue (KM) is the only figure that compares across them.

/** A heading inside the section — "Med", "Ostali proizvodi". */
export function SubHeading({ children }: { children: React.ReactNode }) {
  return (
    <h3 className="mt-6 mb-4 pt-5 border-t border-gray-100 dark:border-slate-800 font-display text-base font-semibold text-gray-800 dark:text-slate-100">
      {children}
    </h3>
  )
}

/** Every product side by side, honey first: a tile each, the revenue, and what has no price. */
export function HarvestOverview({ byProduct }: { byProduct: ProductTypeTotal[] }) {
  if (byProduct.length === 0) return null

  const revenue = byProduct.reduce((sum, t) => sum + t.estimatedRevenue, 0)
  const unpriced = byProduct.filter(t => t.unpricedKg > 0)

  return (
    <>
      <div className="flex flex-wrap gap-3 mb-5">
        {revenue > 0 && (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-xl text-sm font-medium bg-emerald-100 text-emerald-800 dark:bg-emerald-500/15 dark:text-emerald-300">
            💰 Procij. prihod: {fmtKm(revenue)} KM
          </div>
        )}
        {/* Travels with the revenue so a figure built from priced records only is not read as all of it. */}
        {unpriced.length > 0 && (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-xl text-sm bg-amber-50 text-amber-800 dark:bg-amber-500/10 dark:text-amber-300">
            Bez cijene: {unpriced.map(t => `${t.name} ${fmtProductQty(t.unpricedKg, t.productType)}`).join(', ')}
          </div>
        )}
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
        {byProduct.map(t => (
          <div key={t.productType} className="rounded-xl border border-honey-100 dark:border-slate-800 bg-honey-50/50 dark:bg-slate-800/40 p-3 min-w-0">
            <p className="text-sm text-gray-500 dark:text-slate-400 truncate">
              <span aria-hidden>{PRODUCT_ICON[t.productType]}</span> {t.name}
            </p>
            <p className="mt-1 text-xl font-semibold text-gray-900 dark:text-slate-100 tabular-nums">
              {fmtProductQty(t.kg, t.productType)}
            </p>
            <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
              {t.estimatedRevenue > 0 ? `≈ ${fmtKm(t.estimatedRevenue)} KM` : 'bez cijene'}
            </p>
          </div>
        ))}
      </div>
    </>
  )
}

/** The other products per apiary, per pasture and per hive — a column per product, never a total. */
export function ProductBreakdowns({ stats }: { stats: StatsData }) {
  const byApiary: ProductRow[] = stats.hiveProductsByApiary.map(a => ({ name: a.apiaryName, items: a.items }))
  const byPasture: NamedProductTotals[] = stats.hiveProductsByPasture
  const byBeehive: NamedProductTotals[] = stats.hiveProductsByBeehive
  if (byApiary.length === 0 && byPasture.length === 0 && byBeehive.length === 0) return null

  return (
    <>
      <SubHeading>Ostali proizvodi</SubHeading>
      <div className="space-y-6">
        {byApiary.length > 0 && <ProductMatrix caption="Po pčelinjaku" label="Pčelinjak" rows={byApiary} />}
        {byPasture.length > 0 && <ProductMatrix caption="Po pašnjaku" label="Pašnjak" rows={byPasture} />}
        {/* Can run to dozens of hives — the first ten, the rest on request. */}
        {byBeehive.length > 0 && <ProductMatrix caption="Po košnici" label="Košnica" rows={byBeehive} limit={10} />}
      </div>
    </>
  )
}

function ProductMatrix({ caption, label, rows, limit }: {
  caption: string; label: string; rows: ProductRow[]; limit?: number
}) {
  const [expanded, setExpanded] = useState(false)
  const columns = productColumns(rows)
  const visible = limit && !expanded ? rows.slice(0, limit) : rows

  return (
    <div>
      <p className="text-sm font-medium text-gray-500 dark:text-slate-400 mb-3">{caption}</p>
      {/* Wide tables scroll inside their own container — the page body must never scroll sideways. */}
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-gray-200 dark:border-slate-700">
              <th className="py-2 text-left font-medium text-gray-500 dark:text-slate-400">{label}</th>
              {columns.map(t => (
                <th key={t} className="py-2 pl-4 text-right font-medium text-gray-500 dark:text-slate-400 whitespace-nowrap">
                  {HiveProductTypeLabels[t]}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {/* By index: two apiaries can each have a "K1". */}
            {visible.map((r, i) => (
              <tr key={i} className="border-b border-gray-100 dark:border-slate-800 last:border-0">
                <td className="py-2 text-gray-700 dark:text-slate-300">{r.name}</td>
                {columns.map(t => {
                  const kg = productKgIn(r, t)
                  return (
                    <td key={t} className="py-2 pl-4 text-right tabular-nums whitespace-nowrap text-gray-700 dark:text-slate-300">
                      {kg === null ? '—' : fmtProductQty(kg, t)}
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {limit !== undefined && rows.length > limit && (
        <button
          type="button"
          onClick={() => setExpanded(e => !e)}
          className="mt-2 text-sm font-medium text-honey-600 dark:text-honey-400 hover:underline"
        >
          {expanded ? 'Prikaži manje' : `Prikaži sve (${rows.length})`}
        </button>
      )}
    </div>
  )
}
