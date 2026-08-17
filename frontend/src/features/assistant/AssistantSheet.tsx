import { useCallback, useRef, useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { History, Sparkles, X } from 'lucide-react'
import { useDialogBehavior } from '../../shared/hooks/useDialogBehavior'
import { AssistantThread } from './AssistantThread'

/** Drag distance past which letting go dismisses the sheet instead of springing it back. */
const DISMISS_AFTER_PX = 110

/**
 * The assistant's global sheet (SPEC-17 D2), mounted once in Layout so it follows the beekeeper
 * around the app instead of making them navigate away from the hive they are looking at.
 *
 * Opening it belongs to Layout, not to this component: the button that does it shares the corner of
 * the screen with the QR scanner (see `FabDock`), and one owner for that corner is what keeps the
 * two from landing on top of each other. Layout also decides when the assistant is unavailable —
 * offline, or on `/assistant`, where a floating shortcut to the page you are on is noise.
 *
 * On a phone this is a real bottom sheet: grab handle, drag down to dismiss, and it stops the page
 * behind it from moving. It was the only overlay in the app not using `useDialogBehavior` — which is
 * exactly why the content behind it scrolled while every other dialog's did not.
 */
export default function AssistantSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()

  // The page the user is standing on becomes context for the command; an explicitly spoken apiary or
  // hive still wins, server-side.
  const { apiaryId, beehiveId } = useRouteContext()

  // Escape, focus trap, focus restore and the body scroll lock — the same behaviour every other
  // dialog in the app gets, instead of this one hand-rolling a bare Escape listener.
  const { panelProps } = useDialogBehavior({ open, onClose })

  const [dragY, setDragY] = useState(0)
  const drag = useRef<{ pointerId: number; startY: number } | null>(null)
  // Mirrors dragY. The distance has to be readable in the pointerup handler *without* going through
  // a setState updater: an updater runs during render, so closing the sheet from inside one is a
  // state update on the parent mid-render — React warns, and StrictMode can run it twice.
  const dragYRef = useRef(0)

  const isBottomSheet = () => !window.matchMedia('(min-width: 640px)').matches

  const onPointerDown = useCallback((event: React.PointerEvent) => {
    // Desktop renders a centred dialog, where dragging it downwards means nothing. Buttons in the
    // header keep their taps.
    if (!isBottomSheet()) return
    if ((event.target as HTMLElement).closest('button')) return

    drag.current = { pointerId: event.pointerId, startY: event.clientY }
    try {
      // Keeps the gesture alive when the thumb slides off the header. Not essential — without it
      // the drag simply ends early — so a browser that refuses must not take the sheet down with it.
      event.currentTarget.setPointerCapture(event.pointerId)
    } catch { /* capture unavailable; the drag still works while the thumb stays on the header */ }
  }, [])

  const onPointerMove = useCallback((event: React.PointerEvent) => {
    // A second finger landing mid-drag must not yank the sheet to wherever it touched down.
    if (drag.current?.pointerId !== event.pointerId) return
    // Downwards only — dragging up must not detach the sheet from the bottom of the screen.
    const travelled = Math.max(0, event.clientY - drag.current.startY)
    dragYRef.current = travelled
    setDragY(travelled)
  }, [])

  const onPointerUp = useCallback((event: React.PointerEvent) => {
    if (drag.current?.pointerId !== event.pointerId) return
    drag.current = null

    const travelled = dragYRef.current
    dragYRef.current = 0
    setDragY(0)
    if (travelled > DISMISS_AFTER_PX) onClose()
  }, [onClose])

  if (!open) return null

  const dragging = dragY > 0

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center sm:justify-center">
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
        aria-hidden="true"
      />

      <div
        {...panelProps}
        aria-label="AI asistent"
        // `dvh`, not `vh`: on a phone `vh` is measured against the viewport with the browser's URL
        // bar *hidden*, so 85vh reached below the visible area and pushed the input row — the whole
        // point of the sheet — off the bottom of the screen.
        //
        // The transform is applied only while a drag is in progress, on purpose: a transform makes
        // the panel a containing block, and `AssistantThread` renders a `fixed inset-0` confirmation
        // modal inside it. Off-drag there is no transform, so that modal still covers the viewport.
        style={dragging ? { transform: `translateY(${dragY}px)` } : undefined}
        className={`relative w-full sm:max-w-lg h-[85dvh] sm:h-[70dvh] max-h-[85dvh]
                    bg-white dark:bg-slate-900 rounded-t-3xl sm:rounded-3xl shadow-2xl
                    flex flex-col overflow-hidden outline-none
                    pb-[env(safe-area-inset-bottom)] sm:pb-0
                    ${dragging ? '' : 'transition-transform duration-200 animate-slide-up'}`}
      >
        {/* Grab area: the handle and the title row are one drag target, so a thumb anywhere along
            the top of the sheet can pull it closed. */}
        <div
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerCancel={onPointerUp}
          className="shrink-0 touch-none"
        >
          <div className="sm:hidden pt-2.5 pb-1 flex justify-center" aria-hidden="true">
            <span className="h-1.5 w-10 rounded-full bg-gray-300 dark:bg-slate-600" />
          </div>

          <div className="flex items-center gap-2.5 px-4 py-2.5 sm:py-3 border-b border-honey-100 dark:border-slate-800">
            <span className="w-8 h-8 shrink-0 rounded-xl bg-honey-100 dark:bg-honey-500/15 flex items-center justify-center">
              <Sparkles className="w-4 h-4 text-honey-600 dark:text-honey-400" />
            </span>
            <h2 className="flex-1 min-w-0 text-sm font-semibold text-gray-800 dark:text-slate-100">
              AI asistent
            </h2>
            <button
              type="button"
              onClick={() => { onClose(); navigate('/assistant') }}
              className="w-9 h-9 rounded-xl flex items-center justify-center text-gray-500 dark:text-slate-400 hover:bg-honey-50 dark:hover:bg-slate-800 hover:text-honey-600 transition-colors"
              aria-label="Historija razgovora"
              title="Historija"
            >
              <History className="w-[18px] h-[18px]" />
            </button>
            <button
              type="button"
              onClick={onClose}
              className="w-9 h-9 rounded-xl flex items-center justify-center text-gray-500 dark:text-slate-400 hover:bg-gray-100 dark:hover:bg-slate-800 transition-colors"
              aria-label="Zatvori"
            >
              <X className="w-[18px] h-[18px]" />
            </button>
          </div>
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
