import { useMemo, useState } from 'react'
import { CheckCircle2, Loader2, Sparkles } from 'lucide-react'
import { useToast } from '../../core/context/ToastContext'
import { useAnnouncements, useMarkAnnouncementRead } from '../../core/services/announcementQueries'
import { announcementService } from '../../core/services/announcementService'
import { AnnouncementType, AnnouncementTypeLabels } from '../../core/models'
import type { AnnouncementDetail, AnnouncementSummary } from '../../core/models'
import { EmptyState, ErrorState, VitalsSkeleton } from '../../shared/components'
import AnnouncementModal from '../../shared/components/AnnouncementModal'
import { ANNOUNCEMENT_TYPE_CLASS } from '../../shared/components/announcementType'

const TYPES = Object.values(AnnouncementType).filter(v => typeof v === 'number') as AnnouncementType[]

/**
 * "Šta je novo" — the archive (SPEC-21). Everything ever published lives here, including what the
 * banner never showed because a newer announcement had already replaced it (D1).
 */
export default function AnnouncementsPage() {
  const { data, isLoading, isError, refetch } = useAnnouncements()
  const markRead = useMarkAnnouncementRead()
  const { toast } = useToast()

  const [type, setType] = useState<AnnouncementType | 0>(0)
  const [open, setOpen] = useState<AnnouncementDetail | null>(null)
  const [loadingId, setLoadingId] = useState<number | null>(null)

  const items = data?.items ?? []
  const filtered = useMemo(
    () => items.filter(a => type === 0 || a.type === type),
    [items, type],
  )
  const readCount = items.filter(a => a.isRead).length

  // The list carries no body, so the modal fetches the one announcement it needs on open. The
  // failure path has to say something: without it a tap offline does nothing at all and the page
  // just looks broken.
  async function openAnnouncement(summary: AnnouncementSummary) {
    setLoadingId(summary.id)
    try {
      setOpen(await announcementService.getById(summary.id))
    } catch {
      toast.error('Greška pri učitavanju objave.')
    } finally {
      setLoadingId(null)
    }
  }

  return (
    <div className="animate-fade-in space-y-6">
      {/* Hero */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex items-center gap-4">
          <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
            ✨
          </div>
          <div className="min-w-0">
            <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Šta je novo</h1>
            <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
              Sve novosti u Melariumu na jednom mjestu — pročitano {readCount} od {items.length}.
            </p>
          </div>
        </div>
      </div>

      {/* Type filter chips */}
      <div className="flex items-center gap-2 flex-wrap">
        <FilterChip label="Sve" active={type === 0} onClick={() => setType(0)} />
        {TYPES.map(t => (
          <FilterChip
            key={t}
            label={AnnouncementTypeLabels[t]}
            active={type === t}
            onClick={() => setType(t)}
          />
        ))}
      </div>

      {isLoading && <VitalsSkeleton />}

      {isError && <ErrorState message="Greška pri učitavanju novosti." onRetry={refetch} />}

      {!isLoading && !isError && items.length === 0 && (
        <EmptyState
          title="Još nema objava."
          description="Ovdje ćete vidjeti svaku novu funkcionalnost u Melariumu."
        />
      )}

      {!isLoading && !isError && items.length > 0 && filtered.length === 0 && (
        <EmptyState
          title="Nema objava ovog tipa."
          description="Odaberite drugi tip ili pogledajte sve objave."
        />
      )}

      {filtered.length > 0 && (
        <div className="space-y-3">
          {filtered.map(a => (
            <AnnouncementCard
              key={a.id}
              announcement={a}
              loading={loadingId === a.id}
              onOpen={() => openAnnouncement(a)}
            />
          ))}
        </div>
      )}

      <AnnouncementModal
        announcement={open}
        open={open !== null}
        onClose={() => {
          // Opening from the archive marks it seen too — one state, not two (D2).
          if (open && !open.isRead) markRead.mutate(open.id)
          setOpen(null)
        }}
      />
    </div>
  )
}

function FilterChip({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className={`px-3 py-1.5 rounded-full text-sm font-medium border transition-colors ${
        active
          ? 'bg-honey-500 border-honey-500 text-white'
          : 'bg-white dark:bg-slate-800 border-honey-200 dark:border-slate-700 text-gray-600 dark:text-slate-300 hover:bg-honey-50 dark:hover:bg-slate-700'
      }`}
    >
      {label}
    </button>
  )
}

function AnnouncementCard({
  announcement,
  loading,
  onOpen,
}: {
  announcement: AnnouncementSummary
  loading: boolean
  onOpen: () => void
}) {
  return (
    <button
      type="button"
      onClick={onOpen}
      disabled={loading}
      className="w-full text-left block bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800
                 px-4 py-3.5 sm:px-5 sm:py-4 shadow-sm dark:shadow-none
                 hover:border-honey-200 dark:hover:border-slate-700 transition-colors disabled:opacity-60"
    >
      <div className="flex items-start gap-3">
        <div className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl flex items-center justify-center shrink-0 bg-honey-50 text-honey-600 dark:bg-honey-500/15 dark:text-honey-300">
          {loading
            ? <Loader2 className="w-4 h-4 sm:w-5 sm:h-5 animate-spin" />
            : <Sparkles className="w-4 h-4 sm:w-5 sm:h-5" />}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-start gap-1.5">
            <h3 className="font-semibold text-gray-900 dark:text-slate-100 leading-snug line-clamp-2 break-words min-w-0">
              {announcement.title}
            </h3>
            {announcement.isRead && (
              <CheckCircle2 className="w-4 h-4 mt-0.5 text-emerald-500 shrink-0" aria-label="Pročitano" />
            )}
          </div>
          <div className="mt-2 flex items-center gap-x-2 gap-y-1 flex-wrap">
            <span className={`text-xs font-medium rounded-full px-2 py-0.5 ${ANNOUNCEMENT_TYPE_CLASS[announcement.type]}`}>
              {announcement.typeName}
            </span>
            {announcement.publishedAt && (
              <span className="text-xs text-gray-400 dark:text-slate-500">
                {new Date(announcement.publishedAt).toLocaleDateString('bs-BA')}
              </span>
            )}
          </div>
        </div>
      </div>
    </button>
  )
}
