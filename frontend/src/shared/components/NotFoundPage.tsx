import { Link, useLocation } from 'react-router-dom'
import { Compass } from 'lucide-react'
import { useAuth } from '../../core/context/AuthContext'

/**
 * Real 404 instead of a silent redirect to `/`. A stale bookmark, an old QR link or a typo used to
 * bounce the user to the home page with no explanation, which reads like the app losing their page.
 */
export default function NotFoundPage() {
  const { pathname } = useLocation()
  const { isAuthenticated, user } = useAuth()

  const homePath = !isAuthenticated ? '/login' : user?.role === 'SystemAdmin' ? '/admin' : '/apiaries'

  return (
    <div className="min-h-screen flex items-center justify-center bg-honey-50 dark:bg-slate-950 px-6 py-12">
      <div className="w-full max-w-md text-center animate-fade-in">
        <div className="text-5xl mb-2">🐝</div>
        <h1 className="font-display text-3xl font-bold text-honey-800 dark:text-honey-300 mb-8">
          Melarium
        </h1>

        <div className="bg-white/90 dark:bg-slate-900/90 backdrop-blur rounded-3xl shadow-xl border border-honey-100 dark:border-slate-800 px-8 py-10">
          <div className="w-14 h-14 mx-auto rounded-full bg-honey-100 dark:bg-honey-500/15 flex items-center justify-center mb-4">
            <Compass className="w-7 h-7 text-honey-500" />
          </div>

          <p className="font-display text-4xl font-bold text-honey-600 dark:text-honey-400">404</p>
          <h2 className="mt-2 font-display text-xl font-bold text-gray-900 dark:text-slate-100">
            Stranica nije pronađena
          </h2>
          <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">
            Adresa <code className="text-xs bg-gray-100 dark:bg-slate-800 rounded px-1.5 py-0.5 break-all">{pathname}</code>{' '}
            ne postoji. Možda je link zastario ili pogrešno prepisan.
          </p>

          <Link to={homePath} className="btn-primary w-full justify-center mt-6">
            {isAuthenticated ? 'Nazad na početnu' : 'Idi na prijavu'}
          </Link>
        </div>
      </div>
    </div>
  )
}
