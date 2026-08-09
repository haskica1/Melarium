import { useState } from 'react'
import { format } from 'date-fns'
import { Loader2, MessageSquarePlus, Sparkles, Trash2 } from 'lucide-react'
import {
  useAssistantSession,
  useAssistantSessions,
  useDeleteAssistantSession,
} from '../../core/services/assistantQueries'
import { AssistantThread } from './AssistantThread'
import type { AssistantSessionDetail } from '../../core/models'

/**
 * Full-page assistant with history (SPEC-17 D8): every command, what the assistant understood, and
 * what it created. The floating launcher is for entering a command in one breath; this page is for
 * looking back at what was done.
 */
export default function AssistantPage() {
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const { data: sessions = [], isLoading } = useAssistantSessions()
  const { data: session } = useAssistantSession(selectedId ?? 0)
  const remove = useDeleteAssistantSession()

  async function handleDelete(id: number) {
    await remove.mutateAsync(id)
    if (selectedId === id) setSelectedId(null)
  }

  return (
    <div className="h-[calc(100vh-8rem)] flex gap-4">
      {/* Sessions */}
      <aside className={`w-full sm:w-72 shrink-0 flex flex-col rounded-2xl border border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden ${selectedId ? 'hidden sm:flex' : 'flex'}`}>
        <div className="p-3 border-b border-honey-100 dark:border-slate-800">
          <button
            type="button"
            onClick={() => setSelectedId(null)}
            className="btn-primary w-full text-sm flex items-center justify-center gap-2"
          >
            <MessageSquarePlus className="w-4 h-4" />
            Nova naredba
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-2 space-y-1">
          {isLoading && (
            <div className="flex justify-center py-6 text-gray-400">
              <Loader2 className="w-5 h-5 animate-spin" />
            </div>
          )}

          {!isLoading && sessions.length === 0 && (
            <p className="px-2 py-6 text-center text-xs text-gray-500 dark:text-slate-400">
              Još nema naredbi. Recite asistentu šta ste uradili na pčelinjaku.
            </p>
          )}

          {sessions.map(item => (
            <div
              key={item.id}
              className={`group flex items-start gap-2 rounded-xl px-2.5 py-2 cursor-pointer transition-colors ${
                selectedId === item.id
                  ? 'bg-honey-50 dark:bg-slate-800'
                  : 'hover:bg-gray-50 dark:hover:bg-slate-800/60'
              }`}
              onClick={() => setSelectedId(item.id)}
            >
              <div className="min-w-0 flex-1">
                <p className="text-sm text-gray-800 dark:text-slate-100 truncate">{item.title}</p>
                <p className="text-xs text-gray-400 dark:text-slate-500">
                  {format(new Date(item.lastActivityAt), 'dd.MM.yyyy HH:mm')}
                </p>
              </div>
              <button
                type="button"
                onClick={e => { e.stopPropagation(); void handleDelete(item.id) }}
                className="opacity-0 group-hover:opacity-100 focus:opacity-100 text-gray-400 hover:text-red-500 transition-opacity"
                aria-label={`Obriši: ${item.title}`}
              >
                <Trash2 className="w-4 h-4" />
              </button>
            </div>
          ))}
        </div>
      </aside>

      {/* Thread */}
      <section className={`flex-1 min-w-0 rounded-2xl border border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden ${selectedId ? 'flex' : 'hidden sm:flex'} flex-col`}>
        <header className="flex items-center gap-2 px-4 py-3 border-b border-honey-100 dark:border-slate-800">
          <button
            type="button"
            onClick={() => setSelectedId(null)}
            className="sm:hidden text-xs text-gray-500 dark:text-slate-400"
          >
            ← Nazad
          </button>
          <Sparkles className="w-5 h-5 text-honey-500" />
          <h1 className="text-sm font-semibold text-gray-800 dark:text-slate-100 truncate">
            {session?.title ?? 'AI asistent'}
          </h1>
        </header>

        <div className="flex-1 min-h-0">
          {/* Keyed on the session so switching threads resets the drafts and the input. */}
          <AssistantThread
            key={selectedId ?? 'new'}
            initialSession={session as AssistantSessionDetail | undefined}
            onSessionChanged={updated => setSelectedId(updated.id)}
          />
        </div>
      </section>
    </div>
  )
}
