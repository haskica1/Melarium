import { Droplets } from 'lucide-react'
import { MonthLabels, type MonthYield } from '../../core/models'
import DashboardCard, { CardEmpty } from './DashboardCard'
import { formatKg } from './format'

/**
 * Honey per month, this year against last. Plain bars instead of the chart library: this is the start
 * page, and recharts would add its whole chunk to the first thing every user downloads.
 */
export default function YieldCard({ months, year }: { months: MonthYield[]; year: number }) {
  const max = Math.max(1, ...months.flatMap(m => [m.thisYearKg, m.lastYearKg]))

  return (
    <DashboardCard icon={<Droplets className="w-4 h-4" />} title="Prinos po mjesecima" action={{ to: '/harvests', label: 'Vrcanja' }}>
      {months.length === 0 ? (
        <CardEmpty icon="🍯">Još nema vrcanja ove ni prošle godine.</CardEmpty>
      ) : (
        <>
          <div className="flex gap-4 text-xs text-gray-600 dark:text-slate-400 mb-3">
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-sm bg-honey-500" />{year}.</span>
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-sm bg-gray-300 dark:bg-slate-600" />{year - 1}.</span>
          </div>
          <div className="flex items-end gap-3 h-32 border-b border-honey-100 dark:border-slate-800" role="img"
               aria-label={months.map(m => `${MonthLabels[m.month - 1]}: ${formatKg(m.thisYearKg)} ove, ${formatKg(m.lastYearKg)} prošle godine`).join('; ')}>
            {months.map(m => (
              <div key={m.month} className="flex-1 h-full flex items-end justify-center gap-0.5 min-w-0">
                <Bar value={m.thisYearKg} max={max} className="bg-honey-500" title={`${MonthLabels[m.month - 1]} ${year}.: ${formatKg(m.thisYearKg)}`} />
                <Bar value={m.lastYearKg} max={max} className="bg-gray-300 dark:bg-slate-600" title={`${MonthLabels[m.month - 1]} ${year - 1}.: ${formatKg(m.lastYearKg)}`} />
              </div>
            ))}
          </div>
          <div className="flex gap-3 mt-1.5">
            {months.map(m => (
              <span key={m.month} className="flex-1 text-center text-[11px] text-gray-500 dark:text-slate-400 truncate">
                {MonthLabels[m.month - 1].slice(0, 3).toLowerCase()}
              </span>
            ))}
          </div>
        </>
      )}
    </DashboardCard>
  )
}

function Bar({ value, max, className, title }: { value: number; max: number; className: string; title: string }) {
  return (
    <div
      className={`w-full max-w-[18px] rounded-t ${className} transition-[height] duration-700`}
      style={{ height: `${value > 0 ? Math.max((value / max) * 100, 3) : 0}%` }}
      title={title}
    />
  )
}
