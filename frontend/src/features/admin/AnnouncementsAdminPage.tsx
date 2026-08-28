import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { format } from 'date-fns'
import { Eye, EyeOff, Loader2, PencilLine, Plus, Trash2 } from 'lucide-react'
import {
  useAdminAnnouncements,
  useDeleteAnnouncement,
  useSetAnnouncementPublished,
} from '../../core/services/announcementQueries'
import type { AdminAnnouncement } from '../../core/models'
import { ConfirmDialog, EmptyState, ErrorState, VitalsSkeleton } from '../../shared/components'
import { ANNOUNCEMENT_TYPE_CLASS } from '../../shared/components/announcementType'
import { useToast } from '../../core/context/ToastContext'

export default function AnnouncementsAdminPage() {
  const navigate = useNavigate()
  const { toast } = useToast()

  const { data: announcements = [], isLoading, isError, refetch } = useAdminAnnouncements()
  const setPublished = useSetAnnouncementPublished()
  const deleteAnnouncement = useDeleteAnnouncement()

  const [confirmTarget, setConfirmTarget] = useState<AdminAnnouncement | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const [togglingId, setTogglingId] = useState<number | null>(null)

  async function handleTogglePublish(a: AdminAnnouncement) {
    setTogglingId(a.id)
    try {
      const updated = await setPublished.mutateAsync({ id: a.id, isPublished: !a.isPublished })
      toast.success(updated.isPublished
        ? `Objava "${updated.title}" je objavljena.`
        : `Objava "${updated.title}" je sklonjena.`)
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
      await deleteAnnouncement.mutateAsync(confirmTarget.id)
      toast.success(`Objava "${confirmTarget.title}" obrisana.`)
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri brisanju objave.')
    } finally {
      setIsDeleting(false)
    }
  }

  const published = announcements.filter(a => a.isPublished).length

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
              ✨
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Šta je novo — objave</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
                Objavljeno {published} od {announcements.length}. Banner uvijek pokazuje samo zadnju objavu.
              </p>
            </div>
          </div>
          <button onClick={() => navigate('/admin/announcements/new')} className="btn-primary text-sm shrink-0">
            <Plus className="w-4 h-4" /> Nova objava
          </button>
        </div>
      </div>

      {isLoading && <VitalsSkeleton />}

      {isError && <ErrorState message="Greška pri učitavanju objava." onRetry={refetch} />}

      {!isLoading && !isError && announcements.length === 0 && (
        <EmptyState
          title="Još nema objava."
          description="Napišite prvu objavu o novoj funkcionalnosti."
          action={
            <button onClick={() => navigate('/admin/announcements/new')} className="btn-primary text-sm">
              <Plus className="w-4 h-4" /> Nova objava
            </button>
          }
        />
      )}

      {!isLoading && announcements.length > 0 && (
        <div className="space-y-3">
          {announcements.map(a => (
            <div key={a.id} className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4 flex items-center gap-4">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="font-semibold text-gray-900 dark:text-slate-100">{a.title}</span>
                  <span className={`text-xs font-medium rounded-full px-2 py-0.5 ${ANNOUNCEMENT_TYPE_CLASS[a.type]}`}>
                    {a.typeName}
                  </span>
                  <span className={`text-xs rounded-full px-2 py-0.5 ${
                    a.isPublished
                      ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300'
                      : 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300'
                  }`}>
                    {a.isPublished ? 'Objavljeno' : 'Skica'}
                  </span>
                </div>
                <div className="flex items-center gap-3 mt-0.5 text-sm text-gray-500 dark:text-slate-400">
                  <span>{format(new Date(a.createdAt), 'dd.MM.yyyy')}</span>
                  {a.publishedAt && (
                    <>
                      <span>·</span>
                      <span>objavljeno {format(new Date(a.publishedAt), 'dd.MM.yyyy')}</span>
                    </>
                  )}
                </div>
              </div>
              <div className="flex items-center gap-1 shrink-0">
                <button
                  onClick={() => handleTogglePublish(a)}
                  disabled={togglingId === a.id}
                  className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-emerald-600 dark:hover:text-emerald-400 hover:bg-emerald-50 dark:hover:bg-emerald-500/10 transition-colors disabled:opacity-50"
                  aria-label={a.isPublished ? 'Skloni s objave' : 'Objavi'}
                  title={a.isPublished ? 'Skloni s objave' : 'Objavi'}
                >
                  {togglingId === a.id
                    ? <Loader2 className="w-4 h-4 animate-spin" />
                    : a.isPublished ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
                <button
                  onClick={() => navigate(`/admin/announcements/${a.id}/edit`)}
                  className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                  aria-label="Uredi objavu"
                >
                  <PencilLine className="w-4 h-4" />
                </button>
                <button
                  onClick={() => setConfirmTarget(a)}
                  disabled={confirmTarget?.id === a.id && isDeleting}
                  className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-50"
                  aria-label="Obriši objavu"
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
        title="Obriši objavu"
        message={confirmTarget ? `Obrisati objavu "${confirmTarget.title}"? Briše se i evidencija ko ju je vidio.` : ''}
        confirmLabel="Obriši"
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}
