import { useEffect, useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { Sparkles, X } from 'lucide-react'
import { useOnlineStatus } from '../../core/hooks/useOnlineStatus'
import { AssistantThread } from './AssistantThread'

/**
 * The assistant's global entry point (SPEC-17 D2): one floating mic button on every page, opening a
 * sheet with the thread. Mounted once in Layout so it follows the beekeeper around the app instead of
 * making them navigate away from the hive they are looking at.
 */
export default function AssistantLauncher() {
  const [open, setOpen] = useState(false)
  const location = useLocation()
  const navigate = useNavigate()
  const online = useOnlineStatus()

  // The page the user is standing on becomes context for the command; an explicitly spoken apiary or
  // hive still wins, server-side.
  const { apiaryId, beehiveId } = useRouteContext()

  // Navigating away closes the sheet — its context would otherwise be stale.
  useEffect(() => { setOpen(false) }, [location.pathname])

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false) }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open])

  // The assistant needs the server for both transcription and interpretation (SPEC-07 precedent:
  // voice input is hidden offline rather than failing on tap).
  if (!online) return null

  // The full-page assistant already is the assistant — a floating button on top of it is noise.
  if (location.pathname.startsWith('/assistant')) return null

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="fixed bottom-5 right-5 z-40 w-14 h-14 rounded-full bg-honey-500 hover:bg-honey-600 text-white shadow-lg shadow-honey-500/30 flex items-center justify-center transition-transform hover:scale-105 active:scale-95"
        aria-label="Otvori AI asistenta"
      >
        <Sparkles className="w-6 h-6" />
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-end sm:items-center sm:justify-center">
          <div
            className="absolute inset-0 bg-black/40"
            onClick={() => setOpen(false)}
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
                onClick={() => { setOpen(false); navigate('/assistant') }}
                className="text-xs text-gray-500 dark:text-slate-400 hover:text-honey-600"
              >
                Historija
              </button>
              <button
                type="button"
                onClick={() => setOpen(false)}
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
      )}
    </>
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
