import { useEffect } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { Sparkles, X } from 'lucide-react'
import { AssistantThread } from './AssistantThread'

/**
 * The assistant's global sheet (SPEC-17 D2), mounted once in Layout so it follows the beekeeper
 * around the app instead of making them navigate away from the hive they are looking at.
 *
 * Opening it belongs to Layout, not to this component: the button that does it shares the corner of
 * the screen with the QR scanner (see `FabDock`), and one owner for that corner is what keeps the
 * two from landing on top of each other. Layout also decides when the assistant is unavailable —
 * offline, or on `/assistant`, where a floating shortcut to the page you are on is noise.
 */
export default function AssistantSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()

  // The page the user is standing on becomes context for the command; an explicitly spoken apiary or
  // hive still wins, server-side.
  const { apiaryId, beehiveId } = useRouteContext()

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center sm:justify-center">
      <div
        className="absolute inset-0 bg-black/40"
        onClick={onClose}
        aria-hidden="true"
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-label="AI asistent"
        className="relative w-full sm:max-w-lg h-[85vh] sm:h-[70vh] bg-white dark:bg-slate-900 rounded-t-3xl sm:rounded-3xl shadow-2xl flex flex-col overflow-hidden animate-fade-in"
      >
        <div className="flex items-center gap-2 px-4 py-3 border-b border-honey-100 dark:border-slate-800">
          <Sparkles className="w-5 h-5 text-honey-500" />
          <h2 className="flex-1 text-sm font-semibold text-gray-800 dark:text-slate-100">AI asistent</h2>
          <button
            type="button"
            onClick={() => { onClose(); navigate('/assistant') }}
            className="text-xs text-gray-500 dark:text-slate-400 hover:text-honey-600"
          >
            Historija
          </button>
          <button
            type="button"
            onClick={onClose}
            className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-500 dark:text-slate-400 hover:bg-gray-100 dark:hover:bg-slate-800"
            aria-label="Zatvori"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="flex-1 min-h-0">
          <AssistantThread contextApiaryId={apiaryId} contextBeehiveId={beehiveId} />
        </div>
      </div>
    </div>
  )
}

/**
 * Reads the apiary/hive the current route is about. `useParams` is empty here because the launcher
 * sits outside the matched route, so the path is parsed directly.
 */
function useRouteContext(): { apiaryId: number | null; beehiveId: number | null } {
  const { pathname } = useLocation()
  useParams() // keeps the hook order stable if this component is ever moved inside a route

  const apiaryMatch = pathname.match(/^\/apiaries\/(\d+)/)
  const beehiveMatch = pathname.match(/^\/beehives\/(\d+)/)

  return {
    apiaryId: apiaryMatch ? Number(apiaryMatch[1]) : null,
    beehiveId: beehiveMatch ? Number(beehiveMatch[1]) : null,
  }
}
