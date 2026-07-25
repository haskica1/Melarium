import { Link } from 'react-router-dom'
import { ArrowLeft, Moon, Sun } from 'lucide-react'
import clsx from 'clsx'
import { useTheme } from '../../core/hooks/useTheme'

/**
 * Shared shell for the utility auth pages (forgot / reset / verify). Mirrors the right-hand
 * panel of Login and Register — same background, card, logo and theme toggle — so the whole
 * signed-out flow reads as one screen family. Login and Register keep their extra marketing
 * panel; these pages are single-purpose, so they stay centred.
 */
export function AuthCard({
  title,
  subtitle,
  children,
  showBackToLogin = true,
}: {
  title: string
  subtitle?: string
  children: React.ReactNode
  showBackToLogin?: boolean
}) {
  const { isDark, toggleTheme } = useTheme()

  return (
    <div className="relative min-h-screen flex items-center justify-center bg-honey-50 dark:bg-slate-950 px-6 py-12">
      <button
        onClick={toggleTheme}
        className="absolute top-4 right-4 z-30 w-9 h-9 rounded-full flex items-center justify-center bg-white/80 dark:bg-slate-800/80 backdrop-blur border border-honey-200 dark:border-slate-700 text-gray-600 dark:text-slate-300 hover:text-honey-600 dark:hover:text-honey-300 transition-colors shadow-sm"
        aria-label={isDark ? 'Prebaci na svjetlu temu' : 'Prebaci na tamnu temu'}
        title={isDark ? 'Svjetla tema' : 'Tamna tema'}
      >
        {isDark ? <Sun className="w-[18px] h-[18px]" /> : <Moon className="w-[18px] h-[18px]" />}
      </button>

      <div className="w-full max-w-md animate-fade-in">
        <div className="text-center mb-8">
          <div className="text-5xl mb-2 animate-float">🐝</div>
          <h1 className="font-display text-3xl font-bold text-honey-800 dark:text-honey-300">Melarium</h1>
        </div>

        <div className="bg-white/90 dark:bg-slate-900/90 backdrop-blur rounded-3xl shadow-xl border border-honey-100 dark:border-slate-800 px-8 py-10">
          <div className="mb-7">
            <h2 className="font-display text-2xl font-bold text-gray-900 dark:text-slate-100">{title}</h2>
            {subtitle && <p className="text-gray-500 dark:text-slate-400 mt-1 text-sm">{subtitle}</p>}
          </div>

          {children}

          {showBackToLogin && (
            <p className="mt-6 text-center text-sm">
              <Link
                to="/login"
                className="inline-flex items-center gap-1.5 font-semibold text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300 transition-colors"
              >
                <ArrowLeft className="w-4 h-4" />
                Nazad na prijavu
              </Link>
            </p>
          )}
        </div>
      </div>
    </div>
  )
}

/** Login's input styling, shared so the whole auth flow uses one field treatment. */
export function authInputClass(hasError: boolean, withIcon = true): string {
  return clsx(
    'w-full pr-4 py-3 rounded-xl border text-sm transition-all duration-200 outline-none',
    withIcon ? 'pl-11' : 'pl-4',
    'bg-gray-50 dark:bg-slate-800 focus:bg-white dark:focus:bg-slate-800 dark:text-slate-100',
    hasError
      ? 'border-red-400 focus:ring-2 focus:ring-red-200 dark:focus:ring-red-500/30'
      : 'border-gray-200 dark:border-slate-700 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 dark:focus:ring-honey-500/20',
  )
}

/** Login's primary submit button, including the shine sweep. */
export function AuthSubmitButton({
  isSubmitting,
  label,
  busyLabel,
  icon,
}: {
  isSubmitting: boolean
  label: string
  busyLabel: string
  icon?: React.ReactNode
}) {
  return (
    <button
      type="submit"
      disabled={isSubmitting}
      className="group relative w-full overflow-hidden flex items-center justify-center gap-2
        bg-gradient-to-r from-honey-500 to-honey-600 hover:from-honey-600 hover:to-honey-700
        text-white font-semibold py-3 px-6 rounded-xl mt-2
        shadow-lg shadow-honey-500/30 hover:shadow-honey-500/40
        transition-all duration-200
        focus:outline-none focus:ring-2 focus:ring-honey-400 focus:ring-offset-2 dark:focus:ring-offset-slate-900
        disabled:opacity-60 disabled:cursor-not-allowed"
    >
      <span className="absolute inset-0 -translate-x-full group-hover:translate-x-full transition-transform duration-700 ease-out bg-gradient-to-r from-transparent via-white/25 to-transparent" />
      <span className="relative z-10 flex items-center gap-2">
        {isSubmitting ? busyLabel : label}
        {!isSubmitting && icon}
      </span>
    </button>
  )
}
