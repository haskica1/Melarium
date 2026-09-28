import clsx from 'clsx'

/**
 * A labelled on/off switch row. `disabled` shows a setting that exists but cannot be changed — the
 * Critical notifications switch (SPEC-29) — rather than hiding it, so the user knows why it stays on.
 */
export default function Toggle({
  checked,
  onChange,
  label,
  hint,
  disabled = false,
}: {
  checked: boolean
  onChange: (v: boolean) => void
  label: string
  hint?: string
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      onClick={() => !disabled && onChange(!checked)}
      disabled={disabled}
      className={clsx('w-full flex items-start justify-between gap-4 py-3 text-left', disabled && 'cursor-not-allowed')}
    >
      <span>
        <span className="block text-sm font-medium text-gray-800 dark:text-slate-100">{label}</span>
        {hint && <span className="block text-xs text-gray-500 dark:text-slate-400 mt-0.5">{hint}</span>}
      </span>
      <span
        role="switch"
        aria-checked={checked}
        aria-disabled={disabled || undefined}
        className={clsx(
          'mt-0.5 relative inline-flex h-6 w-11 shrink-0 rounded-full transition-colors',
          checked ? 'bg-honey-500' : 'bg-gray-300 dark:bg-slate-600',
          disabled && 'opacity-60',
        )}
      >
        <span className={clsx('absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition-transform', checked ? 'translate-x-[22px]' : 'translate-x-0.5')} />
      </span>
    </button>
  )
}
