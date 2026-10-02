import { Link } from 'react-router-dom'
import { Lock } from 'lucide-react'

/**
 * Shown to managers whose plan does not include products other than honey (SPEC-30). Honey stays on
 * every plan; the rest is read-only there — a Free organization keeps what it recorded, and a past
 * season's report stays whole — so this explains the missing buttons rather than hiding the records.
 */
export function ProductsReadOnlyNotice() {
  return (
    <div className="flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-500/30 dark:bg-amber-500/10">
      <Lock className="mt-0.5 h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400" />
      <div className="text-sm text-amber-900 dark:text-amber-200">
        <p className="font-medium">Med unosite na svakom paketu. Vosak, propolis i ostali proizvodi su dio paketa Standard, Pro i Max.</p>
        <p className="mt-0.5 text-amber-800/80 dark:text-amber-200/70">
          Postojeće zapise ostalih proizvoda i dalje možete pregledati i obrisati, a ulaze i u statistiku i izvještaje.{' '}
          <Link to="/plans" className="font-medium underline underline-offset-2">
            Pogledaj pakete
          </Link>
        </p>
      </div>
    </div>
  )
}
