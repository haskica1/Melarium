import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import { AlertTriangle, CloudOff, Loader2, PencilLine, Send, Trash2 } from 'lucide-react'
import { useAuth } from '../../core/context/AuthContext'
import { useToast } from '../../core/context/ToastContext'
import { useOnlineStatus } from '../../core/hooks/useOnlineStatus'
import { useOutbox } from '../../core/hooks/useOutbox'
import { removeOutboxItem, type OutboxItem } from '../../core/offline/outbox'
import { flushOutbox } from '../../core/offline/syncOutbox'
import { ConfirmDialog, EmptyState } from '../../shared/components'

export default function OutboxPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const online = useOnlineStatus()

  const items = useOutbox(user?.email)
  const pendingCount = items.filter(i => i.status === 'pending').length

  const [isSending, setIsSending] = useState(false)
  const [confirmTarget, setConfirmTarget] = useState<OutboxItem | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  async function handleSendNow() {
    if (!user?.email) return
    setIsSending(true)
    try {
      const result = await flushOutbox(user.email)
      if (result.synced > 0) {
        toast.success(`Sinhronizovano ${result.synced} pregleda.`)
        void queryClient.invalidateQueries({ queryKey: ['inspections'] })
        void queryClient.invalidateQueries({ queryKey: ['beehives'] })
      }
      if (result.failed > 0) toast.error(`Server je odbio ${result.failed} — pogledajte detalje ispod.`)
      if (result.stoppedOffline) toast.error('Nema mreže — pokušajte kasnije.')
    } finally {
      setIsSending(false)
    }
  }

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await removeOutboxItem(confirmTarget.localId)
      toast.success('Pregled obrisan iz neposlanih.')
      setConfirmTarget(null)
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <div className="animate-fade-in space-y-6">
      {/* Hero */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center gap-4 min-w-0">
            <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center shadow-honey dark:shadow-none">
              <CloudOff className="w-7 h-7 text-honey-600 dark:text-honey-400" />
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Neposlani pregledi</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
                Pregledi sačuvani offline — šalju se automatski kad se mreža vrati.
              </p>
            </div>
          </div>
          {pendingCount > 0 && (
            <button
              onClick={handleSendNow}
              disabled={!online || isSending}
              className="btn-primary text-sm shrink-0 disabled:opacity-60"
              title={online ? undefined : 'Nema mreže'}
            >
              {isSending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
              Pošalji sada ({pendingCount})
            </button>
          )}
        </div>
      </div>

      {items.length === 0 ? (
        <EmptyState
          title="Nema neposlanih pregleda."
          description="Sve što zabilježite bez mreže pojaviće se ovdje i biće poslano automatski."
        />
      ) : (
        <div className="space-y-3">
          {items.map(item => (
            <div key={item.localId} className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4">
              <div className="flex items-center gap-4">
                <div className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 ${
                  item.status === 'failed'
                    ? 'bg-red-50 text-red-500 dark:bg-red-500/15 dark:text-red-400'
                    : 'bg-amber-50 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300'
                }`}>
                  {item.status === 'failed' ? <AlertTriangle className="w-5 h-5" /> : <CloudOff className="w-5 h-5" />}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-semibold text-gray-900 dark:text-slate-100">{item.beehiveName}</span>
                    <span className={`text-xs rounded-full px-2 py-0.5 ${
                      item.status === 'failed'
                        ? 'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300'
                        : 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300'
                    }`}>
                      {item.status === 'failed' ? 'Odbijeno' : 'Na čekanju'}
                    </span>
                  </div>
                  <p className="mt-0.5 text-sm text-gray-500 dark:text-slate-400">
                    Pregled od {format(new Date(item.payload.date), 'dd.MM.yyyy')} · sačuvano {format(new Date(item.createdAt), 'dd.MM.yyyy HH:mm')}
                  </p>
                </div>
                <div className="flex items-center gap-1 shrink-0">
                  <button
                    onClick={() => navigate(`/inspections/new?beehiveId=${item.beehiveId}&outboxId=${item.localId}`)}
                    className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                    aria-label="Uredi pregled"
                    title="Uredi"
                  >
                    <PencilLine className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() => setConfirmTarget(item)}
                    className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                    aria-label="Obriši pregled"
                    title="Obriši"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>
              {item.status === 'failed' && item.error && (
                <p className="mt-3 text-sm text-red-700 dark:text-red-300 bg-red-50 dark:bg-red-500/10 border border-red-100 dark:border-red-500/20 rounded-xl px-3 py-2">
                  {item.error} — uredite pregled ili ga obrišite.
                </p>
              )}
            </div>
          ))}
        </div>
      )}

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši neposlani pregled"
        message={confirmTarget
          ? `Obrisati pregled za "${confirmTarget.beehiveName}" od ${format(new Date(confirmTarget.payload.date), 'dd.MM.yyyy')}? Podaci nisu poslani na server i biće izgubljeni.`
          : ''}
        confirmLabel="Obriši"
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}
