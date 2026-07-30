import { Link } from 'react-router-dom'
import { Check, Circle } from 'lucide-react'
import clsx from 'clsx'
import { useStats } from '../../core/services/queries'
import { usePermissions } from '../../core/hooks/usePermissions'
import type { Apiary } from '../../core/models'

interface Step {
  label: string
  hint: string
  to: string
  done: boolean
}

/**
 * Guides a new beekeeper through the first three actions.
 *
 * Progress is **derived from real data**, never stored: whether the user has an apiary, a hive and an
 * inspection is already knowable, and deriving it avoids the whole class of "we forgot to flip the
 * flag" bugs (the reasoning behind the computed effective plan in ADR-028). It also behaves correctly
 * in cases a stored flag gets wrong — someone added to an organisation that already has apiaries is
 * not told to create their first one.
 *
 * The card removes itself once all three are done, so there is no dismissal state either.
 */
export default function FirstStepsCard({ apiaries }: { apiaries: Apiary[] }) {
  const { canManageApiaries, canManageHives } = usePermissions()

  const hasApiary = apiaries.length > 0
  const hasHive = apiaries.some(a => a.beehiveCount > 0)

  // Only the last step needs a count the apiary list doesn't carry, so the request is deferred
  // until it is the one thing still unknown.
  const { data: stats } = useStats({ enabled: hasApiary && hasHive })
  const hasInspection = (stats?.totalInspections ?? 0) > 0

  // A Beekeeper can't create apiaries or hives — showing them "napravi pčelinjak" would be advice
  // they cannot act on. Their empty state is an assignment problem, which the help panel explains.
  if (!canManageApiaries && !canManageHives) return null

  // Nothing left to guide.
  if (hasApiary && hasHive && hasInspection) return null

  const steps: Step[] = [
    {
      label: 'Napravite pčelinjak',
      hint: 'Lokacija na kojoj držite košnice.',
      to: '/apiaries/new',
      done: hasApiary,
    },
    {
      label: 'Dodajte prvu košnicu',
      hint: 'Dobija QR kod automatski.',
      to: hasApiary ? `/beehives/new?apiaryId=${apiaries[0].id}` : '/apiaries/new',
      done: hasHive,
    },
    {
      label: 'Zabilježite prvi pregled',
      hint: 'Iz pregleda se izvode upozorenja i statistika.',
      to: '/apiaries',
      done: hasInspection,
    },
  ]

  const doneCount = steps.filter(s => s.done).length
  const next = steps.find(s => !s.done)

  return (
    <section className="rounded-2xl border border-honey-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-5 shadow-sm dark:shadow-none">
      <div className="flex items-center gap-2 mb-3">
        <h2 className="font-display text-base font-semibold text-gray-900 dark:text-slate-100">
          Prvi koraci
        </h2>
        <span className="text-xs text-gray-500 dark:text-slate-400">{doneCount}/3</span>
      </div>

      <ol className="space-y-2">
        {steps.map(step => {
          const isNext = step === next
          return (
            <li key={step.label}>
              <div className="flex items-start gap-2.5">
                {step.done ? (
                  <span className="shrink-0 w-5 h-5 rounded-full bg-emerald-100 dark:bg-emerald-500/20 flex items-center justify-center mt-0.5">
                    <Check className="w-3 h-3 text-emerald-600 dark:text-emerald-400" />
                  </span>
                ) : (
                  <Circle
                    className={clsx(
                      'shrink-0 w-5 h-5 mt-0.5',
                      isNext ? 'text-honey-500' : 'text-gray-300 dark:text-slate-600',
                    )}
                  />
                )}
                <div className="min-w-0">
                  <p
                    className={clsx(
                      'text-sm',
                      step.done
                        ? 'text-gray-400 dark:text-slate-500 line-through'
                        : 'font-medium text-gray-800 dark:text-slate-100',
                    )}
                  >
                    {step.label}
                  </p>
                  {isNext && (
                    <>
                      <p className="text-xs text-gray-500 dark:text-slate-400 mt-0.5">{step.hint}</p>
                      <Link
                        to={step.to}
                        className="inline-block mt-1.5 text-sm font-medium text-honey-700 dark:text-honey-300 hover:underline"
                      >
                        {step.label} →
                      </Link>
                    </>
                  )}
                </div>
              </div>
            </li>
          )
        })}
      </ol>
    </section>
  )
}
