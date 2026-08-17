import clsx from 'clsx'

export interface FabAction {
  key: string
  icon: React.ReactNode
  /** Used for both the accessible name and the desktop tooltip. */
  label: string
  onClick: () => void
  /** Hidden from `sm` up — for actions that already have a home in the desktop header. */
  mobileOnly?: boolean
}

/**
 * The bottom-right corner, owned by one component.
 *
 * Scanning and the AI assistant were two separate `fixed bottom-right` buttons of identical size,
 * which meant that on a phone they sat on top of each other — each one guessing where the other was.
 * A dock makes that impossible to reproduce: whatever needs to float in that corner is an entry in
 * one list, laid out side by side in a single capsule.
 *
 * Side by side rather than stacked or behind a speed-dial: both are "open a tool" actions used in
 * the field, often with gloves on, so both stay one tap away — and a capsule costs the height of a
 * single button instead of covering content in two places. On desktop the mobile-only halves drop
 * out and the capsule collapses back into an ordinary round FAB.
 */
export default function FabDock({ actions }: { actions: FabAction[] }) {
  if (actions.length === 0) return null

  return (
    <div
      role="group"
      aria-label="Brze akcije"
      className="fixed bottom-5 right-5 z-40 flex items-center rounded-full overflow-hidden
                 bg-honey-500 text-white shadow-lg shadow-honey-500/30"
    >
      {actions.map((action, i) => (
        <button
          key={action.key}
          type="button"
          onClick={action.onClick}
          aria-label={action.label}
          title={action.label}
          className={clsx(
            'w-14 h-14 flex items-center justify-center shrink-0 transition-colors',
            'hover:bg-honey-600 active:bg-honey-700',
            // The separator belongs to the button on its left, so it disappears together with a
            // mobile-only half instead of leaving a stray line down the desktop FAB.
            i < actions.length - 1 && 'border-r border-white/25',
            action.mobileOnly && 'sm:hidden',
          )}
        >
          {action.icon}
        </button>
      ))}
    </div>
  )
}
