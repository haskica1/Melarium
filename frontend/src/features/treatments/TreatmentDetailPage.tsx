import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import {
  CheckCircle2, Circle, Pencil, Trash2,
  CalendarDays, Pill, ChevronDown, ChevronUp, StickyNote,
} from 'lucide-react'
import { format, isPast, isToday } from 'date-fns'
import { useTreatment, useDeleteTreatment, useCompleteTreatmentRound } from '../../core/services/treatmentQueries'
import { ErrorMessage, ConfirmDialog, VitalCard, PageSkeleton } from '../../shared/components'
import { useDialogBehavior } from '../../shared/hooks/useDialogBehavior'
import { useFormNavigation } from '../../shared/hooks/useFormNavigation'
import { TreatmentStatus, TreatmentRoundStatus } from '../../core/models'
import type { TreatmentRound } from '../../core/models'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useToast } from '../../core/context/ToastContext'
import { hivesLabel } from '../../shared/utils/plural'

// ── Status badge ──────────────────────────────────────────────────────────────

function StatusBadge({ status, statusName, karencaUntil }: { status: TreatmentStatus; statusName: string; karencaUntil: string }) {
  const styles: Record<TreatmentStatus, string> = {
    [TreatmentStatus.InProgress]: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
    [TreatmentStatus.Karenca]:    'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300',
    [TreatmentStatus.Completed]:  'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
  }
  const text = status === TreatmentStatus.Karenca ? `Karenca do ${format(new Date(karencaUntil), 'dd.MM.yyyy')}` : statusName
  return <span className={`badge ${styles[status]}`}>{text}</span>
}

// ── Round row ────────────────────────────────────────────────────────────────

function RoundRow({
  round, canComplete, onComplete, isCompleting,
}: {
  round: TreatmentRound
  canComplete: boolean
  onComplete: (round: TreatmentRound) => void
  isCompleting: boolean
}) {
  const date = new Date(round.scheduledDate)
  const done = round.status === TreatmentRoundStatus.Completed
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
      {done ? (
        <CheckCircle2 className="w-5 h-5 text-green-500 shrink-0" />
      ) : (
        <button
          onClick={() => canComplete && onComplete(round)}
          disabled={!canComplete || isCompleting}
          className={`shrink-0 transition-colors ${
            canComplete
              ? 'text-gray-300 dark:text-slate-600 hover:text-honey-500 dark:hover:text-honey-400 cursor-pointer'
              : 'text-gray-200 dark:text-slate-700 cursor-not-allowed'
          }`}
          title="Označi primjenu kao obavljenu"
        >
          <Circle className="w-5 h-5" />
        </button>
      )}

      <div className="flex-1 min-w-0">
        <p className={`text-sm font-medium ${done ? 'text-gray-500 dark:text-slate-500 line-through' : 'text-gray-800 dark:text-slate-100'}`}>
          {format(date, 'EEEE, dd MMM yyyy')}
          {today && <span className="ml-2 text-xs font-semibold text-amber-600 dark:text-amber-400">Danas</span>}
          {overdue && <span className="ml-2 text-xs font-semibold text-red-500 dark:text-red-400">Kasni</span>}
        </p>
        {done && round.completionDate && (
          <p className="text-xs text-gray-400 dark:text-slate-500 mt-0.5">
            Obavljeno {format(new Date(round.completionDate), 'dd MMM yyyy, HH:mm')}
          </p>
        )}
        {round.note && (
          <p className="text-xs text-gray-500 dark:text-slate-400 mt-1 flex items-start gap-1.5">
            <StickyNote className="w-3.5 h-3.5 shrink-0 mt-px" />
            <span className="break-words">{round.note}</span>
          </p>
        )}
      </div>

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

// ── Complete-round modal ────────────────────────────────────────────────────

function CompleteRoundModal({
  round, onConfirm, onCancel, isLoading,
}: {
  round: TreatmentRound
  onConfirm: (note: string) => void
  onCancel: () => void
  isLoading: boolean
}) {
  const { panelProps } = useDialogBehavior({ open: true, onClose: onCancel })
  const [note, setNote] = useState('')

  return (
    <div
      {...panelProps}
      aria-label="Označi primjenu kao obavljenu"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4 outline-none"
    >
      <div className="bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl p-6 w-full max-w-md animate-fade-in">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-green-100 dark:bg-green-500/15 flex items-center justify-center shrink-0">
            <CheckCircle2 className="w-5 h-5 text-green-500" />
          </div>
          <div className="min-w-0">
            <h2 className="font-semibold text-gray-800 dark:text-slate-100">Primjena obavljena</h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              Runda od {format(new Date(round.scheduledDate), 'dd.MM.yyyy')} — vrijedi za sve košnice na tretmanu.
            </p>
          </div>
        </div>

        <label className="form-label">Napomena (opcionalno)</label>
        <textarea
          className="form-input resize-none h-20"
          placeholder="npr. traka ispala kod košnice 4"
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

// ── Main page ────────────────────────────────────────────────────────────────

export default function TreatmentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const treatmentId = Number(id)

  const { canEditDelete } = usePermissions()
  const { toast } = useToast()
  const { data: treatment, isLoading, error } = useTreatment(treatmentId)

  // Deleting the treatment makes this page's history entry a dead end — leave via goAfterSave so
  // Back can't return to a treatment that no longer exists.
  const { goAfterSave } = useFormNavigation('/treatments')
  const deleteMutation = useDeleteTreatment()
  const roundMutation  = useCompleteTreatmentRound(treatmentId)

  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false)
  const [completingRound,   setCompletingRound]   = useState<TreatmentRound | null>(null)
  const [showCompleted,     setShowCompleted]     = useState(true)

  if (isLoading) return <PageSkeleton />
  if (error)      return <ErrorMessage message={error.message} />
  if (!treatment) return null

  const pendingRounds   = treatment.rounds.filter(r => r.status === TreatmentRoundStatus.Pending)
  const completedRounds = treatment.rounds.filter(r => r.status === TreatmentRoundStatus.Completed)
  const progressPct = treatment.totalRounds > 0
    ? Math.round((treatment.completedRounds / treatment.totalRounds) * 100)
    : 0

  async function handleDelete() {
    await deleteMutation.mutateAsync(treatmentId)
    goAfterSave('/treatments')
  }

  async function handleCompleteRound(note: string) {
    if (!completingRound) return
    try {
      await roundMutation.mutateAsync({ roundId: completingRound.id, payload: { note: note || null } })
      setCompletingRound(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri označavanju primjene.')
    }
  }

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
                💊
              </div>
              <div className="min-w-0">
                <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50 truncate">{treatment.productName}</h1>
                <div className="mt-1 flex items-center gap-2 flex-wrap">
                  <StatusBadge status={treatment.status} statusName={treatment.statusName} karencaUntil={treatment.karencaUntil} />
                  <span className="text-sm text-gray-600 dark:text-slate-400">
                    {treatment.apiaryName ?? `Pčelinjak #${treatment.apiaryId}`} · {treatment.purposeName}
                  </span>
                </div>
              </div>
            </div>

            <div className="flex gap-2 flex-wrap shrink-0">
              {canEditDelete && (
                <button
                  onClick={() => setShowDeleteConfirm(true)}
                  className="btn-secondary text-sm text-red-500 hover:text-red-600 hover:bg-red-50 border-red-200 dark:text-red-400 dark:hover:text-red-300 dark:hover:bg-red-500/10 dark:border-red-500/30"
                >
                  <Trash2 className="w-4 h-4" /> Obriši
                </button>
              )}
              {canEditDelete && (
                <Link to={`/treatments/${treatmentId}/edit`} className="btn-secondary text-sm"><Pencil className="w-4 h-4" /> Uredi</Link>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* ── Vitals ────────────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger mb-6">
        <VitalCard
          icon="📊" label="Napredak" value={`${progressPct}%`} sub="obavljeno"
          gradient={treatment.status === TreatmentStatus.Completed ? 'from-emerald-400 to-green-600' : 'from-honey-400 to-honey-600'}
        />
        <VitalCard icon="✅" label="Primjene" value={`${treatment.completedRounds}/${treatment.totalRounds}`} sub="obavljeno" gradient="from-amber-400 to-orange-500" />
        <VitalCard icon="🐝" label="Košnice" value={String(treatment.hiveCount)} sub="tretirano" gradient="from-emerald-400 to-teal-600" />
        <VitalCard icon="⏳" label="Karenca" value={treatment.withdrawalDays > 0 ? `${treatment.withdrawalDays}d` : '—'} sub="povlačenje" gradient="from-violet-400 to-indigo-600" />
      </div>

      {/* ── Summary card ──────────────────────────────────────────────────────── */}
      <div className="card mb-6">
        <div className="h-2 bg-gray-100 dark:bg-slate-700 rounded-full overflow-hidden mb-4">
          <div
            className={`h-full rounded-full transition-all duration-500 ${
              treatment.status === TreatmentStatus.Completed ? 'bg-green-400' : 'bg-honey-400'
            }`}
            style={{ width: `${progressPct}%` }}
          />
        </div>

        <div className="grid grid-cols-2 gap-4 text-center text-sm">
          <InfoItem icon={<CalendarDays className="w-4 h-4" />} label="Datum početka"
            value={format(new Date(treatment.startDate), 'dd MMM yyyy')} />
          <InfoItem icon={<Pill className="w-4 h-4" />} label="Doza"
            value={`${treatment.dosePerHive} · ${treatment.activeSubstanceName}`} />
        </div>

        {treatment.hiveNames.length > 0 && (
          <p className="mt-4 pt-4 border-t border-honey-100 dark:border-slate-800 text-sm text-gray-600 dark:text-slate-300">
            {hivesLabel(treatment.hiveCount)}: {treatment.hiveNames.join(', ')}
          </p>
        )}
        {(treatment.batchNumber || treatment.supplier || treatment.notes) && (
          <div className="mt-3 pt-3 border-t border-honey-100 dark:border-slate-800 text-xs text-gray-500 dark:text-slate-400 space-y-1">
            {treatment.batchNumber && <p>LOT: {treatment.batchNumber}</p>}
            {treatment.supplier && <p>Dobavljač: {treatment.supplier}</p>}
            {treatment.notes && <p>{treatment.notes}</p>}
          </div>
        )}
        {treatment.createdByName && (
          <p className="mt-3 pt-3 border-t border-honey-100 dark:border-slate-800 text-xs text-gray-500 dark:text-slate-400 flex items-center gap-1.5">
            👤 Kreirao {treatment.createdByName}
          </p>
        )}
      </div>

      {/* Pending rounds */}
      <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 mb-3">
        Predstojeće primjene
        {pendingRounds.length > 0 && (
          <span className="ml-2 text-sm font-normal text-gray-400 dark:text-slate-500">({pendingRounds.length})</span>
        )}
      </h2>

      {pendingRounds.length === 0 ? (
        <div className="card text-center text-gray-400 dark:text-slate-500 py-8 mb-6">
          <CheckCircle2 className="w-10 h-10 mx-auto mb-2 text-green-300" />
          <p className="text-sm">Sve primjene su evidentirane.</p>
        </div>
      ) : (
        <div className="space-y-2 mb-6">
          {pendingRounds.map(round => (
            <RoundRow
              key={round.id}
              round={round}
              canComplete
              onComplete={setCompletingRound}
              isCompleting={roundMutation.isPending}
            />
          ))}
        </div>
      )}

      {/* Completed rounds (collapsible) */}
      {completedRounds.length > 0 && (
        <div className="mb-8">
          <button
            onClick={() => setShowCompleted(v => !v)}
            className="flex items-center gap-2 text-sm font-semibold text-gray-600 dark:text-slate-300 hover:text-gray-800 dark:hover:text-slate-100 mb-3 transition-colors"
          >
            {showCompleted ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
            Obavljene primjene ({completedRounds.length})
          </button>

          {showCompleted && (
            <div className="space-y-2 opacity-80">
              {completedRounds.map(round => (
                <RoundRow
                  key={round.id}
                  round={round}
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
        title="Obriši tretman"
        message="Da li ste sigurni da želite obrisati ovaj tretman? Zakonska obaveza je čuvanje evidencije 5 godina — brišite samo greške."
        confirmLabel="Obriši"
        onConfirm={handleDelete}
        onCancel={() => setShowDeleteConfirm(false)}
        isLoading={deleteMutation.isPending}
      />

      {/* Complete-round modal */}
      {completingRound && (
        <CompleteRoundModal
          round={completingRound}
          onConfirm={handleCompleteRound}
          onCancel={() => setCompletingRound(null)}
          isLoading={roundMutation.isPending}
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
