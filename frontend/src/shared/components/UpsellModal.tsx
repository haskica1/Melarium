import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Sparkles, X } from 'lucide-react'

/**
 * Global upsell prompt (SPEC-09). Listens for the `plan-limit` CustomEvent emitted by the
 * apiClient 402 interceptor and shows the server's Bosnian message with a link to /plans.
 * Mounted once inside the router.
 */
export default function UpsellModal() {
  const [message, setMessage] = useState<string | null>(null)
  const navigate = useNavigate()

  useEffect(() => {
    const onPlanLimit = (e: Event) => {
      const detail = (e as CustomEvent<string>).detail
      setMessage(detail || 'Ova funkcija zahtijeva nadogradnju paketa.')
    }
    window.addEventListener('plan-limit', onPlanLimit)
    return () => window.removeEventListener('plan-limit', onPlanLimit)
  }, [])

  useEffect(() => {
    if (!message) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setMessage(null) }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [message])

  if (!message) return null

  const goToPlans = () => {
    setMessage(null)
    navigate('/plans')
  }

  return (
    <div
      className="fixed inset-0 z-[100] bg-black/50 flex items-center justify-center p-4 animate-fade-in"
      onClick={() => setMessage(null)}
    >
      <div
        className="relative w-full max-w-md bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-honey-100 dark:border-slate-800 p-6"
        onClick={e => e.stopPropagation()}
      >
        <button
          type="button"
          onClick={() => setMessage(null)}
          className="absolute top-3 right-3 p-1.5 rounded-lg text-gray-400 hover:text-gray-600 dark:hover:text-slate-300 hover:bg-gray-100 dark:hover:bg-slate-800 transition-colors"
          aria-label="Zatvori"
        >
          <X className="w-5 h-5" />
        </button>

        <div className="flex items-center justify-center w-12 h-12 rounded-full bg-honey-100 dark:bg-honey-500/15 mb-4">
          <Sparkles className="w-6 h-6 text-honey-600 dark:text-honey-400" />
        </div>

        <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 mb-2">
          Potrebna je nadogradnja paketa
        </h2>
        <p className="text-sm text-gray-600 dark:text-slate-300 leading-relaxed mb-5">
          {message}
        </p>

        <div className="flex gap-3">
          <button
            type="button"
            onClick={() => setMessage(null)}
            className="flex-1 px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
          >
            Zatvori
          </button>
          <button
            type="button"
            onClick={goToPlans}
            className="flex-1 px-4 py-2.5 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
          >
            Pogledaj pakete
          </button>
        </div>
      </div>
    </div>
  )
}
