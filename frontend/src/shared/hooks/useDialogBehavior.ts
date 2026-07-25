import { useCallback, useEffect, useRef } from 'react'

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

  const handleKeyDown = useCallback(
    (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.stopPropagation()
        onClose()
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
    },
    [onClose],
  )

  useEffect(() => {
    if (!open) return

    previouslyFocused.current = document.activeElement as HTMLElement | null

    const { overflow } = document.body.style
    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', handleKeyDown)

    // One frame so the panel exists before we focus it.
    const focusTimer = window.setTimeout(() => {
      ;(initialFocusRef?.current ?? panelRef.current)?.focus()
    }, 0)

    return () => {
      window.clearTimeout(focusTimer)
      document.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = overflow
      previouslyFocused.current?.focus?.()
    }
  }, [open, handleKeyDown, initialFocusRef])

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
