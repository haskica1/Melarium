import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { format } from 'date-fns'
import { Eye, EyeOff, Loader2, PencilLine, Plus, Trash2 } from 'lucide-react'
import {
  useAdminLearningTopics,
  useDeleteLearningTopic,
  useSetTopicPublished,
} from '../../core/services/learningQueries'
import { MonthLabels } from '../../core/models'
import type { AdminLearningTopic } from '../../core/models'
import { ConfirmDialog, EmptyState, VitalsSkeleton } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'

export default function LearningTopicsAdminPage() {
  const navigate = useNavigate()
  const { toast } = useToast()

  const { data: topics = [], isLoading } = useAdminLearningTopics()
  const setPublished = useSetTopicPublished()
  const deleteTopic = useDeleteLearningTopic()

  const [confirmTarget, setConfirmTarget] = useState<AdminLearningTopic | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const [togglingId, setTogglingId] = useState<number | null>(null)

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
              </p>
            </div>
          </div>
          <button onClick={() => navigate('/admin/learning-topics/new')} className="btn-primary text-sm shrink-0">
            <Plus className="w-4 h-4" /> Nova tema
          </button>
        </div>
      </div>

      {isLoading && <VitalsSkeleton />}

      {!isLoading && topics.length === 0 && (
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

      {!isLoading && topics.length > 0 && (
        <div className="space-y-3">
          {topics.map(t => (
            <div key={t.id} className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4 flex items-center gap-4">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="font-semibold text-gray-900 dark:text-slate-100">{t.title}</span>
                  <span className="text-xs text-honey-700 dark:text-honey-300 bg-honey-100 dark:bg-honey-500/15 rounded-full px-2 py-0.5">{t.categoryName}</span>
                  <span className={`text-xs rounded-full px-2 py-0.5 ${
                    t.isPublished
                      ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300'
                      : 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300'
                  }`}>
                    {t.isPublished ? 'Objavljeno' : 'Skica'}
                  </span>
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
