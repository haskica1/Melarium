import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Loader2, Lock, QrCode, AlertTriangle } from 'lucide-react'
import { useAuth } from '../../core/context/AuthContext'
import { beehiveService, type BeehiveScanInfo } from '../../core/services/beehiveService'

type ScanState = 'loading' | 'not-found' | 'no-access' | 'error'

export default function ScanPage() {
  const { uniqueId } = useParams<{ uniqueId: string }>()
  const { isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const [state, setState] = useState<ScanState>('loading')
  const [beehiveName, setBeehiveName] = useState<string>('')

  useEffect(() => {
    if (!uniqueId) {
      setState('not-found')
      return
    }

    let cancelled = false

    async function run() {
      // Step 1: public lookup — resolve uniqueId → { id, name }
      let info: BeehiveScanInfo | null
      try {
        info = await beehiveService.scanLookup(uniqueId!)
      } catch {
        if (!cancelled) setState('error')
        return
      }

      if (cancelled) return

      if (!info) {
        setState('not-found')
        return
      }

      setBeehiveName(info.name)

      // Step 2: if not authenticated, redirect to login and come back
      if (!isAuthenticated) {
        navigate(`/login?returnUrl=${encodeURIComponent(`/scan/${uniqueId}`)}`, { replace: true })
        return
      }

      // Step 3: check access
      let hasAccess: boolean
      try {
        hasAccess = await beehiveService.checkAccess(info.id)
      } catch {
        if (!cancelled) setState('error')
        return
      }

      if (cancelled) return

      if (hasAccess) {
        navigate(`/beehives/${info.id}`, { replace: true })
      } else {
        setState('no-access')
      }
    }

    run()
    return () => { cancelled = true }
  }, [uniqueId, isAuthenticated, navigate])

  if (state === 'loading') {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center bg-honey-50 dark:bg-slate-950 gap-4">
        <Loader2 className="w-10 h-10 text-honey-500 animate-spin" />
        <p className="text-gray-500 dark:text-slate-400 text-sm">Otvaranje košnice…</p>
      </div>
    )
  }

  if (state === 'not-found') {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center bg-honey-50 dark:bg-slate-950 px-6">
        <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-xl border border-honey-100 dark:border-slate-800 px-8 py-10 max-w-sm w-full text-center">
          <div className="flex items-center justify-center w-16 h-16 rounded-full bg-amber-50 dark:bg-amber-500/15 mx-auto mb-4">
            <QrCode className="w-8 h-8 text-amber-400" />
          </div>
          <h1 className="text-xl font-bold text-gray-900 dark:text-slate-100 mb-2">Košnica nije pronađena</h1>
          <p className="text-gray-500 dark:text-slate-400 text-sm">
            Ovaj QR kod ne odgovara nijednoj košnici u sistemu. Možda je uklonjena.
          </p>
          <button
            onClick={() => navigate('/', { replace: true })}
            className="mt-6 w-full py-2.5 px-4 rounded-xl bg-honey-500 hover:bg-honey-600 text-white font-semibold text-sm transition-colors"
          >
            Idi na kontrolnu ploču
          </button>
        </div>
      </div>
    )
  }

  if (state === 'no-access') {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center bg-honey-50 dark:bg-slate-950 px-6">
        <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-xl border border-honey-100 dark:border-slate-800 px-8 py-10 max-w-sm w-full text-center">
          <div className="flex items-center justify-center w-16 h-16 rounded-full bg-red-50 dark:bg-red-500/15 mx-auto mb-4">
            <Lock className="w-8 h-8 text-red-400" />
          </div>
          <h1 className="text-xl font-bold text-gray-900 dark:text-slate-100 mb-2">Pristup odbijen</h1>
          {beehiveName && (
            <p className="text-sm font-medium text-honey-700 dark:text-honey-400 mb-2">{beehiveName}</p>
          )}
          <p className="text-gray-500 dark:text-slate-400 text-sm">
            Nemate ovlaštenje za pregled ove košnice. Kontaktirajte svog administratora da zatražite pristup.
          </p>
          <button
            onClick={() => navigate('/', { replace: true })}
            className="mt-6 w-full py-2.5 px-4 rounded-xl bg-honey-500 hover:bg-honey-600 text-white font-semibold text-sm transition-colors"
          >
            Idi na kontrolnu ploču
          </button>
        </div>
      </div>
    )
  }

  // error state
  return (
    <div className="min-h-screen flex flex-col items-center justify-center bg-honey-50 dark:bg-slate-950 px-6">
      <div className="bg-white rounded-2xl shadow-xl border border-honey-100 px-8 py-10 max-w-sm w-full text-center">
        <div className="flex items-center justify-center w-16 h-16 rounded-full bg-orange-50 dark:bg-orange-500/15 mx-auto mb-4">
          <AlertTriangle className="w-8 h-8 text-orange-400" />
        </div>
        <h1 className="text-xl font-bold text-gray-900 mb-2">Nešto je pošlo po krivu</h1>
        <p className="text-gray-500 dark:text-slate-400 text-sm">
          Nije moguće otvoriti košnicu trenutno. Provjerite vašu vezu i pokušajte ponovo.
        </p>
        <button
          onClick={() => window.location.reload()}
          className="mt-6 w-full py-2.5 px-4 rounded-xl bg-honey-500 hover:bg-honey-600 text-white font-semibold text-sm transition-colors"
        >
          Pokušaj ponovo
        </button>
      </div>
    </div>
  )
}
