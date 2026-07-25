import { useId } from 'react'
import { X } from 'lucide-react'
import clsx from 'clsx'
import { useDialogBehavior } from '../hooks/useDialogBehavior'

/** Panel widths. `full` is for content that needs the space (camera preview, map picker). */
const SIZE_CLASS = {
  sm: 'max-w-sm',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
  xl: 'max-w-4xl',
} as const

export interface ModalProps {
  open: boolean
  onClose: () => void
  /** Accessible name. Always required — a dialog without one is unusable with a screen reader. */
  title: string
  /** Hide the title bar visually while keeping it for assistive tech (designs that draw their own header). */
  hideTitle?: boolean
  /** Decorative badge shown beside the title. */
  icon?: React.ReactNode
  description?: string
  size?: keyof typeof SIZE_CLASS
  /** Set false for destructive or in-progress flows where a stray click should not discard work. */
  closeOnBackdropClick?: boolean
  /** Element to focus when the dialog opens. Defaults to the panel itself. */
  initialFocusRef?: React.RefObject<HTMLElement>
  children: React.ReactNode
  /** Pinned action row at the bottom of the panel. */
  footer?: React.ReactNode
}

/**
 * The standard dialog. Before this, every modal re-implemented its own backdrop and only about
 * half handled Escape — so closing behaviour depended on which screen you were on, and none were
 * reachable with a keyboard or announced by a screen reader.
 *
 * Behaviour (Escape, focus trap, focus restore, scroll lock) lives in `useDialogBehavior`, shared
 * with the two overlays that need a different shell.
 */
export function Modal({
  open,
  onClose,
  title,
  hideTitle = false,
  icon,
  description,
  size = 'md',
  closeOnBackdropClick = true,
  initialFocusRef,
  children,
  footer,
}: ModalProps) {
  const titleId = useId()
  const descriptionId = useId()
  const { panelProps } = useDialogBehavior({ open, onClose, initialFocusRef })

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-sm"
        onClick={closeOnBackdropClick ? onClose : undefined}
        aria-hidden="true"
      />

      <div
        {...panelProps}
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        className={clsx(
          'relative w-full bg-white dark:bg-slate-900 dark:border dark:border-slate-800',
          'rounded-2xl shadow-2xl animate-slide-up outline-none flex flex-col max-h-[85vh]',
          SIZE_CLASS[size],
        )}
      >
        <div className={clsx('flex items-start gap-3 p-5 sm:p-6 pr-14', hideTitle && 'sr-only')}>
          {!hideTitle && icon && <div className="shrink-0">{icon}</div>}
          <div className="min-w-0 flex-1">
            <h2
              id={titleId}
              className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100"
            >
              {title}
            </h2>
            {description && (
              <p id={descriptionId} className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                {description}
              </p>
            )}
          </div>
        </div>

        {/* Always rendered, even when the title is visually hidden — otherwise a pointer-only
            design would have no close affordance besides the backdrop. */}
        <button
          onClick={onClose}
          className="absolute top-4 right-4 z-10 p-1 rounded-lg text-gray-400 hover:text-gray-600 dark:hover:text-slate-200 hover:bg-gray-100 dark:hover:bg-slate-800 transition-colors"
          aria-label="Zatvori"
        >
          <X className="w-5 h-5" />
        </button>

        <div className={clsx('flex-1 overflow-y-auto px-5 sm:px-6', hideTitle ? 'pt-5 pb-5' : 'pb-5')}>
          {children}
        </div>

        {footer && (
          <div className="shrink-0 border-t border-gray-100 dark:border-slate-800 p-4 sm:px-6">
            {footer}
          </div>
        )}
      </div>
    </div>
  )
}
