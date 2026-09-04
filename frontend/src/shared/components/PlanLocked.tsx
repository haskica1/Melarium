import { Lock } from 'lucide-react'

/**
 * The locked-row treatment for apiaries and hives the plan no longer reaches (SPEC-24).
 *
 * A locked row is deliberately still on screen — that is the whole difference between a lock and a
 * deletion, and it is what tells a beekeeper what an upgrade brings back. What it must not do is
 * look like a working row: no hover lift, no pointer, and none of the numbers, because the server
 * already stripped them and rendering a zero would be worse than rendering nothing.
 */

/** The message shown when someone taps a locked row. Matches the server's 402 wording. */
export const LOCKED_APIARY_MESSAGE =
  'Ovaj pčelinjak je zaključan jer prelazi ograničenje vašeg paketa. Podaci nisu obrisani — nadogradite paket da mu ponovo pristupite.'

export const LOCKED_BEEHIVE_MESSAGE =
  'Ova košnica je zaključana jer prelazi ograničenje vašeg paketa. Podaci nisu obrisani — nadogradite paket da joj ponovo pristupite.'

/**
 * Opens the same upsell modal a 402 would, without making a request that is certain to fail.
 * `UpsellModal` listens for this event globally (SPEC-09).
 */
export function showLockedUpsell(message: string) {
  window.dispatchEvent(new CustomEvent('plan-limit', { detail: message }))
}

/** Small "Zaključano" pill for a locked card or row. */
export function LockedBadge({ className = '' }: { className?: string }) {
  return (
    <span
      className={`badge bg-gray-200 text-gray-600 dark:bg-slate-700 dark:text-slate-300 gap-1 ${className}`}
    >
      <Lock className="w-3 h-3" /> Zaključano
    </span>
  )
}

/**
 * Standing explanation above a list that contains locked rows, so the padlocks are not a mystery.
 * Renders nothing when there is nothing locked.
 */
export function PlanLockNotice({ locked, kind }: { locked: number; kind: 'apiaries' | 'beehives' }) {
  if (locked <= 0) return null

  const what = kind === 'apiaries'
    ? `${locked} ${locked === 1 ? 'pčelinjak je' : 'pčelinjaka je'}`
    : `${locked} ${locked === 1 ? 'košnica je' : 'košnica je'}`

  return (
    <div className="flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-500/30 dark:bg-amber-500/10">
      <Lock className="mt-0.5 h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400" />
      <div className="text-sm text-amber-900 dark:text-amber-200">
        <p className="font-medium">{what} zaključano jer prelazi ograničenje vašeg paketa.</p>
        <p className="mt-0.5 text-amber-800/80 dark:text-amber-200/70">
          Podaci nisu obrisani i vraćaju se čim nadogradite paket.{' '}
          <a href="/plans" className="font-medium underline underline-offset-2">
            Pogledaj pakete
          </a>
        </p>
      </div>
    </div>
  )
}
