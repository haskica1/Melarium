import { useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import {
  CheckCircle2, Circle, Pencil, Trash2, Copy,
  CalendarDays, Utensils, AlertTriangle, ChevronDown, ChevronUp,
} from 'lucide-react'
import { format, isPast, isToday } from 'date-fns'
import {
  useDiet,
  useDeleteDiet,
  useCompleteEarlyDiet,
  useCompleteFeedingEntry,
} from '../../core/services/queries'
import {
  ErrorMessage, ConfirmDialog, VitalCard, PageSkeleton,
} from '../../shared/components'
import { DietStatus, FeedingEntryStatus } from '../../core/models'
import type { FeedingEntry } from '../../core/models'
import { usePermissions } from '../../core/hooks/usePermissions'
import CopyDietDialog from './CopyDietDialog'

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

// ── Feeding entry row ─────────────────────────────────────────────────────────

function EntryRow({
  entry,
  canComplete,
  onComplete,
  isCompleting,
}: {
  entry: FeedingEntry
  canComplete: boolean
  onComplete: (id: number) => void
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
          onClick={() => canComplete && onComplete(entry.id)}
          disabled={!canComplete || isCompleting}
          className={`shrink-0 transition-colors ${
            canComplete
              ? 'text-gray-300 dark:text-slate-600 hover:text-honey-500 dark:hover:text-honey-400 cursor-pointer'
              : 'text-gray-200 dark:text-slate-700 cursor-not-allowed'
          }`}
          title={canComplete ? 'Označi kao završeno' : 'Prehrana je završena'}
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
            Završeno {format(new Date(entry.completionDate), 'dd MMM yyyy, HH:mm')}
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
        {done ? 'Završeno' : overdue ? 'Kasni' : today ? 'Danas' : 'Na čekanju'}
      </span>
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
  const [comment, setComment] = useState('')
  const [err, setErr] = useState('')

  function submit() {
    if (!comment.trim()) { setErr('Molimo unesite razlog.'); return }
    onConfirm(comment.trim())
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
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
          <button onClick={onCancel} className="btn-secondary flex-1" disabled={isLoading}>
            Otkaži
          </button>
          <button onClick={submit} className="btn-primary flex-1 bg-red-500 hover:bg-red-600" disabled={isLoading}>
            {isLoading ? 'Zaustavljam…' : 'Zaustavi prehranu'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function DietDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const dietId = Number(id)

  const { canEditDelete, isAssignedToHive } = usePermissions()
  const { data: diet, isLoading, error } = useDiet(dietId)
  const deleteMutation   = useDeleteDiet(diet?.beehiveId ?? 0)
  const completeMutation = useCompleteEarlyDiet(dietId, diet?.beehiveId ?? 0)
  const entryMutation    = useCompleteFeedingEntry(dietId, diet?.beehiveId ?? 0)

  const [showDeleteConfirm,  setShowDeleteConfirm]  = useState(false)
  const [showCompleteEarly,  setShowCompleteEarly]   = useState(false)
  const [showCopy,           setShowCopy]            = useState(false)
  const [showCompleted,      setShowCompleted]       = useState(true)

  if (isLoading) return <PageSkeleton />
  if (error)     return <ErrorMessage message={error.message} />
  if (!diet)     return null

  const isFinished  = diet.status === DietStatus.Completed || diet.status === DietStatus.StoppedEarly
  const canManage   = canEditDelete || isAssignedToHive(diet.beehiveId)
  const canDelete   = canManage && diet.status === DietStatus.NotStarted
  const canEdit     = canManage && !isFinished

  const pendingEntries   = diet.feedingEntries.filter(e => e.status === FeedingEntryStatus.Pending)
  const completedEntries = diet.feedingEntries.filter(e => e.status === FeedingEntryStatus.Completed)

  async function handleDelete() {
    await deleteMutation.mutateAsync(dietId)
    if(diet == null) return;
    navigate(`/beehives/${diet.beehiveId}`)
  }

  async function handleCompleteEarly(comment: string) {
    await completeMutation.mutateAsync({ comment })
    setShowCompleteEarly(false)
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
                  <span className="text-sm text-gray-600 dark:text-slate-400">{diet.foodTypeName} · {diet.reasonName}</span>
                </div>
              </div>
            </div>

            <div className="flex gap-2 flex-wrap shrink-0">
              {canManage && (
                <button onClick={() => setShowCopy(true)} className="btn-secondary text-sm">
                  <Copy className="w-4 h-4" /> Kopiraj
                </button>
              )}
              {canDelete && (
                <button
                  onClick={() => setShowDeleteConfirm(true)}
                  className="btn-secondary text-sm text-red-500 hover:text-red-600 hover:bg-red-50 border-red-200 dark:text-red-400 dark:hover:text-red-300 dark:hover:bg-red-500/10 dark:border-red-500/30"
                >
                  <Trash2 className="w-4 h-4" /> Obriši
                </button>
              )}
              {canManage && !isFinished && (
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
          icon="📊" label="Napredak" value={`${progressPct}%`} sub="završeno"
          gradient={
            diet.status === DietStatus.StoppedEarly ? 'from-red-400 to-rose-600'
            : diet.status === DietStatus.Completed ? 'from-emerald-400 to-green-600'
            : 'from-honey-400 to-honey-600'
          }
        />
        <VitalCard icon="✅" label="Hranjenja"  value={`${diet.completedEntries}/${diet.totalEntries}`} sub="završeno" gradient="from-amber-400 to-orange-500" />
        <VitalCard icon="📅" label="Trajanje"  value={String(diet.durationDays)} sub="dana ukupno" gradient="from-sky-400 to-blue-600" />
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
            value={diet.foodType === 5 && diet.customFoodType ? diet.customFoodType : diet.foodTypeName} />
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

      {/* Pending entries */}
      <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 mb-3">
        Predstojeća hranjenja
        {pendingEntries.length > 0 && (
          <span className="ml-2 text-sm font-normal text-gray-400 dark:text-slate-500">({pendingEntries.length})</span>
        )}
      </h2>

      {pendingEntries.length === 0 ? (
        <div className="card text-center text-gray-400 dark:text-slate-500 py-8 mb-6">
          <CheckCircle2 className="w-10 h-10 mx-auto mb-2 text-green-300" />
          <p className="text-sm">Sva hranjenja su evidentirana.</p>
        </div>
      ) : (
        <div className="space-y-2 mb-6">
          {pendingEntries.map(entry => (
            <EntryRow
              key={entry.id}
              entry={entry}
              canComplete={!isFinished}
              onComplete={id => entryMutation.mutate(id)}
              isCompleting={entryMutation.isPending}
            />
          ))}
        </div>
      )}

      {/* Completed entries (collapsible) */}
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
            Završena hranjenja ({completedEntries.length})
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
        message="Da li ste sigurni da želite obrisati ovu prehranu? Ova radnja ne može biti poništena."
        onConfirm={handleDelete}
        onCancel={() => setShowDeleteConfirm(false)}
        isLoading={deleteMutation.isPending}
      />

      {/* Complete early modal */}
      {showCompleteEarly && (
        <CompleteEarlyModal
          onConfirm={handleCompleteEarly}
          onCancel={() => setShowCompleteEarly(false)}
          isLoading={completeMutation.isPending}
        />
      )}

      {/* Copy-to-other-hives dialog */}
      {showCopy && (
        <CopyDietDialog diet={diet} onClose={() => setShowCopy(false)} />
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
