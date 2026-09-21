import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { format } from 'date-fns'
import { Check, Eye, EyeOff, Loader2, PencilLine, Plus, Trash2, UserRound, X } from 'lucide-react'
import {
  useAdminLearningTopics,
  useApproveTopic,
  useDeleteLearningTopic,
  useRejectTopic,
  useSetTopicPublished,
} from '../../core/services/learningQueries'
import { MonthLabels, TopicReviewStatus } from '../../core/models'
import type { AdminLearningTopic } from '../../core/models'
import { ConfirmDialog, EmptyState, ErrorState, Modal, VitalsSkeleton } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'

/** Mirrors the backend validator, so the button says no before the request does. */
const MIN_REASON_LENGTH = 10

export default function LearningTopicsAdminPage() {
  const navigate = useNavigate()
  const { toast } = useToast()

  const { data: topics = [], isLoading, isError, refetch } = useAdminLearningTopics()
  const setPublished = useSetTopicPublished()
  const deleteTopic = useDeleteLearningTopic()
  const approveTopic = useApproveTopic()
  const rejectTopic = useRejectTopic()

  const [confirmTarget, setConfirmTarget] = useState<AdminLearningTopic | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const [togglingId, setTogglingId] = useState<number | null>(null)
  const [rejectTarget, setRejectTarget] = useState<AdminLearningTopic | null>(null)
  const [rejectReason, setRejectReason] = useState('')
  const [reviewingId, setReviewingId] = useState<number | null>(null)

  async function handleApprove(topic: AdminLearningTopic) {
    setReviewingId(topic.id)
    try {
      await approveTopic.mutateAsync(topic.id)
      toast.success(`Tema "${topic.title}" je odobrena i objavljena.`)
    } catch (e: any) {
      toast.error(
        e?.response?.data?.errors?.bodyMarkdown?.[0] ??
        e?.response?.data?.detail ??
        'Greška pri odobravanju teme.')
    } finally {
      setReviewingId(null)
    }
  }

  async function handleReject() {
    if (!rejectTarget) return
    setReviewingId(rejectTarget.id)
    try {
      await rejectTopic.mutateAsync({ id: rejectTarget.id, reason: rejectReason.trim() })
      toast.success(`Tema "${rejectTarget.title}" je odbijena — autor je obaviješten.`)
      setRejectTarget(null)
      setRejectReason('')
    } catch (e: any) {
      toast.error(
        e?.response?.data?.errors?.reason?.[0] ??
        e?.response?.data?.detail ??
        'Greška pri odbijanju teme.')
    } finally {
      setReviewingId(null)
    }
  }

  async function handleTogglePublish(topic: AdminLearningTopic) {
    setTogglingId(topic.id)
    try {
      const updated = await setPublished.mutateAsync({ id: topic.id, isPublished: !topic.isPublished })
      toast.success(updated.isPublished ? `Tema "${updated.title}" je objavljena.` : `Tema "${updated.title}" je sklonjena s objave.`)
    } catch (e: any) {
      toast.error(e?.response?.data?.errors?.bodyMarkdown?.[0] ?? e?.response?.data?.detail ?? 'Greška pri promjeni statusa objave.')
    } finally {
      setTogglingId(null)
    }
  }

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await deleteTopic.mutateAsync(confirmTarget.id)
      toast.success(`Tema "${confirmTarget.title}" obrisana.`)
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri brisanju teme.')
    } finally {
      setIsDeleting(false)
    }
  }

  const published = topics.filter(t => t.isPublished).length
  const pending = topics.filter(t => t.reviewStatus === TopicReviewStatus.Pending)
  const rest = topics.filter(t => t.reviewStatus !== TopicReviewStatus.Pending)

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
              🎓
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Edukacija — teme</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
                Objavljeno {published} od {topics.length} tema. Prva objava obavještava sve korisnike.
                {pending.length > 0 && ` ${pending.length} čeka odobrenje.`}
              </p>
            </div>
          </div>
          <button onClick={() => navigate('/admin/learning-topics/new')} className="btn-primary text-sm shrink-0">
            <Plus className="w-4 h-4" /> Nova tema
          </button>
        </div>
      </div>

      {isLoading && <VitalsSkeleton />}

      {isError && <ErrorState message="Greška pri učitavanju tema." onRetry={refetch} />}

      {!isLoading && !isError && topics.length === 0 && (
        <EmptyState
          title="Još nema tema."
          description="Kreirajte prvu edukativnu temu — možete krenuti od AI nacrta."
          action={
            <button onClick={() => navigate('/admin/learning-topics/new')} className="btn-primary text-sm">
              <Plus className="w-4 h-4" /> Nova tema
            </button>
          }
        />
      )}

      {!isLoading && pending.length > 0 && (
        <section className="space-y-3">
          <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 px-1">
            Čeka odobrenje
            <span className="ml-2 text-sm font-normal text-gray-500 dark:text-slate-400">{pending.length}</span>
          </h2>
          {pending.map(t => (
            <div key={t.id} className="bg-white dark:bg-slate-900 rounded-2xl border border-amber-200 dark:border-amber-500/30 shadow-sm dark:shadow-none px-5 py-4">
              <div className="flex items-start gap-4 flex-wrap">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-semibold text-gray-900 dark:text-slate-100">{t.title}</span>
                    <span className="text-xs text-honey-700 dark:text-honey-300 bg-honey-100 dark:bg-honey-500/15 rounded-full px-2 py-0.5">{t.categoryName}</span>
                    <span className="text-xs rounded-full px-2 py-0.5 bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300">
                      {t.reviewStatusName}
                    </span>
                  </div>
                  <p className="mt-1 text-sm text-gray-500 dark:text-slate-400 line-clamp-2 break-words">{t.summary}</p>
                  <div className="flex items-center gap-3 mt-1 text-xs text-gray-400 dark:text-slate-500">
                    <span className="inline-flex items-center gap-1">
                      <UserRound className="w-3.5 h-3.5" />
                      {t.authorName ?? 'nepoznat autor'}
                    </span>
                    {t.submittedAt && <span>· poslano {format(new Date(t.submittedAt), 'dd.MM.yyyy')}</span>}
                  </div>
                </div>

                <div className="flex items-center gap-2 shrink-0">
                  {/* Reading the whole article before deciding happens in the existing form. */}
                  <button
                    onClick={() => navigate(`/admin/learning-topics/${t.id}/edit`)}
                    className="flex items-center gap-1.5 px-3 py-2 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
                  >
                    <Eye className="w-4 h-4" /> Pročitaj
                  </button>
                  <button
                    onClick={() => setRejectTarget(t)}
                    disabled={reviewingId === t.id}
                    className="flex items-center gap-1.5 px-3 py-2 rounded-xl border border-red-200 dark:border-red-500/30 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-50"
                  >
                    <X className="w-4 h-4" /> Odbij
                  </button>
                  <button
                    onClick={() => handleApprove(t)}
                    disabled={reviewingId === t.id}
                    className="flex items-center gap-1.5 px-3 py-2 rounded-xl bg-emerald-500 hover:bg-emerald-600 text-white text-sm font-semibold transition-colors disabled:opacity-60"
                  >
                    {reviewingId === t.id ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
                    Odobri
                  </button>
                </div>
              </div>
            </div>
          ))}
        </section>
      )}

      {!isLoading && rest.length > 0 && (
        <div className="space-y-3">
          {pending.length > 0 && (
            <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 px-1 pt-2">
              Sve teme
            </h2>
          )}
          {rest.map(t => (
            <div key={t.id} className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4 flex items-center gap-4">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="font-semibold text-gray-900 dark:text-slate-100">{t.title}</span>
                  <span className="text-xs text-honey-700 dark:text-honey-300 bg-honey-100 dark:bg-honey-500/15 rounded-full px-2 py-0.5">{t.categoryName}</span>
                  {/* "Skica" means the admin has not finished writing it; a rejected proposal is
                      also unpublished but for a different reason, so it says that instead. */}
                  {t.reviewStatus === TopicReviewStatus.Rejected ? (
                    <span className="text-xs rounded-full px-2 py-0.5 bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300">
                      {t.reviewStatusName}
                    </span>
                  ) : (
                    <span className={`text-xs rounded-full px-2 py-0.5 ${
                      t.isPublished
                        ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300'
                        : 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300'
                    }`}>
                      {t.isPublished ? 'Objavljeno' : 'Skica'}
                    </span>
                  )}
                  {t.authorName && (
                    <span className="inline-flex items-center gap-1 text-xs text-gray-400 dark:text-slate-500">
                      <UserRound className="w-3.5 h-3.5" /> {t.authorName}
                    </span>
                  )}
                </div>
                <div className="flex items-center gap-3 mt-0.5 text-sm text-gray-500 dark:text-slate-400">
                  <span>{format(new Date(t.createdAt), 'dd.MM.yyyy')}</span>
                  {t.months && t.months.length > 0 && (
                    <>
                      <span>·</span>
                      <span>{t.months.map(m => MonthLabels[m - 1].slice(0, 3).toLowerCase()).join(', ')}</span>
                    </>
                  )}
                </div>
              </div>
              <div className="flex items-center gap-1 shrink-0">
                <button
                  onClick={() => handleTogglePublish(t)}
                  disabled={togglingId === t.id}
                  className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-emerald-600 dark:hover:text-emerald-400 hover:bg-emerald-50 dark:hover:bg-emerald-500/10 transition-colors disabled:opacity-50"
                  aria-label={t.isPublished ? 'Skloni s objave' : 'Objavi temu'}
                  title={t.isPublished ? 'Skloni s objave' : 'Objavi temu'}
                >
                  {togglingId === t.id
                    ? <Loader2 className="w-4 h-4 animate-spin" />
                    : t.isPublished ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
                <button
                  onClick={() => navigate(`/admin/learning-topics/${t.id}/edit`)}
                  className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                  aria-label="Uredi temu"
                >
                  <PencilLine className="w-4 h-4" />
                </button>
                <button
                  onClick={() => setConfirmTarget(t)}
                  disabled={confirmTarget?.id === t.id && isDeleting}
                  className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-50"
                  aria-label="Obriši temu"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      <Modal
        open={!!rejectTarget}
        onClose={() => { setRejectTarget(null); setRejectReason('') }}
        title="Odbij temu"
        description={rejectTarget ? `"${rejectTarget.title}" — autor dobija razlog i može temu doraditi i poslati ponovo.` : undefined}
        closeOnBackdropClick={false}
        footer={
          <div className="flex gap-3">
            <button
              onClick={() => { setRejectTarget(null); setRejectReason('') }}
              className="flex-1 px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
            >
              Otkaži
            </button>
            <button
              onClick={handleReject}
              disabled={rejectReason.trim().length < MIN_REASON_LENGTH || reviewingId === rejectTarget?.id}
              className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-red-500 hover:bg-red-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors"
            >
              {reviewingId === rejectTarget?.id && <Loader2 className="w-4 h-4 animate-spin" />}
              Odbij temu
            </button>
          </div>
        }
      >
        <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
          Razlog odbijanja <span className="text-red-500">*</span>
        </label>
        <textarea
          rows={4}
          maxLength={500}
          value={rejectReason}
          onChange={e => setRejectReason(e.target.value)}
          placeholder="npr. Tekst ponavlja postojeću temu o varoi — dodajte konkretna iskustva s tretmanima."
          className="w-full px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
        />
        <p className="text-xs text-gray-400 dark:text-slate-500 mt-1 text-right">
          {rejectReason.trim().length < MIN_REASON_LENGTH
            ? `Najmanje ${MIN_REASON_LENGTH} znakova`
            : `${rejectReason.length}/500`}
        </p>
      </Modal>

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši temu"
        message={confirmTarget ? `Obrisati temu "${confirmTarget.title}"? Briše se i evidencija pročitanosti.` : ''}
        confirmLabel="Obriši"
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}
