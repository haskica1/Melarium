import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { format } from 'date-fns'
import { Loader2, PencilLine, Plus, Trash2, X } from 'lucide-react'
import { useHarvests, useDeleteHarvest } from '../../core/services/harvestQueries'
import { useMyPlan, isFeatureLocked } from '../../core/services/planService'
import { errorMessage } from '../../core/services/apiClient'
import { HiveProductType, HiveProductTypeLabels } from '../../core/models'
import type { Harvest } from '../../core/models'
import { VitalCard, VitalsSkeleton, ConfirmDialog, EmptyState, ErrorState } from '../../shared/components'
import { usePermissions } from '../../core/hooks/usePermissions'
import { hivesLabel } from '../../shared/utils/plural'
import { HIVE_PRODUCT_TYPES, PRODUCT_ICON, fmtProductQty, fmtProductPrice, sumByType } from '../../shared/utils/hiveProductUnits'
import { useHelpTrigger } from '../../core/help/HelpContext'
import { useToast } from '../../core/context/ToastContext'
import { ProductChip, ProductTotalsChips } from './ProductChips'
import { ProductsReadOnlyNotice } from './ProductsReadOnlyNotice'

const CURRENT_YEAR = new Date().getFullYear()
const YEARS = [CURRENT_YEAR, CURRENT_YEAR - 1, CURRENT_YEAR - 2, CURRENT_YEAR - 3]

/** Records of the whole organization head the list as their own group (SPEC-30). */
const SHARED_GROUP = 'Zajedničko — cijela organizacija'

const fmtMoney = (n: number) => n.toLocaleString('bs-BA', { maximumFractionDigits: 0 })

export default function HarvestsPage() {
  const navigate = useNavigate()
  const { canEditDelete, isOrgAdmin, isSystemAdmin } = usePermissions()
  const { data: plan } = useMyPlan()
  const { openHelp } = useHelpTrigger()
  const { toast } = useToast()

  // Honey is on every plan; the other products are read-only below Standard (SPEC-30).
  const otherProductsLocked = isFeatureLocked(plan, 'hiveProducts')
  // A record of the whole organization speaks for all of it, so only its owner edits or deletes one.
  const mayTouch = (h: Harvest) => canEditDelete && (h.apiaryId != null || isOrgAdmin || isSystemAdmin)
  const mayEdit = (h: Harvest) => mayTouch(h) && (h.productType === HiveProductType.Honey || !otherProductsLocked)

  const [searchParams, setSearchParams] = useSearchParams()
  const beehiveId = Number(searchParams.get('beehiveId')) || undefined

  const [year, setYear] = useState<number>(CURRENT_YEAR)
  const [product, setProduct] = useState<HiveProductType | 0>(0)
  const { data: harvests = [], isLoading, isError, refetch } = useHarvests(
    product ? { year, beehiveId, productType: product } : { year, beehiveId, allProducts: true },
  )
  const deleteHarvest = useDeleteHarvest()

  const [confirmTarget, setConfirmTarget] = useState<Harvest | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await deleteHarvest.mutateAsync(confirmTarget.id)
      toast.success('Zapis obrisan.')
      setConfirmTarget(null)
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setIsDeleting(false)
    }
  }

  // ── Group by apiary, the organization's own first ──
  const grouped = useMemo(() => {
    const map = new Map<string, Harvest[]>()
    for (const h of harvests) {
      const key = h.apiaryId == null ? SHARED_GROUP : h.apiaryName ?? `Pčelinjak #${h.apiaryId}`
      map.set(key, [...(map.get(key) ?? []), h])
    }
    return [...map.entries()].sort((a, b) =>
      a[0] === SHARED_GROUP ? -1 : b[0] === SHARED_GROUP ? 1 : a[0].localeCompare(b[0], 'bs'))
  }, [harvests])

  const addButton = (
    <button onClick={() => navigate('/harvests/new')} className="btn-primary text-sm" title="Dodaj prinos" aria-label="Dodaj prinos">
      <Plus className="w-4 h-4" />
      <span className="hidden sm:inline">Dodaj prinos</span>
    </button>
  )

  const selectClass =
    'px-3 py-2 rounded-xl border border-honey-200 dark:border-slate-700 bg-white/70 dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 outline-none focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all'

  return (
    <div className="animate-fade-in space-y-6">

      {/* ── Hero ──────────────────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center gap-4 min-w-0">
            <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
              🍯
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Prinosi</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">Med, vosak, propolis, polen i ostali proizvodi — po košnici, pčelinjaku i sezoni.</p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2 shrink-0">
            <select value={product} onChange={e => setProduct(Number(e.target.value))} className={selectClass} aria-label="Proizvod">
              <option value={0}>Svi proizvodi</option>
              {HIVE_PRODUCT_TYPES.map(t => <option key={t} value={t}>{HiveProductTypeLabels[t]}</option>)}
            </select>
            <select value={year} onChange={e => setYear(Number(e.target.value))} className={selectClass} aria-label="Godina">
              {YEARS.map(y => <option key={y} value={y}>{y}.</option>)}
            </select>
            {canEditDelete && addButton}
          </div>
        </div>
      </div>

      {canEditDelete && otherProductsLocked && <ProductsReadOnlyNotice />}

      {beehiveId && (
        <div className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-honey-50 dark:bg-slate-800/60 border border-honey-200 dark:border-slate-700 text-sm text-gray-700 dark:text-slate-200">
          <span aria-hidden>🐝</span>
          Prikazan je prinos jedne košnice.
          <button
            onClick={() => setSearchParams({}, { replace: true })}
            className="ml-auto flex items-center gap-1 text-xs font-medium text-honey-700 dark:text-honey-300 hover:underline"
          >
            <X className="w-3.5 h-3.5" /> Prikaži sve
          </button>
        </div>
      )}

      {isLoading && <VitalsSkeleton />}

      {isError && <ErrorState message="Greška pri učitavanju prinosa." onRetry={refetch} />}

      {!isLoading && !isError && harvests.length === 0 && (
        <EmptyState
          title="Još nema evidencije prinosa."
          description={product
            ? `Za ${year}. godinu nema zapisa: ${HiveProductTypeLabels[product]}.`
            : `Za ${year}. godinu nema zabilježenog meda ni drugih proizvoda.`}
          action={canEditDelete ? addButton : undefined}
          onHelp={openHelp}
        />
      )}

      {!isLoading && harvests.length > 0 && (
        <>
          {product === HiveProductType.Honey
            ? <HoneyVitals harvests={harvests} year={year} />
            : <ProductVitals harvests={harvests} year={year} />}

          {!product && <ProductTotalsChips totals={sumByType(harvests)} />}

          {grouped.map(([group, items]) => (
            <div key={group} className="space-y-3">
              <div className="flex flex-wrap items-center justify-between gap-2 px-1">
                <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 min-w-0 truncate">{group}</h2>
                <ProductTotalsChips totals={sumByType(items)} small />
              </div>
              <div className="space-y-3">
                {items.map(h => (
                  <HarvestCard
                    key={h.id}
                    harvest={h}
                    canEdit={mayEdit(h)}
                    canDelete={mayTouch(h)}
                    isDeleting={confirmTarget?.id === h.id && isDeleting}
                    onEdit={() => navigate(`/harvests/${h.id}/edit`)}
                    onDelete={() => setConfirmTarget(h)}
                  />
                ))}
              </div>
            </div>
          ))}
        </>
      )}

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši zapis"
        message={confirmTarget
          ? `Obrisati zapis (${HiveProductTypeLabels[confirmTarget.productType]}, ${fmtProductQty(confirmTarget.totalKg, confirmTarget.productType)}) od ${format(new Date(confirmTarget.date), 'dd.MM.yyyy')}? Ova radnja se ne može poništiti.`
          : ''}
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}

// ── Vitals ───────────────────────────────────────────────────────────────────────

/** Honey alone — the figures the Vrcanja page always had. */
function HoneyVitals({ harvests, year }: { harvests: Harvest[]; year: number }) {
  const totalKg = harvests.reduce((sum, h) => sum + h.totalKg, 0)
  const revenue = harvests.reduce((sum, h) => sum + (h.estimatedRevenue ?? 0), 0)
  const byType = new Map<string, number>()
  harvests.forEach(h => byType.set(h.honeyTypeName, (byType.get(h.honeyTypeName) ?? 0) + h.totalKg))
  const top = [...byType.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] || '—'

  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
      <VitalCard icon="🍯" label="Ukupno meda"   value={fmtProductQty(totalKg, HiveProductType.Honey)} sub={`${year}.`} gradient="from-honey-400 to-honey-600" />
      <VitalCard icon="📋" label="Vrcanja"       value={String(harvests.length)} sub="zapisa"    gradient="from-amber-400 to-orange-500" />
      <VitalCard icon="💰" label="Procj. prihod" value={fmtMoney(revenue)}       sub="KM"        gradient="from-emerald-400 to-teal-600" />
      <VitalCard icon="🌼" label="Najviše"       value={top}                     sub="vrsta meda" gradient="from-violet-400 to-indigo-600" />
    </div>
  )
}

/** Several products: nothing here sums kg across them — revenue is the one comparable figure. */
function ProductVitals({ harvests, year }: { harvests: Harvest[]; year: number }) {
  const revenue = harvests.reduce((sum, h) => sum + (h.estimatedRevenue ?? 0), 0)
  const unpriced = harvests.filter(h => h.pricePerKg == null).length
  const types = new Set(harvests.map(h => h.productType)).size

  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
      <VitalCard icon="📋" label="Zapisa"        value={String(harvests.length)} sub={`${year}.`}  gradient="from-honey-400 to-honey-600" />
      <VitalCard icon="🐝" label="Proizvoda"     value={String(types)}           sub="različitih" gradient="from-amber-400 to-orange-500" />
      <VitalCard icon="💰" label="Procj. prihod" value={fmtMoney(revenue)}       sub="KM"         gradient="from-emerald-400 to-teal-600" />
      <VitalCard
        icon="🏷️"
        label="Bez cijene"
        value={String(unpriced)}
        sub={unpriced > 0 ? 'nije u procjeni' : 'sve ima cijenu'}
        subAlert={unpriced > 0}
        gradient="from-violet-400 to-indigo-600"
      />
    </div>
  )
}

// ── Harvest card ──────────────────────────────────────────────────────────────────

interface HarvestCardProps {
  harvest: Harvest
  canEdit: boolean
  canDelete: boolean
  isDeleting: boolean
  onEdit: () => void
  onDelete: () => void
}

/** Where the quantity was recorded, in the words a beekeeper would use. */
function levelLabel(h: Harvest): string {
  if (h.apiaryId == null) return 'cijela organizacija'
  if (h.entryCount === 0) return 'ukupno za pčelinjak'
  return hivesLabel(h.entryCount)
}

function HarvestCard({ harvest: h, canEdit, canDelete, isDeleting, onEdit, onDelete }: HarvestCardProps) {
  return (
    <div className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-4 py-3.5 sm:px-5 sm:py-4 flex items-start gap-3 sm:gap-4 hover:border-honey-200 dark:hover:border-slate-700 transition-colors">
      <div className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl flex items-center justify-center shrink-0 bg-honey-50 dark:bg-honey-500/15 text-lg" aria-hidden>
        {PRODUCT_ICON[h.productType]}
      </div>

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <ProductChip productType={h.productType} kg={h.totalKg} bare />
          {h.honeyTypeName && (
            <span className="text-xs text-honey-700 dark:text-honey-300 border border-honey-200 dark:border-honey-500/30 rounded-full px-2 py-0.5">
              {h.honeyTypeName}
            </span>
          )}
        </div>
        {/* Wrapping, and no "·" separators: on a phone the content column is ~160 px, so a single
            non-wrapping row squeezed date/hives/revenue into each other ("podaci su zbijeni"). */}
        <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-[13px] sm:text-sm text-gray-500 dark:text-slate-400">
          <span className="whitespace-nowrap">{format(new Date(h.date), 'dd.MM.yyyy')}</span>
          <span className="whitespace-nowrap">{levelLabel(h)}</span>
          {h.pricePerKg != null && (
            <span className="whitespace-nowrap">{fmtProductPrice(h.pricePerKg, h.productType)}</span>
          )}
          {h.estimatedRevenue != null && h.estimatedRevenue > 0 && (
            <span className="whitespace-nowrap">≈ {fmtMoney(h.estimatedRevenue)} KM</span>
          )}
        </div>
        {h.notes && <p className="mt-1 text-[13px] sm:text-sm text-gray-500 dark:text-slate-400 line-clamp-2">{h.notes}</p>}
      </div>

      {(canEdit || canDelete) && (
        <div className="flex items-center gap-0.5 sm:gap-1 shrink-0 -mr-1.5 -mt-1 sm:mr-0 sm:mt-0">
          {canEdit && (
            <button
              onClick={onEdit}
              className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
              aria-label="Uredi zapis"
            >
              <PencilLine className="w-4 h-4" />
            </button>
          )}
          {canDelete && (
            <button
              onClick={onDelete}
              disabled={isDeleting}
              className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-50"
              aria-label="Obriši zapis"
            >
              {isDeleting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
            </button>
          )}
        </div>
      )}
    </div>
  )
}
