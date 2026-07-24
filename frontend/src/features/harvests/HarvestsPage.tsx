import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { format } from 'date-fns'
import { Droplets, Loader2, PencilLine, Plus, Trash2, X } from 'lucide-react'
import { useHarvests, useDeleteHarvest } from '../../core/services/harvestQueries'
import { HoneyTypeLabels } from '../../core/models'
import type { Harvest } from '../../core/models'
import { VitalCard, VitalsSkeleton, ConfirmDialog, EmptyState } from '../../shared/components'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useToast } from '../../core/context/ToastContext'

const CURRENT_YEAR = new Date().getFullYear()
const YEARS = [CURRENT_YEAR, CURRENT_YEAR - 1, CURRENT_YEAR - 2, CURRENT_YEAR - 3]

function fmtKg(kg: number): string {
  return `${kg.toFixed(1).replace(/\.0$/, '')} kg`
}

export default function HarvestsPage() {
  const navigate = useNavigate()
  const { canEditDelete } = usePermissions()
  const { toast } = useToast()

  const [searchParams, setSearchParams] = useSearchParams()
  const beehiveId = Number(searchParams.get('beehiveId')) || undefined

  const [year, setYear] = useState<number>(CURRENT_YEAR)
  const { data: harvests = [], isLoading } = useHarvests({ year, beehiveId })
  const deleteHarvest = useDeleteHarvest()

  const [confirmTarget, setConfirmTarget] = useState<Harvest | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await deleteHarvest.mutateAsync(confirmTarget.id)
      toast.success('Vrcanje obrisano.')
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? e?.message ?? 'Greška pri brisanju vrcanja.')
    } finally {
      setIsDeleting(false)
    }
  }

  // ── Vitals ──
  const totalKg = harvests.reduce((sum, h) => sum + h.totalKg, 0)
  const revenue = harvests.reduce((sum, h) => sum + (h.estimatedRevenue ?? 0), 0)
  const topHoneyType = useMemo(() => {
    const byType = new Map<string, number>()
    harvests.forEach(h => byType.set(h.honeyTypeName, (byType.get(h.honeyTypeName) ?? 0) + h.totalKg))
    return [...byType.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? '—'
  }, [harvests])

  // ── Group by apiary ──
  const grouped = useMemo(() => {
    const map = new Map<string, Harvest[]>()
    for (const h of harvests) {
      const key = h.apiaryName ?? `Pčelinjak #${h.apiaryId}`
      const arr = map.get(key) ?? []
      arr.push(h)
      map.set(key, arr)
    }
    return [...map.entries()].sort((a, b) => a[0].localeCompare(b[0]))
  }, [harvests])

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
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Vrcanja</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">Evidencija prinosa meda po košnici i sezoni.</p>
            </div>
          </div>

          <div className="flex items-center gap-2 shrink-0">
            <select
              value={year}
              onChange={e => setYear(Number(e.target.value))}
              className="px-3 py-2 rounded-xl border border-honey-200 dark:border-slate-700 bg-white/70 dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 outline-none focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
            >
              {YEARS.map(y => <option key={y} value={y}>{y}.</option>)}
            </select>
            {canEditDelete && (
              <button onClick={() => navigate('/harvests/new')} className="btn-primary text-sm">
                <Plus className="w-4 h-4" />
                Dodaj vrcanje
              </button>
            )}
          </div>
        </div>
      </div>

      {beehiveId && (
        <div className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-honey-50 dark:bg-slate-800/60 border border-honey-200 dark:border-slate-700 text-sm text-gray-700 dark:text-slate-200">
          <Droplets className="w-4 h-4 text-honey-600 dark:text-honey-400 shrink-0" />
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

      {!isLoading && harvests.length === 0 && (
        <EmptyState
          title="Još nema evidencije vrcanja."
          description={`Za ${year}. godinu nema zabilježenih vrcanja.`}
          action={canEditDelete ? (
            <button onClick={() => navigate('/harvests/new')} className="btn-primary text-sm">
              <Plus className="w-4 h-4" />
              Dodaj vrcanje
            </button>
          ) : undefined}
        />
      )}

      {!isLoading && harvests.length > 0 && (
        <>
          {/* Vitals */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
            <VitalCard icon="🍯" label="Ukupno meda" value={fmtKg(totalKg)}         sub={`${year}.`}          gradient="from-honey-400 to-honey-600" />
            <VitalCard icon="📋" label="Vrcanja"     value={String(harvests.length)} sub="zapisa"              gradient="from-amber-400 to-orange-500" />
            <VitalCard icon="💰" label="Procj. prihod" value={revenue.toFixed(0)}    sub="KM"                  gradient="from-emerald-400 to-teal-600" />
            <VitalCard icon="🌼" label="Najviše"     value={topHoneyType}            sub="vrsta meda"          gradient="from-violet-400 to-indigo-600" />
          </div>

          {/* Grouped list */}
          {grouped.map(([apiaryName, items]) => {
            const groupKg = items.reduce((s, h) => s + h.totalKg, 0)
            return (
              <div key={apiaryName} className="space-y-3">
                <div className="flex items-center justify-between px-1">
                  <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">{apiaryName}</h2>
                  <span className="text-sm font-medium text-honey-700 dark:text-honey-300">{fmtKg(groupKg)}</span>
                </div>
                <div className="space-y-3">
                  {items.map(h => (
                    <HarvestCard
                      key={h.id}
                      harvest={h}
                      canEdit={canEditDelete}
                      isDeleting={confirmTarget?.id === h.id && isDeleting}
                      onEdit={() => navigate(`/harvests/${h.id}/edit`)}
                      onDelete={() => setConfirmTarget(h)}
                    />
                  ))}
                </div>
              </div>
            )
          })}
        </>
      )}

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši vrcanje"
        message={confirmTarget
          ? `Obrisati vrcanje (${HoneyTypeLabels[confirmTarget.honeyType]}, ${fmtKg(confirmTarget.totalKg)}) od ${format(new Date(confirmTarget.date), 'dd.MM.yyyy')}? Ova radnja se ne može poništiti.`
          : ''}
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}

// ── Harvest card ────────────────────────────────────────────────────────────────

interface HarvestCardProps {
  harvest: Harvest
  canEdit: boolean
  isDeleting: boolean
  onEdit: () => void
  onDelete: () => void
}

function HarvestCard({ harvest, canEdit, isDeleting, onEdit, onDelete }: HarvestCardProps) {
  return (
    <div className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4 flex items-center gap-4 hover:border-honey-200 dark:hover:border-slate-700 transition-colors">
      <div className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 bg-honey-50 text-honey-600 dark:bg-honey-500/15 dark:text-honey-300">
        <Droplets className="w-5 h-5" />
      </div>

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-semibold text-gray-900 dark:text-slate-100">{fmtKg(harvest.totalKg)}</span>
          <span className="text-xs text-honey-700 dark:text-honey-300 bg-honey-100 dark:bg-honey-500/15 rounded-full px-2 py-0.5">
            {harvest.honeyTypeName}
          </span>
        </div>
        <div className="flex items-center gap-3 mt-0.5 text-sm text-gray-500 dark:text-slate-400">
          <span>{format(new Date(harvest.date), 'dd.MM.yyyy')}</span>
          <span>·</span>
          <span>{harvest.entryCount} {harvest.entryCount === 1 ? 'košnica' : 'košnica'}</span>
          {harvest.estimatedRevenue != null && harvest.estimatedRevenue > 0 && (
            <>
              <span>·</span>
              <span>≈ {harvest.estimatedRevenue.toFixed(0)} KM</span>
            </>
          )}
        </div>
      </div>

      {canEdit && (
        <div className="flex items-center gap-1 shrink-0">
          <button
            onClick={onEdit}
            className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
            aria-label="Uredi vrcanje"
          >
            <PencilLine className="w-4 h-4" />
          </button>
          <button
            onClick={onDelete}
            disabled={isDeleting}
            className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-50"
            aria-label="Obriši vrcanje"
          >
            {isDeleting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
          </button>
        </div>
      )}
    </div>
  )
}
