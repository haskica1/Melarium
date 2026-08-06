import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import {
  CheckCircle2, Circle, Pencil, Trash2, Plus, X, Loader2,
  CalendarDays, Utensils, AlertTriangle, ChevronDown, ChevronUp, StickyNote,
  Droplets, ReceiptText,
} from 'lucide-react'
import { format, isPast, isToday } from 'date-fns'
import { useBeehivesByApiary } from '../../core/services/queries'
import {
  useDiet,
  useDeleteDiet,
  useCompleteEarlyDiet,
  useCompleteFeedingEntry,
  useAddDietBeehives,
  useRemoveDietBeehive,
} from '../../core/services/dietQueries'
import {
  ErrorMessage, ConfirmDialog, VitalCard, PageSkeleton,
} from '../../shared/components'
import { useDialogBehavior } from '../../shared/hooks/useDialogBehavior'
import { useFormNavigation } from '../../shared/hooks/useFormNavigation'
import { DietStatus, FeedingEntryStatus, FoodType } from '../../core/models'
import type { FeedingEntry, DietBeehive } from '../../core/models'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useToast } from '../../core/context/ToastContext'
import { amountLabel } from './FeedingsPage'

// ── Status badge ──────────────────────────────────────────────────────────────

function StatusBadge({ status, statusName }: { status: DietStatus; statusName: string }) {
  const styles: Record<DietStatus, string> = {
    [DietStatus.NotStarted]:   'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
    [DietStatus.InProgress]:   'bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300',
    [DietStatus.Completed]:    'bg-green-100 text-green-700 dark:bg-green-500/15 dark:text-green-300',
    [DietStatus.StoppedEarly]: 'bg-red-100 text-red-600 dark:bg-red-500/15 dark:text-red-300',
  }
  return (
    <span className={`badge ${styles[status]}`}>{statusName}</span>
  )
}

// ── Feeding round row ─────────────────────────────────────────────────────────

function EntryRow({
  entry,
  canComplete,
  onComplete,
  isCompleting,
}: {
  entry: FeedingEntry
  canComplete: boolean
  onComplete: (entry: FeedingEntry) => void
  isCompleting: boolean
}) {
  const date = new Date(entry.scheduledDate)
  const done = entry.status === FeedingEntryStatus.Completed
  const overdue = !done && isPast(date) && !isToday(date)
  const today = !done && isToday(date)

  return (
    <div
      className={`flex items-center gap-3 py-3 px-4 rounded-xl border transition-colors ${
        done
          ? 'bg-green-50 border-green-100 dark:bg-green-500/10 dark:border-green-500/20'
          : overdue
          ? 'bg-red-50 border-red-100 dark:bg-red-500/10 dark:border-red-500/20'
          : today
          ? 'bg-amber-50 border-amber-100 dark:bg-amber-500/10 dark:border-amber-500/20'
          : 'bg-white border-gray-100 dark:bg-slate-900 dark:border-slate-800'
      }`}
    >
      {/* Checkbox / icon */}
      {done ? (
        <CheckCircle2 className="w-5 h-5 text-green-500 shrink-0" />
      ) : (
        <button
          onClick={() => canComplete && onComplete(entry)}
          disabled={!canComplete || isCompleting}
          className={`shrink-0 transition-colors ${
            canComplete
              ? 'text-gray-300 dark:text-slate-600 hover:text-honey-500 dark:hover:text-honey-400 cursor-pointer'
              : 'text-gray-200 dark:text-slate-700 cursor-not-allowed'
          }`}
          title={canComplete ? 'Označi rundu kao obavljenu' : 'Prehrana je završena'}
        >
          <Circle className="w-5 h-5" />
        </button>
      )}

      {/* Date info */}
      <div className="flex-1 min-w-0">
        <p className={`text-sm font-medium ${done ? 'text-gray-500 dark:text-slate-500 line-through' : 'text-gray-800 dark:text-slate-100'}`}>
          {format(date, 'EEEE, dd MMM yyyy')}
          {today && <span className="ml-2 text-xs font-semibold text-amber-600 dark:text-amber-400">Danas</span>}
          {overdue && <span className="ml-2 text-xs font-semibold text-red-500 dark:text-red-400">Kasni</span>}
        </p>
        {done && entry.completionDate && (
          <p className="text-xs text-gray-400 dark:text-slate-500 mt-0.5">
            Obavljeno {format(new Date(entry.completionDate), 'dd MMM yyyy, HH:mm')}
          </p>
        )}
        {entry.note && (
          <p className="text-xs text-gray-500 dark:text-slate-400 mt-1 flex items-start gap-1.5">
            <StickyNote className="w-3.5 h-3.5 shrink-0 mt-px" />
            <span className="break-words">{entry.note}</span>
          </p>
        )}
      </div>

      {/* Status chip */}
      <span className={`badge shrink-0 ${
        done ? 'bg-green-100 text-green-700 dark:bg-green-500/15 dark:text-green-300' :
        overdue ? 'bg-red-100 text-red-600 dark:bg-red-500/15 dark:text-red-300' :
        today ? 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300' :
        'bg-gray-100 text-gray-500 dark:bg-slate-700 dark:text-slate-300'
      }`}>
        {done ? 'Obavljeno' : overdue ? 'Kasni' : today ? 'Danas' : 'Na čekanju'}
      </span>
    </div>
  )
}

// ── Complete-round modal ──────────────────────────────────────────────────────

function CompleteRoundModal({
  entry, onConfirm, onCancel, isLoading,
}: {
  entry: FeedingEntry
  onConfirm: (note: string) => void
  onCancel: () => void
  isLoading: boolean
}) {
  const { panelProps } = useDialogBehavior({ open: true, onClose: onCancel })
  const [note, setNote] = useState('')

  return (
    <div
      {...panelProps}
      aria-label="Označi rundu kao obavljenu"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4 outline-none"
    >
      <div className="bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl p-6 w-full max-w-md animate-fade-in">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-green-100 dark:bg-green-500/15 flex items-center justify-center shrink-0">
            <CheckCircle2 className="w-5 h-5 text-green-500" />
          </div>
          <div className="min-w-0">
            <h2 className="font-semibold text-gray-800 dark:text-slate-100">Hranjenje obavljeno</h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              Runda od {format(new Date(entry.scheduledDate), 'dd.MM.yyyy')} — vrijedi za sve košnice na programu.
            </p>
          </div>
        </div>

        <label className="form-label">Napomena (opcionalno)</label>
        <textarea
          className="form-input resize-none h-20"
          placeholder="npr. košnice 7 i 12 preskočene — slabe"
          maxLength={300}
          value={note}
          onChange={e => setNote(e.target.value)}
        />

        <div className="flex gap-3 mt-4">
          <button onClick={onCancel} className="btn-secondary flex-1 text-sm py-2" disabled={isLoading}>
            Otkaži
          </button>
          <button onClick={() => onConfirm(note.trim())} className="btn-primary flex-1 text-sm py-2" disabled={isLoading}>
            {isLoading ? 'Spremam…' : 'Označi obavljeno'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Add-hives modal ───────────────────────────────────────────────────────────

function AddHivesModal({
  apiaryId, excludedIds, onConfirm, onCancel, isLoading,
}: {
  apiaryId: number
  excludedIds: Set<number>
  onConfirm: (ids: number[]) => void
  onCancel: () => void
  isLoading: boolean
}) {
  const { panelProps } = useDialogBehavior({ open: true, onClose: onCancel })
  const { data: hives = [], isLoading: loadingHives, isError } = useBeehivesByApiary(apiaryId)
  const [checked, setChecked] = useState<Record<number, boolean>>({})

  // Previously removed hives are offered again on purpose — re-adding creates a new link and keeps
  // the old one as history.
  const available = hives.filter(h => !excludedIds.has(h.id))
  const selected = available.filter(h => checked[h.id]).map(h => h.id)

  return (
    <div
      {...panelProps}
      aria-label="Dodaj košnice na program"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4 outline-none"
    >
      <div className="bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl p-6 w-full max-w-md animate-fade-in">
        <h2 className="font-semibold text-gray-800 dark:text-slate-100 mb-1">Dodaj košnice</h2>
        <p className="text-sm text-gray-500 dark:text-slate-400 mb-4">
          Košnice se uključuju od sada — ranije runde im se ne pripisuju.
        </p>

        {loadingHives ? (
          <div className="flex justify-center py-6"><Loader2 className="w-5 h-5 animate-spin text-honey-500" /></div>
        ) : isError ? (
          <ErrorMessage message="Greška pri učitavanju košnica." />
        ) : available.length === 0 ? (
          <p className="text-sm text-gray-400 dark:text-slate-500 py-4">Sve košnice ovog pčelinjaka su već na programu.</p>
        ) : (
          <div className="space-y-2 max-h-64 overflow-y-auto">
            {available.map(h => (
              <label key={h.id} className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={!!checked[h.id]}
                  onChange={e => setChecked(prev => ({ ...prev, [h.id]: e.target.checked }))}
                  className="w-4 h-4 rounded border-gray-300 dark:border-slate-600 text-honey-500 focus:ring-honey-400 accent-honey-500"
                />
                <span className="text-sm text-gray-700 dark:text-slate-200 truncate">{h.name}</span>
              </label>
            ))}
          </div>
        )}

        <div className="flex gap-3 mt-5">
          <button onClick={onCancel} className="btn-secondary flex-1 text-sm py-2" disabled={isLoading}>Otkaži</button>
          <button
            onClick={() => onConfirm(selected)}
            className="btn-primary flex-1 text-sm py-2"
            disabled={isLoading || selected.length === 0}
          >
            {isLoading ? 'Dodajem…' : `Dodaj (${selected.length})`}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Early-complete modal ──────────────────────────────────────────────────────

function CompleteEarlyModal({
  onConfirm,
  onCancel,
  isLoading,
}: {
  onConfirm: (comment: string) => void
  onCancel: () => void
  isLoading: boolean
}) {
  const { panelProps } = useDialogBehavior({ open: true, onClose: onCancel })
  const [comment, setComment] = useState('')
  const [err, setErr] = useState('')

  function submit() {
    if (!comment.trim()) { setErr('Molimo unesite razlog.'); return }
    onConfirm(comment.trim())
  }

  return (
    <div
      {...panelProps}
      aria-label="Prekini prehranu ranije"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4 outline-none"
    >
      <div className="bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl p-6 w-full max-w-md animate-fade-in">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-red-100 dark:bg-red-500/15 flex items-center justify-center shrink-0">
            <AlertTriangle className="w-5 h-5 text-red-500" />
          </div>
          <div>
            <h2 className="font-semibold text-gray-800 dark:text-slate-100">Zaustavi prehranu ranije</h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">Molimo objasnite zašto se prehrana zaustavlja ranije.</p>
          </div>
        </div>

        <textarea
          className={`form-input resize-none h-24 ${err ? 'border-red-400' : ''}`}
          placeholder="Razlog za prijevremeno završavanje…"
          value={comment}
          onChange={e => { setComment(e.target.value); setErr('') }}
        />
        {err && <p className="form-error mt-1">{err}</p>}

        <div className="flex gap-3 mt-4">
          <button onClick={onCancel} className="btn-secondary flex-1 text-sm py-2" disabled={isLoading}>
            Otkaži
          </button>
          <button onClick={submit} className="btn-primary flex-1 text-sm py-2 bg-red-500 hover:bg-red-600" disabled={isLoading}>
            {isLoading ? 'Zaustavljam…' : 'Zaustavi'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function DietDetailPage() {
  const { id } = useParams<{ id: string }>()
  const dietId = Number(id)

  const { canManageDiets, canSeeExpenses } = usePermissions()
  const { toast } = useToast()
  const { data: diet, isLoading, error } = useDiet(dietId)

  // Deleting the programme makes this page's history entry a dead end — leave via goAfterSave so
  // Back can't return to a feeding that no longer exists.
  const { goAfterSave } = useFormNavigation('/feedings')
  const deleteMutation   = useDeleteDiet()
  const completeMutation = useCompleteEarlyDiet(dietId)
  const entryMutation    = useCompleteFeedingEntry(dietId)
  const addHives         = useAddDietBeehives(dietId)
  const removeHive       = useRemoveDietBeehive(dietId)

  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false)
  const [showCompleteEarly, setShowCompleteEarly] = useState(false)
  const [showAddHives,      setShowAddHives]      = useState(false)
  const [completingEntry,   setCompletingEntry]   = useState<FeedingEntry | null>(null)
  const [removeTarget,      setRemoveTarget]      = useState<DietBeehive | null>(null)
  const [showCompleted,     setShowCompleted]     = useState(true)

  if (isLoading) return <PageSkeleton />
  if (error)     return <ErrorMessage message={error.message} />
  if (!diet)     return null

  const isFinished = diet.status === DietStatus.Completed || diet.status === DietStatus.StoppedEarly
  const canDelete  = canManageDiets && diet.status === DietStatus.NotStarted
  const canEdit    = canManageDiets && !isFinished

  const activeHives  = diet.beehives.filter(b => !b.removedOn)
  const removedHives = diet.beehives.filter(b => b.removedOn)

  const pendingEntries   = diet.feedingEntries.filter(e => e.status === FeedingEntryStatus.Pending)
  const completedEntries = diet.feedingEntries.filter(e => e.status === FeedingEntryStatus.Completed)

  const food = diet.foodType === FoodType.Custom && diet.customFoodType ? diet.customFoodType : diet.foodTypeName
  const amount = amountLabel(diet)

  async function handleDelete() {
    await deleteMutation.mutateAsync(dietId)
    goAfterSave('/feedings')
  }

  async function handleCompleteEarly(comment: string) {
    await completeMutation.mutateAsync({ comment })
    setShowCompleteEarly(false)
  }

  async function handleCompleteRound(note: string) {
    if (!completingEntry) return
    try {
      await entryMutation.mutateAsync({ entryId: completingEntry.id, payload: { note: note || null } })
      setCompletingEntry(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri označavanju runde.')
    }
  }

  async function handleAddHives(ids: number[]) {
    try {
      await addHives.mutateAsync({ beehiveIds: ids })
      toast.success('Košnice dodane na program.')
      setShowAddHives(false)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri dodavanju košnica.')
    }
  }

  async function handleRemoveHive() {
    if (!removeTarget) return
    try {
      await removeHive.mutateAsync(removeTarget.beehiveId)
      setRemoveTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri izbacivanju košnice.')
    }
  }

  const progressPct = diet.totalEntries > 0
    ? Math.round((diet.completedEntries / diet.totalEntries) * 100)
    : 0

  return (
    <div className="animate-fade-in">
      {/* ── Hero ──────────────────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none mb-6">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div className="flex items-center gap-4 min-w-0">
              <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
                🍽️
              </div>
              <div className="min-w-0">
                <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50 truncate">{diet.name}</h1>
                <div className="mt-1 flex items-center gap-2 flex-wrap">
                  <StatusBadge status={diet.status} statusName={diet.statusName} />
                  <span className="text-sm text-gray-600 dark:text-slate-400">
                    {diet.apiaryName ?? `Pčelinjak #${diet.apiaryId}`} · {food}
                    {amount ? ` · ${amount}` : ''}
                  </span>
                </div>
              </div>
            </div>

            <div className="flex gap-2 flex-wrap shrink-0">
              {canDelete && (
                <button
                  onClick={() => setShowDeleteConfirm(true)}
                  className="btn-secondary text-sm text-red-500 hover:text-red-600 hover:bg-red-50 border-red-200 dark:text-red-400 dark:hover:text-red-300 dark:hover:bg-red-500/10 dark:border-red-500/30"
                >
                  <Trash2 className="w-4 h-4" /> Obriši
                </button>
              )}
              {canManageDiets && !isFinished && (
                <button onClick={() => setShowCompleteEarly(true)} className="btn-secondary text-sm">Zaustavi ranije</button>
              )}
              {canEdit && (
                <Link to={`/feedings/${dietId}/edit`} className="btn-secondary text-sm"><Pencil className="w-4 h-4" /> Uredi</Link>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* ── Vitals ────────────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger mb-6">
        <VitalCard
          icon="📊" label="Napredak" value={`${progressPct}%`} sub="obavljeno"
          gradient={
            diet.status === DietStatus.StoppedEarly ? 'from-red-400 to-rose-600'
            : diet.status === DietStatus.Completed ? 'from-emerald-400 to-green-600'
            : 'from-honey-400 to-honey-600'
          }
        />
        <VitalCard icon="✅" label="Runde"     value={`${diet.completedEntries}/${diet.totalEntries}`} sub="obavljeno" gradient="from-amber-400 to-orange-500" />
        <VitalCard icon="🐝" label="Košnice"   value={String(diet.hiveCount)} sub="na programu" gradient="from-emerald-400 to-teal-600" />
        <VitalCard icon="⏱️" label="Frekvencija" value={String(diet.frequencyDays)} sub={`svakih ${diet.frequencyDays} ${diet.frequencyDays === 1 ? 'dan' : 'dana'}`} gradient="from-violet-400 to-indigo-600" />
      </div>

      {/* ── Summary card ──────────────────────────────────────────────────────── */}
      <div className="card mb-6">
        {/* Progress bar */}
        <div className="h-2 bg-gray-100 dark:bg-slate-700 rounded-full overflow-hidden mb-4">
          <div
            className={`h-full rounded-full transition-all duration-500 ${
              diet.status === DietStatus.Completed ? 'bg-green-400' :
              diet.status === DietStatus.StoppedEarly ? 'bg-red-400' :
              'bg-honey-400'
            }`}
            style={{ width: `${progressPct}%` }}
          />
        </div>

        <div className="grid grid-cols-2 gap-4 text-center text-sm">
          <InfoItem icon={<CalendarDays className="w-4 h-4" />} label="Datum početka"
            value={format(new Date(diet.startDate), 'dd MMM yyyy')} />
          <InfoItem icon={<Utensils className="w-4 h-4" />} label="Hrana"
            value={amount ? `${food}, ${amount}` : food} />
        </div>

        {diet.status === DietStatus.StoppedEarly && diet.earlyCompletionComment && (
          <div className="mt-4 pt-4 border-t border-red-100 dark:border-red-500/20 flex gap-2 text-sm text-red-700 dark:text-red-300">
            <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
            <span><strong>Zaustavljeno ranije:</strong> {diet.earlyCompletionComment}</span>
          </div>
        )}
        {diet.createdByName && (
          <p className="mt-3 pt-3 border-t border-honey-100 dark:border-slate-800 text-xs text-gray-500 dark:text-slate-400 flex items-center gap-1.5">
            👤 Kreirao {diet.createdByName}
          </p>
        )}
      </div>

      {/* ── Consumption & cost (SPEC-12 Phase E) ─────────────────────────────────
          Potrošnja (from the programme) is visible to everyone who can read the diet — it is a
          quantity, not money. Trošak (attributed expenses) is manager-only; the server already
          omits it for a Beekeeper, so canSeeExpenses here just avoids rendering an always-empty
          section for a role that could never fill it in anyway. */}
      {(canSeeExpenses || diet.plannedAmount != null) && (
        <div className="card mb-6">
          <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 mb-3">Potrošnja i trošak</h2>

          {diet.plannedAmount != null ? (
            <div className="grid grid-cols-2 gap-4 text-center text-sm mb-1">
              <InfoItem icon={<Droplets className="w-4 h-4" />} label="Planirano"
                value={`${diet.plannedAmount.toLocaleString('bs-BA', { maximumFractionDigits: 2 })} ${diet.amountUnitName ?? ''}`} />
              <InfoItem icon={<Droplets className="w-4 h-4" />} label="Do sada"
                value={`${(diet.consumedAmount ?? 0).toLocaleString('bs-BA', { maximumFractionDigits: 2 })} ${diet.amountUnitName ?? ''}`} />
            </div>
          ) : (
            <p className="text-sm text-gray-400 dark:text-slate-500">
              Količina po košnici nije unesena — potrošnja se ne može izračunati.
            </p>
          )}

          {canSeeExpenses && (
            <div className="mt-4 pt-4 border-t border-honey-100 dark:border-slate-800">
              {diet.costTotals && diet.costTotals.length > 0 ? (
                <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
                  {diet.costTotals.map(t => (
                    <span key={t.currency} className="font-semibold text-gray-800 dark:text-slate-100">
                      Trošak: {t.total.toLocaleString('bs-BA', { maximumFractionDigits: 2 })} {t.currency}
                    </span>
                  ))}
                  {diet.costPerHive != null && (
                    <span className="text-gray-500 dark:text-slate-400">
                      ({diet.costPerHive.toLocaleString('bs-BA', { maximumFractionDigits: 2 })} po košnici)
                    </span>
                  )}
                  <Link
                    to={`/expenses/new?dietId=${diet.id}`}
                    className="sm:ml-auto text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium"
                  >
                    Poveži još troška
                  </Link>
                </div>
              ) : (
                <Link
                  to={`/expenses/new?dietId=${diet.id}`}
                  className="inline-flex items-center gap-1.5 text-sm text-honey-600 dark:text-honey-400 hover:underline font-medium"
                >
                  <ReceiptText className="w-4 h-4" /> Poveži trošak
                </Link>
              )}
            </div>
          )}
        </div>
      )}

      {/* ── Hives on the programme ────────────────────────────────────────────── */}
      <div className="card mb-6">
        <div className="flex items-center justify-between gap-2 mb-3">
          <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">
            Košnice <span className="text-sm font-normal text-gray-400 dark:text-slate-500">({activeHives.length})</span>
          </h2>
          {/* Hidden on a finished programme — the backend rejects it with 422 anyway. */}
          {canManageDiets && !isFinished && (
            <button onClick={() => setShowAddHives(true)} className="btn-secondary text-sm py-1.5">
              <Plus className="w-4 h-4" /> Dodaj košnice
            </button>
          )}
        </div>

        {activeHives.length === 0 ? (
          <p className="text-sm text-gray-400 dark:text-slate-500">
            Nijedna košnica trenutno nije na ovom programu.
          </p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {activeHives.map(b => (
              <span
                key={b.id}
                className="inline-flex items-center gap-1.5 pl-3 pr-1.5 py-1.5 rounded-xl bg-honey-50 dark:bg-honey-500/10 border border-honey-200 dark:border-honey-500/30 text-sm text-gray-700 dark:text-slate-200"
              >
                <Link to={`/beehives/${b.beehiveId}`} className="hover:underline truncate max-w-[10rem]">{b.beehiveName}</Link>
                {canManageDiets && (
                  <button
                    onClick={() => setRemoveTarget(b)}
                    className="p-0.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 transition-colors"
                    aria-label={`Izbaci ${b.beehiveName} iz programa`}
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                )}
              </span>
            ))}
          </div>
        )}

        {removedHives.length > 0 && (
          <div className="mt-4 pt-3 border-t border-honey-100 dark:border-slate-800">
            <p className="text-xs text-gray-400 dark:text-slate-500 mb-2">Ranije na programu</p>
            <div className="flex flex-wrap gap-2">
              {removedHives.map(b => (
                <span
                  key={b.id}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-gray-50 dark:bg-slate-800 border border-gray-200 dark:border-slate-700 text-sm text-gray-400 dark:text-slate-500 line-through"
                  title={`Uklonjena ${format(new Date(b.removedOn!), 'dd.MM.yyyy')}`}
                >
                  <span className="truncate max-w-[10rem]">{b.beehiveName}</span>
                  <span className="text-xs no-underline">uklonjena {format(new Date(b.removedOn!), 'dd.MM.')}</span>
                </span>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Pending rounds */}
      <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 mb-3">
        Predstojeće runde
        {pendingEntries.length > 0 && (
          <span className="ml-2 text-sm font-normal text-gray-400 dark:text-slate-500">({pendingEntries.length})</span>
        )}
      </h2>

      {pendingEntries.length === 0 ? (
        <div className="card text-center text-gray-400 dark:text-slate-500 py-8 mb-6">
          <CheckCircle2 className="w-10 h-10 mx-auto mb-2 text-green-300" />
          <p className="text-sm">Sve runde su evidentirane.</p>
        </div>
      ) : (
        <div className="space-y-2 mb-6">
          {pendingEntries.map(entry => (
            <EntryRow
              key={entry.id}
              entry={entry}
              canComplete={!isFinished}
              onComplete={setCompletingEntry}
              isCompleting={entryMutation.isPending}
            />
          ))}
        </div>
      )}

      {/* Completed rounds (collapsible) */}
      {completedEntries.length > 0 && (
        <div className="mb-8">
          <button
            onClick={() => setShowCompleted(v => !v)}
            className="flex items-center gap-2 text-sm font-semibold text-gray-600 dark:text-slate-300 hover:text-gray-800 dark:hover:text-slate-100 mb-3 transition-colors"
          >
            {showCompleted
              ? <ChevronUp className="w-4 h-4" />
              : <ChevronDown className="w-4 h-4" />
            }
            Obavljene runde ({completedEntries.length})
          </button>

          {showCompleted && (
            <div className="space-y-2 opacity-80">
              {completedEntries.map(entry => (
                <EntryRow
                  key={entry.id}
                  entry={entry}
                  canComplete={false}
                  onComplete={() => {}}
                  isCompleting={false}
                />
              ))}
            </div>
          )}
        </div>
      )}

      {/* Delete confirm */}
      <ConfirmDialog
        isOpen={showDeleteConfirm}
        title="Obriši prehranu"
        message="Da li ste sigurni da želite obrisati ovaj program prehrane? Ova radnja ne može biti poništena."
        onConfirm={handleDelete}
        onCancel={() => setShowDeleteConfirm(false)}
        isLoading={deleteMutation.isPending}
      />

      {/* Remove-hive confirm */}
      <ConfirmDialog
        isOpen={!!removeTarget}
        title="Izbaci košnicu iz programa"
        message={removeTarget
          ? `Izbaciti košnicu "${removeTarget.beehiveName}" iz programa? Ostaje zapisano da je do danas bila na prehrani, a možete je kasnije vratiti.`
          : ''}
        confirmLabel="Izbaci"
        onConfirm={handleRemoveHive}
        onCancel={() => setRemoveTarget(null)}
        isLoading={removeHive.isPending}
      />

      {/* Complete-round modal */}
      {completingEntry && (
        <CompleteRoundModal
          entry={completingEntry}
          onConfirm={handleCompleteRound}
          onCancel={() => setCompletingEntry(null)}
          isLoading={entryMutation.isPending}
        />
      )}

      {/* Add-hives modal */}
      {showAddHives && (
        <AddHivesModal
          apiaryId={diet.apiaryId}
          excludedIds={new Set(activeHives.map(b => b.beehiveId))}
          onConfirm={handleAddHives}
          onCancel={() => setShowAddHives(false)}
          isLoading={addHives.isPending}
        />
      )}

      {/* Complete early modal */}
      {showCompleteEarly && (
        <CompleteEarlyModal
          onConfirm={handleCompleteEarly}
          onCancel={() => setShowCompleteEarly(false)}
          isLoading={completeMutation.isPending}
        />
      )}
    </div>
  )
}

function InfoItem({
  icon, label, value,
}: {
  icon: React.ReactNode; label: string; value: string
}) {
  return (
    <div>
      <div className="flex justify-center mb-1 text-honey-500 dark:text-honey-400">{icon}</div>
      <div className="text-xs text-gray-500 dark:text-slate-400 mb-0.5">{label}</div>
      <div className="text-sm font-semibold text-gray-800 dark:text-slate-100">{value}</div>
    </div>
  )
}

/* VitalCard now lives in shared/components (with count-up animation). */
