import { format } from 'date-fns'
import { Leaf } from 'lucide-react'
import { useActiveDiets } from '../../core/services/dietQueries'
import type { DietActiveInfo } from '../../core/models'

/**
 * Groups `/feedings/active` rows by hive. The endpoint returns one flat row per
 * (hive × programme) precisely so a whole apiary costs one request — group here, never fetch per hive.
 */
export function groupActiveByHive(rows: DietActiveInfo[]): Map<number, DietActiveInfo[]> {
  const map = new Map<number, DietActiveInfo[]>()
  for (const r of rows) {
    const arr = map.get(r.beehiveId) ?? []
    arr.push(r)
    map.set(r.beehiveId, arr)
  }
  return map
}

/** Earliest of the hive's next feeding dates — a hive on two programmes gets the sooner one. */
export function earliestNextFeeding(infos: DietActiveInfo[]): string | undefined {
  return infos
    .map(i => i.nextFeedingDate)
    .filter((d): d is string => !!d)
    .sort()[0]
}

/**
 * "🌿 Aktivna prehrana" indicator for the hive detail page (SPEC-12). Renders nothing when the hive
 * is not on a programme — and, deliberately, also nothing while loading or on error: a hive badge is
 * an additive hint, and a red error box next to the hive name would be louder than the fact it
 * conveys. The card below is where a failed request has to be visible.
 */
export function ActiveFeedingBadge({ apiaryId, beehiveId }: { apiaryId: number; beehiveId: number }) {
  const { data: rows = [] } = useActiveDiets(apiaryId)

  const infos = groupActiveByHive(rows).get(beehiveId) ?? []
  if (infos.length === 0) return null

  const next = earliestNextFeeding(infos)

  return (
    <div className="mt-1.5">
      <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium
                       bg-honey-100 text-honey-800 border border-honey-200
                       dark:bg-honey-500/15 dark:text-honey-300 dark:border-honey-500/30">
        <Leaf className="w-3.5 h-3.5" />
        Aktivna prehrana
        {infos.length > 1 && <span className="opacity-70">×{infos.length}</span>}
      </span>
      {next && (
        <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
          sljedeće hranjenje {format(new Date(next), 'dd.MM.yyyy')}
        </p>
      )}
    </div>
  )
}

/** Compact leaf chip for a hive row in the apiary's hive list. */
export function ActiveFeedingChip({ infos }: { infos: DietActiveInfo[] }) {
  if (infos.length === 0) return null

  const next = earliestNextFeeding(infos)
  const title = next
    ? `Aktivna prehrana — sljedeće hranjenje ${format(new Date(next), 'dd.MM.yyyy')}`
    : 'Aktivna prehrana'

  return (
    <span
      title={title}
      aria-label={title}
      className="inline-flex items-center gap-0.5 rounded-full px-1.5 py-0.5 text-[10px] font-medium shrink-0
                 bg-honey-100 text-honey-700 border border-honey-200
                 dark:bg-honey-500/15 dark:text-honey-300 dark:border-honey-500/30"
    >
      <Leaf className="w-3 h-3" />
      {infos.length > 1 && <span>×{infos.length}</span>}
    </span>
  )
}
