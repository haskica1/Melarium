import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { format } from 'date-fns'
import { Leaf, Loader2, PencilLine, Plus, Trash2, X } from 'lucide-react'
import { useDiets, useDeleteDiet } from '../../core/services/dietQueries'
import { DietStatus, FoodType } from '../../core/models'
import type { Diet } from '../../core/models'
import { VitalCard, VitalsSkeleton, ConfirmDialog, EmptyState, ErrorState } from '../../shared/components'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useHelpTrigger } from '../../core/help/HelpContext'
import { useToast } from '../../core/context/ToastContext'
import { hivesLabel } from '../../shared/utils/plural'

const CURRENT_YEAR = new Date().getFullYear()
const YEARS = [CURRENT_YEAR, CURRENT_YEAR - 1, CURRENT_YEAR - 2, CURRENT_YEAR - 3, CURRENT_YEAR - 4]

const STATUS_STYLE: Record<DietStatus, string> = {
  [DietStatus.NotStarted]:   'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
  [DietStatus.InProgress]:   'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  [DietStatus.Completed]:    'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  [DietStatus.StoppedEarly]: 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
}

/** "1 L (1:1)" — the note carries what the number cannot, so both are shown or neither. */
export function amountLabel(d: Pick<Diet, 'amountPerHive' | 'amountUnitName' | 'amountNote'>): string | null {
  const qty = d.amountPerHive != null && d.amountUnitName
    ? `${d.amountPerHive.toLocaleString('bs-BA', { maximumFractionDigits: 2 })} ${d.amountUnitName}`
    : null
  if (qty && d.amountNote) return `${qty} (${d.amountNote})`
  return qty ?? d.amountNote ?? null
}

export default function FeedingsPage() {
  const navigate = useNavigate()
  const { canManageDiets, canSeeExpenses } = usePermissions()
  const { openHelp } = useHelpTrigger()
  const { toast } = useToast()

  const [searchParams, setSearchParams] = useSearchParams()
  const beehiveId = Number(searchParams.get('beehiveId')) || undefined

  const [year, setYear] = useState<number>(CURRENT_YEAR)
  const { data: diets = [], isLoading, isError, refetch } = useDiets({ year, beehiveId })
  const deleteDiet = useDeleteDiet()

  const [confirmTarget, setConfirmTarget] = useState<Diet | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await deleteDiet.mutateAsync(confirmTarget.id)
      toast.success('Program prehrane obrisan.')
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri brisanju programa prehrane.')
    } finally {
      setIsDeleting(false)
    }
  }

  const activeCount = diets.filter(
    d => d.status === DietStatus.NotStarted || d.status === DietStatus.InProgress,
  ).length

  // BAM only, matching StatsService.FeedingCost — summing currencies would be a silent lie, and
  // a Beekeeper's diets never carry costTotals at all (server-side omission), so this is 0 for them.
  const feedingCostThisYear = diets.reduce(
    (sum, d) => sum + (d.costTotals?.find(t => t.currency === 'BAM')?.total ?? 0), 0,
  )

  const grouped = useMemo(() => {
    const map = new Map<string, Diet[]>()
    for (const d of diets) {
      const key = d.apiaryName ?? `Pčelinjak #${d.apiaryId}`
      const arr = map.get(key) ?? []
      arr.push(d)
      map.set(key, arr)
    }
    return [...map.entries()].sort((a, b) => a[0].localeCompare(b[0]))
  }, [diets])

  return (
    <div className="animate-fade-in space-y-6">
      {/* Hero */}
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
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Prehrana</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">Programi prehrane po pčelinjaku i njihove runde hranjenja.</p>
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
            {canManageDiets && (
              <button onClick={() => navigate('/feedings/new')} className="btn-primary text-sm" title="Dodaj prehranu" aria-label="Dodaj prehranu">
                <Plus className="w-4 h-4" /> <span className="hidden sm:inline">Dodaj prehranu</span>
              </button>
            )}
          </div>
        </div>
      </div>

      {beehiveId && (
        <div className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-honey-50 dark:bg-slate-800/60 border border-honey-200 dark:border-slate-700 text-sm text-gray-700 dark:text-slate-200">
          <Leaf className="w-4 h-4 text-honey-600 dark:text-honey-400 shrink-0" />
          Prikazani su programi koji obuhvataju jednu košnicu.
          <button
            onClick={() => setSearchParams({}, { replace: true })}
            className="ml-auto flex items-center gap-1 text-xs font-medium text-honey-700 dark:text-honey-300 hover:underline"
          >
            <X className="w-3.5 h-3.5" /> Prikaži sve
          </button>
        </div>
      )}

      {isLoading && <VitalsSkeleton />}

      {isError && <ErrorState message="Greška pri učitavanju prehrane." onRetry={refetch} />}

      {!isLoading && !isError && diets.length === 0 && (
        <EmptyState
          title="Još nema programa prehrane."
          description={`Za ${year}. godinu nema zabilježenih programa prehrane.`}
          action={canManageDiets ? (
            <button onClick={() => navigate('/feedings/new')} className="btn-primary text-sm">
              <Plus className="w-4 h-4" /> Dodaj prehranu
            </button>
          ) : undefined}
          onHelp={openHelp}
        />
      )}

      {!isLoading && diets.length > 0 && (
        <>
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
            <VitalCard icon="🍯" label="Programi"    value={String(diets.length)}     sub={`${year}.`}      gradient="from-honey-400 to-honey-600" />
            <VitalCard icon="🌿" label="Aktivni"     value={String(activeCount)}      sub="u toku"          gradient="from-emerald-400 to-teal-600" />
            <VitalCard icon="🏡" label="Pčelinjaci"  value={String(grouped.length)}   sub="s prehranom"     gradient="from-amber-400 to-orange-500" />
            <VitalCard
              icon="✅"
              label="Runde"
              value={String(diets.reduce((sum, d) => sum + d.completedEntries, 0))}
              sub={`od ${diets.reduce((sum, d) => sum + d.totalEntries, 0)}`}
              gradient="from-violet-400 to-indigo-600"
            />
          </div>

          {grouped.map(([apiaryName, items]) => (
            <div key={apiaryName} className="space-y-3">
              <h2 className="px-1 font-display text-lg font-semibold text-gray-800 dark:text-slate-100 min-w-0 truncate">{apiaryName}</h2>
              <div className="space-y-3">
                {items.map(d => (
                  <DietCard
                    key={d.id}
                    diet={d}
                    canEdit={canManageDiets}
                    isDeleting={confirmTarget?.id === d.id && isDeleting}
                    onOpen={() => navigate(`/feedings/${d.id}`)}
                    onEdit={() => navigate(`/feedings/${d.id}/edit`)}
                    onDelete={() => setConfirmTarget(d)}
                  />
                ))}
              </div>
            </div>
          ))}

          {/* Trošak prehrane (SPEC-12 Phase E) — manager-only; a Beekeeper's diets never carry
              costTotals at all, so this stays at 0 and hidden for them regardless. */}
          {canSeeExpenses && feedingCostThisYear > 0 && (
            <p className="text-sm text-gray-500 dark:text-slate-400 text-right px-1">
              Trošak prehrane {year}.: <span className="font-semibold text-gray-800 dark:text-slate-100">
                {feedingCostThisYear.toLocaleString('bs-BA', { maximumFractionDigits: 2 })} BAM
              </span>
            </p>
          )}
        </>
      )}

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši program prehrane"
        message={confirmTarget
          ? `Obrisati program "${confirmTarget.name}" od ${format(new Date(confirmTarget.startDate), 'dd.MM.yyyy')}?`
          : ''}
        confirmLabel="Obriši"
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}

// ── Diet card ───────────────────────────────────────────────────────────────────

interface DietCardProps {
  diet: Diet
  canEdit: boolean
  isDeleting: boolean
  onOpen: () => void
  onEdit: () => void
  onDelete: () => void
}

function DietCard({ diet: d, canEdit, isDeleting, onOpen, onEdit, onDelete }: DietCardProps) {
  const food = d.foodType === FoodType.Custom ? (d.customFoodType ?? d.foodTypeName) : d.foodTypeName
  const amount = amountLabel(d)

  return (
    <div className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-4 py-3.5 sm:px-5 sm:py-4 flex items-start gap-3 sm:gap-4 hover:border-honey-200 dark:hover:border-slate-700 transition-colors">
      <button onClick={onOpen} className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl flex items-center justify-center shrink-0 bg-honey-50 text-honey-600 dark:bg-honey-500/15 dark:text-honey-300" aria-label={`Otvori program ${d.name}`}>
        <Leaf className="w-4 h-4 sm:w-5 sm:h-5" />
      </button>
      <button onClick={onOpen} className="flex-1 min-w-0 text-left">
        <p className="font-semibold text-gray-900 dark:text-slate-100 leading-snug break-words">{d.name}</p>

        <div className="mt-1 flex flex-wrap items-center gap-x-1.5 gap-y-1">
          <span className={`text-xs rounded-full px-2 py-0.5 whitespace-nowrap ${STATUS_STYLE[d.status]}`}>{d.statusName}</span>
          <span className="text-xs text-gray-500 dark:text-slate-400 bg-gray-100 dark:bg-slate-800 rounded-full px-2 py-0.5 whitespace-nowrap">
            {d.completedEntries}/{d.totalEntries} rundi
          </span>
        </div>

        <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-[13px] sm:text-sm text-gray-500 dark:text-slate-400">
          <span className="whitespace-nowrap">{format(new Date(d.startDate), 'dd.MM.yyyy')}</span>
          <span className="whitespace-nowrap">{hivesLabel(d.hiveCount)}</span>
          <span className="break-words">{food}{amount ? `, ${amount}` : ''}</span>
        </div>
      </button>
      {canEdit && (
        <div className="flex items-center gap-0.5 sm:gap-1 shrink-0 -mr-1.5 -mt-1 sm:mr-0 sm:mt-0">
          <button onClick={onEdit} className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors" aria-label="Uredi program prehrane">
            <PencilLine className="w-4 h-4" />
          </button>
          <button onClick={onDelete} disabled={isDeleting} className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-50" aria-label="Obriši program prehrane">
            {isDeleting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
          </button>
        </div>
      )}
    </div>
  )
}
