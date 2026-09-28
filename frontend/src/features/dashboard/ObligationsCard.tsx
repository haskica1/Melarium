import { Link } from 'react-router-dom'
import { CalendarDays } from 'lucide-react'
import clsx from 'clsx'
import type { DashboardObligation } from '../../core/models'
import DashboardCard, { CardEmpty } from './DashboardCard'
import { daysFromToday, relativeDay } from './format'

/** Today and the next seven days, from the same source as the agenda and the ICS feed (SPEC-11). */
export default function ObligationsCard({ items }: { items: DashboardObligation[] }) {
  const byDay = new Map<string, DashboardObligation[]>()
  for (const item of items) byDay.set(item.date, [...(byDay.get(item.date) ?? []), item])

  return (
    <DashboardCard icon={<CalendarDays className="w-4 h-4" />} title="Obaveze" action={{ to: '/calendar', label: 'Kalendar' }}>
      {items.length === 0 ? (
        <CardEmpty icon="🗓️">Nema obaveza u narednih 7 dana.</CardEmpty>
      ) : (
        <div className="space-y-3">
          {[...byDay.entries()].map(([date, list]) => {
            const today = daysFromToday(date) <= 0
            return (
              <div key={date}>
                <p className={clsx(
                  'text-xs font-semibold uppercase tracking-wide mb-1',
                  today ? 'text-honey-700 dark:text-honey-400' : 'text-gray-400 dark:text-slate-500',
                )}>
                  {relativeDay(date)}
                </p>
                <ul className="space-y-0.5">
                  {list.map((o, i) => (
                    <li key={`${o.kind}-${i}`}>
                      <Link
                        to={o.linkPath}
                        className="block px-2 py-1.5 -mx-2 rounded-lg text-sm text-gray-800 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-800/60 truncate"
                      >
                        {o.title}
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>
            )
          })}
        </div>
      )}
    </DashboardCard>
  )
}
