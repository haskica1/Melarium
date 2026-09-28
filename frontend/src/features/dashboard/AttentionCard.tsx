import { Link } from 'react-router-dom'
import { AlertTriangle, ChevronRight } from 'lucide-react'
import clsx from 'clsx'
import type { ApiaryWeather, AttentionItem } from '../../core/models'
import DashboardCard, { CardEmpty } from './DashboardCard'

const KIND_ICONS: Record<string, string> = {
  InspectionOverdue:     '⏰',
  HoneyLevelDrop:        '📉',
  StripsLeftIn:          '💊',
  TreatmentRoundOverdue: '💊',
  FeedingOverdue:        '🍯',
  FrostWarning:          '❄️',
}

/**
 * What needs doing now (SPEC-29), grouped by apiary — the same rules and season as the alerts, but
 * computed live, so it empties itself the moment the work is done instead of waiting for a
 * notification to be read. Frost comes from the forecast request and joins when it arrives.
 */
export default function AttentionCard({
  items,
  weather,
  resting,
}: {
  items: AttentionItem[]
  weather?: ApiaryWeather[]
  resting: boolean
}) {
  const frost: AttentionItem[] = (weather ?? [])
    .filter(w => w.frostPriority)
    .map(w => ({
      kind: 'FrostWarning',
      priority: w.frostPriority!,
      apiaryId: w.apiaryId,
      apiaryName: w.apiaryName,
      text: `Mraz u naredna 2 dana (${formatTemp(w.frostMinTemp)})`,
      items: [],
      linkPath: `/apiaries/${w.apiaryId}`,
    }))

  const all = [...frost.filter(f => f.priority === 'Critical'), ...items, ...frost.filter(f => f.priority !== 'Critical')]

  return (
    <DashboardCard
      icon={<AlertTriangle className="w-4 h-4" />}
      title="Traži pažnju"
      badge={all.length > 0 && (
        <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-honey-100 text-honey-800 dark:bg-honey-500/15 dark:text-honey-300">
          {all.length}
        </span>
      )}
    >
      {all.length === 0 ? (
        resting
          ? <CardEmpty icon="❄️">Košnice miruju — do proljeća se pregledi ne prate.</CardEmpty>
          : <CardEmpty icon="✅">Sve je u redu — ništa ne kasni.</CardEmpty>
      ) : (
        <ul className="-mx-1 divide-y divide-honey-100 dark:divide-slate-800">
          {all.map((item, i) => (
            <li key={`${item.kind}-${item.apiaryId}-${i}`}>
              <Link
                to={item.linkPath}
                className={clsx(
                  'flex items-start gap-3 px-1 py-2.5 rounded-lg hover:bg-honey-50 dark:hover:bg-slate-800/60 transition-colors',
                  item.priority === 'Critical' && 'bg-red-50/70 dark:bg-red-500/10',
                )}
              >
                <span className="text-lg leading-none mt-0.5" aria-hidden>{KIND_ICONS[item.kind] ?? '🔔'}</span>
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                    <span className="text-sm font-medium text-gray-900 dark:text-slate-100">{item.text}</span>
                    {item.priority === 'Critical' && (
                      <span className="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase tracking-wide bg-red-600 text-white">
                        Kritično
                      </span>
                    )}
                  </span>
                  <span className="block text-xs text-gray-500 dark:text-slate-400 mt-0.5">
                    {item.apiaryName}
                    {item.items.length > 0 && <> · {item.items.join(', ')}</>}
                  </span>
                </span>
                <ChevronRight className="w-4 h-4 text-gray-300 dark:text-slate-600 mt-1 shrink-0" />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </DashboardCard>
  )
}

function formatTemp(t?: number | null): string {
  return t == null ? '< 0 °C' : `${t.toLocaleString('bs-BA', { maximumFractionDigits: 1 })} °C`
}
