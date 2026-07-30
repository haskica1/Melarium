import { useEffect, useState } from 'react'
import { format } from 'date-fns'
import { Image as ImageIcon, Inbox, Loader2, Trash2 } from 'lucide-react'
import {
  useAdminFeedback,
  useDeleteFeedback,
  useUpdateFeedbackStatus,
} from '../../core/services/feedbackQueries'
import { feedbackService } from '../../core/services/feedbackService'
import {
  FeedbackStatus,
  FeedbackStatusLabels,
  FeedbackType,
  FeedbackTypeEmojis,
  FeedbackTypeLabels,
} from '../../core/models'
import type { AdminFeedback } from '../../core/models'
import { ConfirmDialog, EmptyState, ErrorState, Modal, VitalsSkeleton } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'

const STATUS_STYLE: Record<FeedbackStatus, string> = {
  [FeedbackStatus.New]:       'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300',
  [FeedbackStatus.InReview]:  'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  [FeedbackStatus.Resolved]:  'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  [FeedbackStatus.Dismissed]: 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
}

const STATUS_ORDER: FeedbackStatus[] = [
  FeedbackStatus.New,
  FeedbackStatus.InReview,
  FeedbackStatus.Resolved,
  FeedbackStatus.Dismissed,
]

const TYPE_ORDER: FeedbackType[] = [
  FeedbackType.Bug,
  FeedbackType.Complaint,
  FeedbackType.Compliment,
  FeedbackType.FeatureRequest,
  FeedbackType.Question,
  FeedbackType.Other,
]

export default function FeedbackAdminPage() {
  const { toast } = useToast()

  const [typeFilter, setTypeFilter] = useState<FeedbackType | ''>('')
  const [statusFilter, setStatusFilter] = useState<FeedbackStatus | ''>('')

  const filters = {
    ...(typeFilter !== '' && { type: typeFilter }),
    ...(statusFilter !== '' && { status: statusFilter }),
  }
  // isPending/isSuccess rather than isLoading: in React Query v5 `isLoading` is
  // `isPending && isFetching`, so during the pause *between* retries neither isLoading nor isError
  // is true and data is still undefined — which rendered "Nema prijava." on a failed request.
  const { data: items = [], isPending, isError, isSuccess, refetch } = useAdminFeedback(filters)

  const updateStatus = useUpdateFeedbackStatus()
  const deleteFeedback = useDeleteFeedback()

  const [selected, setSelected] = useState<AdminFeedback | null>(null)
  const [confirmTarget, setConfirmTarget] = useState<AdminFeedback | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  // Detail-modal draft state, seeded when a row is opened.
  const [draftStatus, setDraftStatus] = useState<FeedbackStatus>(FeedbackStatus.New)
  const [draftResponse, setDraftResponse] = useState('')

  function openDetail(item: AdminFeedback) {
    setSelected(item)
    setDraftStatus(item.status)
    setDraftResponse(item.adminResponse ?? '')
  }

  async function handleSave() {
    if (!selected) return
    try {
      await updateStatus.mutateAsync({
        id: selected.id,
        payload: {
          status: draftStatus,
          adminResponse: draftResponse.trim() === '' ? null : draftResponse.trim(),
        },
      })
      toast.success('Sačuvano. Korisnik je obaviješten.')
      setSelected(null)
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Greška pri spremanju.')
    }
  }

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await deleteFeedback.mutateAsync(confirmTarget.id)
      toast.success('Prijava obrisana.')
      setConfirmTarget(null)
      setSelected(null)
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Greška pri brisanju.')
    } finally {
      setIsDeleting(false)
    }
  }

  const newCount = items.filter(i => i.status === FeedbackStatus.New).length

  return (
    <div className="animate-fade-in space-y-6">
      {/* Hero */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex items-center gap-4 min-w-0">
          <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
            📮
          </div>
          <div className="min-w-0">
            <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">
              Povratne informacije
            </h1>
            <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
              {!isSuccess
                ? 'Učitavanje…'
                : items.length === 0
                ? 'Nema prijava za odabrane filtere.'
                : `${items.length} prijava · ${newCount} neobrađeno`}
            </p>
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <select
          value={typeFilter}
          onChange={e => setTypeFilter(e.target.value === '' ? '' : (Number(e.target.value) as FeedbackType))}
          className="form-input w-auto"
          aria-label="Filtriraj po vrsti"
        >
          <option value="">Sve vrste</option>
          {TYPE_ORDER.map(t => (
            <option key={t} value={t}>{FeedbackTypeLabels[t]}</option>
          ))}
        </select>
        <select
          value={statusFilter}
          onChange={e => setStatusFilter(e.target.value === '' ? '' : (Number(e.target.value) as FeedbackStatus))}
          className="form-input w-auto"
          aria-label="Filtriraj po statusu"
        >
          <option value="">Svi statusi</option>
          {STATUS_ORDER.map(s => (
            <option key={s} value={s}>{FeedbackStatusLabels[s]}</option>
          ))}
        </select>
      </div>

      {isPending && <VitalsSkeleton />}

      {isError && <ErrorState message="Greška pri učitavanju povratnih informacija." onRetry={refetch} />}

      {isSuccess && items.length === 0 && (
        <EmptyState
          title="Nema prijava."
          description="Kada korisnici pošalju povratnu informaciju, pojavit će se ovdje."
        />
      )}

      {isSuccess && items.length > 0 && (
        <div className="space-y-3">
          {items.map(item => (
            <button
              key={item.id}
              onClick={() => openDetail(item)}
              className="w-full text-left bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4 hover:border-honey-300 dark:hover:border-slate-700 transition-colors"
            >
              <div className="flex items-start gap-3">
                <span className="text-lg leading-none mt-0.5 shrink-0">{FeedbackTypeEmojis[item.type]}</span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-semibold text-gray-900 dark:text-slate-100">{item.subject}</span>
                    <span className={`text-xs rounded-full px-2 py-0.5 ${STATUS_STYLE[item.status]}`}>
                      {item.statusName}
                    </span>
                    {item.severityName && (
                      <span className="text-xs text-gray-500 dark:text-slate-400">· {item.severityName}</span>
                    )}
                    {item.hasScreenshot && (
                      <ImageIcon className="w-3.5 h-3.5 text-gray-400 dark:text-slate-500" aria-label="Ima sliku" />
                    )}
                  </div>
                  <p className="mt-1 text-sm text-gray-600 dark:text-slate-300 line-clamp-2">{item.message}</p>
                  <div className="flex items-center gap-2 mt-1.5 text-xs text-gray-500 dark:text-slate-400 flex-wrap">
                    <span>{item.typeName}</span>
                    <span>·</span>
                    <span>{item.submitterName ?? 'Obrisani korisnik'}</span>
                    {item.organizationName && (<><span>·</span><span>{item.organizationName}</span></>)}
                    <span>·</span>
                    <span>{format(new Date(item.createdAt), 'dd.MM.yyyy HH:mm')}</span>
                  </div>
                </div>
              </div>
            </button>
          ))}
        </div>
      )}

      {/* Detail + triage */}
      <Modal
        open={selected !== null}
        onClose={() => setSelected(null)}
        title={selected?.subject ?? ''}
        description={selected ? `${selected.typeName} · ${format(new Date(selected.createdAt), 'dd.MM.yyyy HH:mm')}` : undefined}
        size="lg"
        icon={
          <div className="w-10 h-10 bg-honey-100 dark:bg-honey-500/15 rounded-full flex items-center justify-center text-lg">
            {selected ? FeedbackTypeEmojis[selected.type] : <Inbox className="w-5 h-5" />}
          </div>
        }
        footer={
          <div className="flex gap-3">
            <button
              onClick={() => selected && setConfirmTarget(selected)}
              className="btn-secondary text-sm text-red-600 dark:text-red-400"
              aria-label="Obriši prijavu"
            >
              <Trash2 className="w-4 h-4" />
            </button>
            <button onClick={() => setSelected(null)} className="btn-secondary flex-1 justify-center text-sm">
              Zatvori
            </button>
            <button
              onClick={handleSave}
              disabled={updateStatus.isPending}
              className="btn-primary flex-1 justify-center text-sm"
            >
              {updateStatus.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Sačuvaj'}
            </button>
          </div>
        }
      >
        {selected && (
          <div className="space-y-4">
            <p className="text-sm text-gray-700 dark:text-slate-200 whitespace-pre-wrap">{selected.message}</p>

            <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-2 text-sm p-4 rounded-xl bg-gray-50 dark:bg-slate-800/60">
              <Detail label="Poslao" value={selected.submitterName ?? 'Obrisani korisnik'} />
              <Detail label="E-pošta" value={selected.submitterEmail ?? '—'} />
              <Detail label="Organizacija" value={selected.organizationName ?? '—'} />
              <Detail label="Ozbiljnost" value={selected.severityName ?? '—'} />
              <Detail label="Stranica" value={selected.pageContext ?? '—'} />
              <Detail label="Preglednik" value={selected.userAgent ?? '—'} />
            </dl>

            {selected.hasScreenshot && (
              <div>
                <span className="form-label">Slika ekrana</span>
                <FeedbackScreenshot feedbackId={selected.id} />
              </div>
            )}

            <div>
              <label htmlFor="admin-status" className="form-label">Status</label>
              <select
                id="admin-status"
                value={draftStatus}
                onChange={e => setDraftStatus(Number(e.target.value) as FeedbackStatus)}
                className="form-input"
              >
                {STATUS_ORDER.map(s => (
                  <option key={s} value={s}>{FeedbackStatusLabels[s]}</option>
                ))}
              </select>
            </div>

            <div>
              <label htmlFor="admin-response" className="form-label">
                Odgovor korisniku <span className="font-normal text-gray-400">(nije obavezno)</span>
              </label>
              <textarea
                id="admin-response"
                rows={4}
                maxLength={1000}
                value={draftResponse}
                onChange={e => setDraftResponse(e.target.value)}
                className="form-input"
                placeholder="Korisnik dobija obavijest i e-poštu s ovim tekstom."
              />
            </div>
          </div>
        )}
      </Modal>

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši prijavu"
        message={confirmTarget ? `Obrisati „${confirmTarget.subject}“? Ovo se ne može vratiti.` : ''}
        confirmLabel="Obriši"
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}

/**
 * Fetches the screenshot through apiClient (Bearer header) and renders it via an object URL —
 * a plain <img src> can't authenticate. Mirrors `AuthImage` in InspectionPhotos.
 */
function FeedbackScreenshot({ feedbackId }: { feedbackId: number }) {
  const [url, setUrl] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let objectUrl: string | null = null
    let cancelled = false
    setUrl(null)
    setFailed(false)
    feedbackService
      .fetchScreenshotBlob(feedbackId)
      .then(blob => {
        objectUrl = URL.createObjectURL(blob)
        if (!cancelled) setUrl(objectUrl)
      })
      .catch(() => { if (!cancelled) setFailed(true) })
    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [feedbackId])

  if (failed) {
    return (
      <p className="text-sm text-gray-500 dark:text-slate-400">
        Slika se nije mogla učitati.
      </p>
    )
  }
  if (!url) {
    return (
      <div className="flex items-center justify-center h-32 rounded-xl bg-gray-100 dark:bg-slate-800">
        <Loader2 className="w-4 h-4 animate-spin text-gray-400 dark:text-slate-500" />
      </div>
    )
  }
  return (
    <img
      src={url}
      alt="Slika ekrana koju je poslao korisnik"
      className="max-w-full rounded-xl border border-gray-200 dark:border-slate-700"
    />
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-gray-500 dark:text-slate-400">{label}</dt>
      <dd className="text-gray-800 dark:text-slate-200 break-words">{value}</dd>
    </div>
  )
}
