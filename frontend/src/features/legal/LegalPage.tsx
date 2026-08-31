import { Link, useLocation } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import { CONTACT_EMAIL } from '../../core/contact/contactInfo'

/**
 * Shared shell for the public legal pages (`/privatnost`, `/uslovi`).
 *
 * These live outside `ProtectedRoute` and outside `Layout` — the app stores open them without an
 * account, and so does anyone deciding whether to register — so they cannot inherit the app's
 * chrome and have to draw their own. One shell rather than two copies, because the second copy is
 * the one that stops matching.
 */

interface LegalPageProps {
  title: string
  icon: React.ReactNode
  /** Human-readable date of the last substantive change, e.g. "31. augusta 2026." */
  lastUpdated: string
  children: React.ReactNode
}

/** The two pages that cross-link to each other in the footer. */
const LEGAL_LINKS = [
  { to: '/privatnost', label: 'Politika privatnosti' },
  { to: '/uslovi', label: 'Uslovi korištenja' },
] as const

export function LegalPage({ title, icon, lastUpdated, children }: LegalPageProps) {
  const { pathname } = useLocation()
  const others = LEGAL_LINKS.filter(l => l.to !== pathname)

  return (
    <div className="min-h-screen bg-gradient-to-b from-honey-50 to-white dark:from-slate-950 dark:to-slate-900">
      <div className="max-w-3xl mx-auto px-4 py-10 sm:py-14">

        <Link
          to="/login"
          className="inline-flex items-center gap-1.5 text-sm text-gray-500 dark:text-slate-400
                     hover:text-honey-600 dark:hover:text-honey-400 transition-colors mb-8"
        >
          <ArrowLeft className="w-4 h-4" />
          Nazad na prijavu
        </Link>

        <header className="mb-10">
          <div className="flex items-center gap-3 mb-3">
            <span className="flex items-center justify-center w-11 h-11 rounded-2xl bg-honey-100 dark:bg-honey-500/15">
              {icon}
            </span>
            <h1 className="font-display text-3xl font-bold text-gray-900 dark:text-slate-100">
              {title}
            </h1>
          </div>
          <p className="text-sm text-gray-500 dark:text-slate-400">
            Posljednja izmjena: {lastUpdated}
          </p>
        </header>

        <div className="space-y-8 text-[15px] leading-relaxed text-gray-700 dark:text-slate-300">
          {children}
        </div>

        <footer className="mt-12 pt-6 border-t border-gray-200 dark:border-slate-800
                           text-sm text-gray-500 dark:text-slate-400 space-y-2">
          <p>
            Imate pitanje? Pišite na{' '}
            <a className="link" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>.
          </p>
          {others.length > 0 && (
            <p className="flex flex-wrap gap-x-4 gap-y-1">
              {others.map(l => (
                <Link key={l.to} to={l.to} className="link">{l.label}</Link>
              ))}
            </p>
          )}
        </footer>
      </div>
    </div>
  )
}

export function LegalSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section>
      <h2 className="font-display text-xl font-bold text-gray-900 dark:text-slate-100 mb-2">
        {title}
      </h2>
      {children}
    </section>
  )
}

export function LegalSubTitle({ children }: { children: React.ReactNode }) {
  return (
    <h3 className="font-semibold text-gray-800 dark:text-slate-200 mt-4 mb-1">{children}</h3>
  )
}
