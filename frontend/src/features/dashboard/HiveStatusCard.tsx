import { Link } from 'react-router-dom'
import { ClipboardCheck } from 'lucide-react'
import type { HiveStatus } from '../../core/models'
import ProgressRing from '../../shared/components/ProgressRing'
import DashboardCard, { CardEmpty } from './DashboardCard'
import { count, dayMonth } from './format'

const LEGEND = [
  { key: 'inTime', label: 'u roku', dot: 'bg-emerald-500', stroke: 'stroke-emerald-500' },
  { key: 'late', label: 'kasni', dot: 'bg-amber-400', stroke: 'stroke-amber-400' },
  { key: 'never', label: 'nikad', dot: 'bg-gray-300 dark:bg-slate-600', stroke: 'stroke-gray-300 dark:stroke-slate-600' },
] as const

/**
 * Hives by inspection state, judged by the season's threshold — the same one the alerts use
 * (SPEC-29). In winter there is nothing to be late with, and the card says so instead of drawing
 * a chart of hives nobody should open.
 */
export default function HiveStatusCard({
  statuses,
  resting,
  springStart,
}: {
  statuses: HiveStatus[]
  resting: boolean
  springStart?: string
}) {
  const totals = statuses.reduce(
    (t, s) => ({ inTime: t.inTime + s.inTime, late: t.late + s.late, never: t.never + s.never }),
    { inTime: 0, late: 0, never: 0 },
  )
  const total = totals.inTime + totals.late + totals.never

  return (
    <DashboardCard icon={<ClipboardCheck className="w-4 h-4" />} title="Stanje košnica">
      {total === 0 ? (
        <CardEmpty icon="🐝">Još nema košnica.</CardEmpty>
      ) : resting ? (
        <div className="flex items-center gap-4">
          <ProgressRing
            segments={[{ value: total, strokeClassName: 'stroke-sky-300 dark:stroke-sky-500/60' }]}
            total={total}
            center="❄️"
            sub={count(total, 'košnica', 'košnice', 'košnica')}
            label={`Zimsko mirovanje, ${total} košnica`}
          />
          <p className="text-sm text-gray-600 dark:text-slate-400">
            Zimsko mirovanje — pregledi se ne prate
            {springStart && <> do <span className="font-medium text-gray-800 dark:text-slate-200">{dayMonth(springStart)}</span></>}.
            Tada sat kreće iznova, pa zima ne ulazi u „dana bez pregleda“.
          </p>
        </div>
      ) : (
        <div className="flex flex-col sm:flex-row sm:items-center gap-5">
          <div className="flex items-center gap-4">
            <ProgressRing
              segments={LEGEND.map(l => ({ value: totals[l.key], strokeClassName: l.stroke }))}
              total={total}
              center={`${totals.inTime}/${total}`}
              sub="u roku"
              size={104}
              label={`${totals.inTime} od ${total} košnica pregledano u roku, ${totals.late} kasni, ${totals.never} nikad pregledano`}
            />
            <ul className="space-y-1 text-xs text-gray-600 dark:text-slate-400">
              {LEGEND.map(l => (
                <li key={l.key} className="flex items-center gap-2">
                  <span className={`w-2.5 h-2.5 rounded-sm ${l.dot}`} />
                  <span className="tabular-nums font-medium text-gray-800 dark:text-slate-200">{totals[l.key]}</span> {l.label}
                </li>
              ))}
            </ul>
          </div>

          {statuses.length > 1 && (
            <ul className="flex-1 min-w-0 space-y-2">
              {statuses.map(s => {
                const sum = s.inTime + s.late + s.never
                return (
                  <li key={s.apiaryId}>
                    <Link to={`/apiaries/${s.apiaryId}`} className="block group">
                      <div className="flex justify-between text-xs mb-1">
                        <span className="truncate text-gray-700 dark:text-slate-300 group-hover:text-honey-700 dark:group-hover:text-honey-300">{s.apiaryName}</span>
                        <span className="tabular-nums text-gray-500 dark:text-slate-400">{s.inTime}/{sum}</span>
                      </div>
                      <div className="flex h-2 gap-0.5 rounded-full overflow-hidden bg-honey-100 dark:bg-slate-800">
                        {LEGEND.map(l => s[l.key] > 0 && (
                          <span key={l.key} className={l.dot} style={{ flex: s[l.key] }} />
                        ))}
                      </div>
                    </Link>
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      )}
    </DashboardCard>
  )
}
