import { Link } from 'react-router-dom'
import { CloudSun, Snowflake } from 'lucide-react'
import clsx from 'clsx'
import type { ApiaryWeather } from '../../core/models'
import { Skeleton } from '../../shared/components'
import { wmoToIcon, wmoToLabel } from '../../shared/utils/weather'
import DashboardCard, { CardEmpty } from './DashboardCard'
import { forecastDay } from './format'

/**
 * Three days per apiary. Its own request (`/dashboard/weather`): the forecast can be slow or down,
 * and the rest of the page must not wait for it. A frost the season's rule would warn about is
 * marked — red when it is Critical, which only happens in spring and the main season.
 */
export default function WeatherCard({
  data,
  isPending,
  isError,
  apiaryCount,
}: {
  data?: ApiaryWeather[]
  isPending: boolean
  isError: boolean
  apiaryCount: number
}) {
  return (
    <DashboardCard icon={<CloudSun className="w-4 h-4" />} title="Vrijeme">
      {isPending ? (
        <div className="space-y-2">
          {Array.from({ length: Math.min(Math.max(apiaryCount, 1), 3) }).map((_, i) => <Skeleton key={i} className="h-12 rounded-xl" />)}
        </div>
      ) : isError ? (
        <CardEmpty icon="🌥️">Prognoza trenutno nije dostupna.</CardEmpty>
      ) : !data || data.length === 0 ? (
        <CardEmpty icon="📍">Unesite koordinate pčelinjaka da biste vidjeli prognozu.</CardEmpty>
      ) : (
        <ul className="divide-y divide-honey-100 dark:divide-slate-800">
          {data.map(w => (
            <li key={w.apiaryId} className="py-2.5 first:pt-0 last:pb-0">
              <div className="flex items-center justify-between gap-2 mb-1.5">
                <Link to={`/apiaries/${w.apiaryId}`} className="text-sm font-medium text-gray-800 dark:text-slate-100 truncate hover:text-honey-700 dark:hover:text-honey-300">
                  {w.apiaryName}
                </Link>
                {w.frostPriority && (
                  <span className={clsx(
                    'inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[11px] font-semibold shrink-0',
                    w.frostPriority === 'Critical'
                      ? 'bg-red-600 text-white'
                      : 'bg-sky-100 text-sky-800 dark:bg-sky-500/15 dark:text-sky-300',
                  )}>
                    <Snowflake className="w-3 h-3" /> mraz
                  </span>
                )}
              </div>
              <div className="grid grid-cols-3 gap-2">
                {w.days.map(d => (
                  <div key={d.date} className="rounded-xl bg-honey-50/70 dark:bg-slate-800/60 px-2 py-1.5 text-center" title={wmoToLabel(d.weatherCode)}>
                    <p className="text-[11px] text-gray-500 dark:text-slate-400">{forecastDay(d.date)}</p>
                    <p className="text-lg leading-tight" aria-label={wmoToLabel(d.weatherCode)}>{wmoToIcon(d.weatherCode)}</p>
                    <p className="text-xs font-medium text-gray-800 dark:text-slate-200 tabular-nums">
                      <span className={clsx((d.minTemp ?? 1) < 0 && 'text-sky-600 dark:text-sky-400')}>{round(d.minTemp)}</span>
                      <span className="text-gray-400"> / </span>
                      {round(d.maxTemp)}°
                    </p>
                  </div>
                ))}
              </div>
            </li>
          ))}
        </ul>
      )}
    </DashboardCard>
  )
}

const round = (t?: number | null) => (t == null ? '–' : Math.round(t).toString())
