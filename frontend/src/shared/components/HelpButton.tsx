import { CircleHelp } from 'lucide-react'
import clsx from 'clsx'

interface HelpButtonProps {
  onClick: () => void
  /** Attention dot until the user has opened help at least once. */
  showDot?: boolean
  /** `header` matches the other round header utilities; `mobile` matches the mobile icon row. */
  variant?: 'header' | 'mobile'
}

/**
 * The one help affordance, rendered from `Layout` so it sits in the same place on every page.
 * Pages with no help entry never render it — `Layout` omits it rather than showing a dead icon.
 */
export default function HelpButton({ onClick, showDot = false, variant = 'header' }: HelpButtonProps) {
  return (
    <button
      onClick={onClick}
      className={clsx(
        'relative flex items-center justify-center transition-colors',
        'text-gray-500 dark:text-slate-300 hover:text-honey-700 dark:hover:text-honey-300',
        variant === 'header'
          ? 'w-8 h-8 rounded-full hover:bg-gray-100 dark:hover:bg-slate-800'
          : 'p-2 rounded-lg hover:bg-honey-100 dark:hover:bg-slate-800',
      )}
      aria-label="Pomoć za ovu stranicu"
      title="Pomoć za ovu stranicu (?)"
    >
      <CircleHelp className={variant === 'header' ? 'w-[18px] h-[18px]' : 'w-5 h-5'} />
      {showDot && (
        <span
          className="absolute top-0.5 right-0.5 w-2 h-2 rounded-full bg-honey-500 ring-2 ring-white dark:ring-slate-900"
          aria-hidden="true"
        />
      )}
    </button>
  )
}
