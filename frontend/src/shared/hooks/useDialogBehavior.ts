import { useEffect, useRef } from 'react'

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'

interface Options {
  open: boolean
  onClose: () => void
  /** Element to focus on open. Defaults to the panel. */
  initialFocusRef?: React.RefObject<HTMLElement>
}

/**
 * Everything a dialog must do besides look like one: close on Escape, keep Tab inside, hand focus
 * back where it came from, and stop the page behind it from scrolling.
 *
 * Split out from `Modal` so the two bespoke overlays that cannot use the standard panel — the QR
 * scanner and the map picker, both bottom-sheets wrapping camera/map surfaces — still get
 * identical behaviour instead of each re-inventing (or forgetting) it.
 *
 * Spread the returned `panelProps` onto the dialog panel element.
 */
export function useDialogBehavior({ open, onClose, initialFocusRef }: Options) {
  const panelRef = useRef<HTMLDivElement>(null)
  const previouslyFocused = useRef<HTMLElement | null>(null)

  // The setup effect below must run exactly once per open — its teardown moves focus, so re-running
  // it mid-typing steals the caret. Callers routinely pass a fresh `onClose` on every render (an
  // inline arrow, or a handler declared in a component that re-renders as the user types in this
  // very dialog), so the handler is read through a ref instead of being an effect dependency.
  const latest = useRef({ onClose, initialFocusRef })
  latest.current = { onClose, initialFocusRef }

  useEffect(() => {
    if (!open) return

    previouslyFocused.current = document.activeElement as HTMLElement | null

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.stopPropagation()
        latest.current.onClose()
        return
      }

      if (event.key !== 'Tab' || !panelRef.current) return

      const focusable = Array.from(
        panelRef.current.querySelectorAll<HTMLElement>(FOCUSABLE),
      ).filter(el => el.offsetParent !== null)

      if (focusable.length === 0) {
        event.preventDefault()
        return
      }

      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      const active = document.activeElement

      if (event.shiftKey && (active === first || active === panelRef.current)) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && active === last) {
        event.preventDefault()
        first.focus()
      }
    }

    const { overflow } = document.body.style
    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', handleKeyDown)

    // One frame so the panel exists before we focus it.
    const focusTimer = window.setTimeout(() => {
      ;(latest.current.initialFocusRef?.current ?? panelRef.current)?.focus()
    }, 0)

    return () => {
      window.clearTimeout(focusTimer)
      document.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = overflow
      // Skip a trigger that the dialog's own action removed from the page — focusing a detached
      // node silently drops focus to <body> and loses the user's place.
      const trigger = previouslyFocused.current
      if (trigger?.isConnected) trigger.focus()
    }
  }, [open])

  return {
    panelRef,
    panelProps: {
      ref: panelRef,
      role: 'dialog' as const,
      'aria-modal': true as const,
      tabIndex: -1,
    },
  }
}
