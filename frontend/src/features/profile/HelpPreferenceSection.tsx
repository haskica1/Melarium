import { useState } from 'react'
import { CircleHelp } from 'lucide-react'
import { useAuth } from '../../core/context/AuthContext'
import { isAutoOpenEnabled, setAutoOpenEnabled } from '../../core/help/helpStorage'

/**
 * The one help preference. Stored per browser, not on the account — see SPEC-14's Backend section for
 * why that trade-off was accepted for v1.
 */
export default function HelpPreferenceSection() {
  const { user } = useAuth()
  const email = user?.email
  const [enabled, setEnabled] = useState(() => isAutoOpenEnabled(email))

  function toggle() {
    setEnabled(v => {
      setAutoOpenEnabled(email, !v)
      return !v
    })
  }

  return (
    <div className="card">
      <div className="flex items-center gap-2 mb-3">
        <CircleHelp className="w-4 h-4 text-honey-500" />
        <h3 className="font-semibold text-gray-700 dark:text-slate-200">Pomoć</h3>
      </div>

      <label className="flex items-start gap-3 cursor-pointer">
        <input
          type="checkbox"
          checked={enabled}
          onChange={toggle}
          className="mt-0.5 w-4 h-4 rounded border-gray-300 text-honey-600 focus:ring-honey-400"
        />
        <span className="min-w-0">
          <span className="block text-sm font-medium text-gray-800 dark:text-slate-100">
            Automatski prikaži pomoć na novim stranicama
          </span>
          <span className="block text-xs text-gray-500 dark:text-slate-400 mt-0.5">
            Pomoć možete uvijek otvoriti ikonom znaka pitanja u gornjem meniju, ili tipkom „?“ —
            i kad je ovo isključeno.
          </span>
        </span>
      </label>
    </div>
  )
}
