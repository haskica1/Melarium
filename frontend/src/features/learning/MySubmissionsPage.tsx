import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { format } from 'date-fns'
import { AlertCircle, ArrowLeft, CheckCircle2, Clock, PencilLine, Plus, Trash2 } from 'lucide-react'
import { useMySubmissions, useWithdrawMySubmission } from '../../core/services/learningQueries'
import { TopicReviewStatus } from '../../core/models'
import type { MyLearningSubmission } from '../../core/models'
import { ConfirmDialog, EmptyState, ErrorState, VitalsSkeleton } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'

export default function MySubmissionsPage() {
  const navigate = useNavigate()
  const { toast } = useToast()

  const { data: submissions = [], isLoading, isError, isSuccess, refetch } = useMySubmissions()
  const withdraw = useWithdrawMySubmission()

  const [confirmTarget, setConfirmTarget] = useState<MyLearningSubmission | null>(null)
  const [isWithdrawing, setIsWithdrawing] = useState(false)

  async function handleConfirmWithdraw() {
    if (!confirmTarget) return
    setIsWithdrawing(true)
    try {
      await withdraw.mutateAsync(confirmTarget.id)
      toast.success(`Prijedlog "${confirmTarget.title}" je povučen.`)
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri povlačenju prijedloga.')
    } finally {
      setIsWithdrawing(false)
    }
  }

  const pending = submissions.filter(s => s.reviewStatus === TopicReviewStatus.Pending).length
  const approved = submissions.filter(s => s.reviewStatus === TopicReviewStatus.Approved).length

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
              ✍️
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Moje teme</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
                {submissions.length === 0
                  ? 'Teme koje pošaljete na odobrenje pojavit će se ovdje.'
                  : `${pending} čeka odobrenje · ${approved} objavljeno.`}
              </p>
            </div>
          </div>
          <button onClick={() => navigate('/learning/predlozi')} className="btn-primary text-sm shrink-0">
            <Plus className="w-4 h-4" /> Predloži temu
          </button>
        </div>
      </div>

      <Link
        to="/learning"
        className="inline-flex items-center gap-1.5 text-sm font-medium text-gray-500 dark:text-slate-400 hover:text-honey-600 dark:hover:text-honey-400 transition-colors"
      >
        <ArrowLeft className="w-4 h-4" /> Nazad na Edukaciju
      </Link>

      {isLoading && <VitalsSkeleton />}

      {isError && <ErrorState message="Greška pri učitavanju vaših tema." onRetry={refetch} />}

      {isSuccess && submissions.length === 0 && (
        <EmptyState
          title="Još niste poslali nijednu temu."
          description="Podijelite svoje iskustvo s ostalim pčelarima — administrator pregleda temu prije objave."
          action={
            <button onClick={() => navigate('/learning/predlozi')} className="btn-primary text-sm">
              <Plus className="w-4 h-4" /> Predloži temu
            </button>
          }
        />
      )}

      {submissions.length > 0 && (
        <div className="space-y-3">
          {submissions.map(s => (
            <div key={s.id} className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4">
              <div className="flex items-start gap-4">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    {/* An approved topic is live in Edukacija — its title is the way in. */}
                    {s.isPublished ? (
                      <Link to={`/learning/${s.id}`} className="font-semibold text-gray-900 dark:text-slate-100 hover:text-honey-600 dark:hover:text-honey-400 transition-colors">
                        {s.title}
                      </Link>
                    ) : (
                      <span className="font-semibold text-gray-900 dark:text-slate-100">{s.title}</span>
                    )}
                    <span className="text-xs text-honey-700 dark:text-honey-300 bg-honey-100 dark:bg-honey-500/15 rounded-full px-2 py-0.5">
                      {s.categoryName}
                    </span>
                    <StatusChip status={s.reviewStatus} label={s.reviewStatusName} />
                  </div>
                  <p className="mt-1 text-sm text-gray-500 dark:text-slate-400 line-clamp-2 break-words">{s.summary}</p>
                  {s.submittedAt && (
                    <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">
                      Poslano {format(new Date(s.submittedAt), 'dd.MM.yyyy')}
                    </p>
                  )}
                </div>

                {s.canEdit && (
                  <div className="flex items-center gap-1 shrink-0">
                    <button
                      onClick={() => navigate(`/learning/moje-teme/${s.id}/uredi`)}
                      className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                      aria-label="Uredi temu"
                      title="Uredi temu"
                    >
                      <PencilLine className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => setConfirmTarget(s)}
                      className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                      aria-label="Povuci prijedlog"
                      title="Povuci prijedlog"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                )}
              </div>

              {s.reviewStatus === TopicReviewStatus.Rejected && s.rejectionReason && (
                <div className="mt-3 flex items-start gap-2 bg-amber-50 dark:bg-amber-500/10 border border-amber-200 dark:border-amber-500/30 text-amber-800 dark:text-amber-300 rounded-xl px-3.5 py-2.5 text-sm">
                  <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
                  <span>
                    <strong>Razlog:</strong> {s.rejectionReason}{' '}
                    <button
                      onClick={() => navigate(`/learning/moje-teme/${s.id}/uredi`)}
                      className="underline font-medium hover:no-underline"
                    >
                      Doradi i pošalji ponovo
                    </button>
                  </span>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Povuci prijedlog"
        message={confirmTarget ? `Povući temu "${confirmTarget.title}"? Tekst se briše i ne može se vratiti.` : ''}
        confirmLabel="Povuci"
        onConfirm={handleConfirmWithdraw}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isWithdrawing}
      />
    </div>
  )
}

function StatusChip({ status, label }: { status: TopicReviewStatus; label: string }) {
  if (status === TopicReviewStatus.None) return null

  const style =
    status === TopicReviewStatus.Approved ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300'
    : status === TopicReviewStatus.Rejected ? 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300'
    : 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300'

  const Icon = status === TopicReviewStatus.Approved ? CheckCircle2
    : status === TopicReviewStatus.Rejected ? AlertCircle
    : Clock

  return (
    <span className={`inline-flex items-center gap-1 text-xs rounded-full px-2 py-0.5 ${style}`}>
      <Icon className="w-3 h-3" /> {label}
    </span>
  )
}
