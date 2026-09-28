import { Link } from 'react-router-dom'
import { ListChecks } from 'lucide-react'
import type { Programme } from '../../core/models'
import ProgressRing from '../../shared/components/ProgressRing'
import DashboardCard, { CardEmpty } from './DashboardCard'
import { dayMonth, relativeDay } from './format'

const KIND = {
  Feeding:         { icon: '🍯', label: 'Prehrana' },
  TreatmentRounds: { icon: '💊', label: 'Tretman' },
  Strips:          { icon: '🎗️', label: 'Trake' },
  Karenca:         { icon: '⏳', label: 'Karenca' },
} as const

/** Work that runs over weeks: feeding and treatment rounds as steps, strips and karenca as dates. */
export default function ProgrammesCard({ programmes }: { programmes: Programme[] }) {
  return (
    <DashboardCard icon={<ListChecks className="w-4 h-4" />} title="Programi u toku">
      {programmes.length === 0 ? (
        <CardEmpty icon="🌿">Nema prehrane ni tretmana u toku.</CardEmpty>
      ) : (
        <ul className="space-y-3">
          {programmes.map(p => (
            <li key={`${p.kind}-${p.id}`}>
              <Link to={p.linkPath} className="flex items-center gap-3 rounded-xl -mx-1 px-1 py-1 hover:bg-honey-50 dark:hover:bg-slate-800/60">
                {p.total ? (
                  <ProgressRing
                    segments={[{ value: p.done ?? 0, strokeClassName: 'stroke-honey-500' }]}
                    total={p.total}
                    center={`${p.done ?? 0}/${p.total}`}
                    size={52}
                    thickness={6}
                    label={`${KIND[p.kind].label}: ${p.done ?? 0} od ${p.total} rundi`}
                  />
                ) : (
                  <span className="w-[52px] h-[52px] shrink-0 rounded-full bg-honey-50 dark:bg-slate-800 flex items-center justify-center text-xl">
                    {KIND[p.kind].icon}
                  </span>
                )}
                <span className="min-w-0 flex-1">
                  <span className="block text-sm font-medium text-gray-900 dark:text-slate-100 truncate">
                    {KIND[p.kind].label}: {p.title}
                  </span>
                  <span className="block text-xs text-gray-500 dark:text-slate-400 truncate">
                    {p.apiaryName}
                    {p.date && <> · {detail(p)}</>}
                  </span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </DashboardCard>
  )
}

function detail(p: Programme): string {
  const date = p.date!
  switch (p.kind) {
    case 'Strips':  return `izvaditi do ${dayMonth(date)}`
    case 'Karenca': return `vrcanje od ${dayMonth(date)}`
    default:        return `sljedeća runda: ${relativeDay(date).toLowerCase()}`
  }
}
