import clsx from 'clsx'
import { parseISO, startOfToday, differenceInCalendarDays } from 'date-fns'
import { SeasonPhaseLabels, SeasonPhaseShortLabels, type DashboardSeason } from '../../core/models'
import { SEASON_ICONS, count, dayMonth, daysFromToday, greeting, todayLong } from './format'

/**
 * Greeting + the beekeeping year as five steps (SPEC-29). Only the phase and the date the next one
 * starts — the task list for a phase arrives once, as a notification, when the phase begins.
 */
export default function SeasonHero({
  firstName,
  season,
  todayCount,
  attentionCount,
}: {
  firstName?: string
  season: DashboardSeason
  todayCount: number
  attentionCount: number
}) {
  const currentIndex = season.phases.findIndex(p => p.phase === season.phase)
  const nextIn = daysFromToday(season.nextStart)

  const summary = [
    todayCount > 0 ? `danas ${count(todayCount, 'obaveza', 'obaveze', 'obaveza')}` : 'danas bez obaveza',
    // A colon, not a verb: "1 stvar traži / 3 stvari traže" would need agreement with the number.
    attentionCount > 0 ? `traži pažnju: ${attentionCount}` : null,
  ].filter(Boolean).join(' · ')

  return (
    <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                    bg-gradient-to-br from-honey-100 via-white to-honey-50
                    dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
      <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
      <div className="relative p-5 sm:p-7">
        <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
          <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">
            {greeting()}{firstName ? `, ${firstName}` : ''}
          </h1>
          <p className="text-sm text-gray-600 dark:text-slate-400">
            {todayLong()} · {summary}
          </p>
        </div>

        {/* ── The five steps of the year ─────────────────────────────────────── */}
        <ol className="mt-5 grid grid-cols-5 gap-1.5" aria-label="Faze pčelarske godine">
          {season.phases.map((p, i) => {
            const past = i < currentIndex
            const current = i === currentIndex
            const progress = current ? phaseProgress(p.start, p.end) : 0
            return (
              <li key={p.phase} className="min-w-0" title={`${SeasonPhaseLabels[p.phase]}: ${dayMonth(p.start)} – ${dayMonth(p.end)}`}>
                <div
                  className={clsx(
                    'h-2 rounded-full overflow-hidden',
                    past && 'bg-honey-300 dark:bg-honey-500/60',
                    current && 'bg-honey-100 dark:bg-slate-800 ring-1 ring-honey-400',
                    !past && !current && 'bg-white/70 dark:bg-slate-800 ring-1 ring-honey-200 dark:ring-slate-700',
                  )}
                  aria-current={current ? 'step' : undefined}
                >
                  {current && <div className="h-full bg-honey-500 rounded-full" style={{ width: `${Math.max(progress, 6)}%` }} />}
                </div>
                <p className={clsx(
                  'mt-1.5 text-[11px] sm:text-xs truncate',
                  current ? 'font-semibold text-honey-800 dark:text-honey-300' : 'text-gray-500 dark:text-slate-400',
                )}>
                  {SeasonPhaseShortLabels[p.phase]}
                </p>
              </li>
            )
          })}
        </ol>

        <div className="mt-3 flex flex-wrap items-center justify-between gap-x-4 gap-y-1 text-sm">
          <p className="font-medium text-gray-800 dark:text-slate-100">
            <span className="mr-1.5">{SEASON_ICONS[season.phase]}</span>
            {SeasonPhaseLabels[season.phase]}
            <span className="font-normal text-gray-500 dark:text-slate-400"> · od {dayMonth(season.start)}</span>
          </p>
          <p className="text-gray-600 dark:text-slate-400">
            Sljedeća: <span className="font-medium text-gray-800 dark:text-slate-200">{SeasonPhaseLabels[season.nextPhase]}</span>{' '}
            od {dayMonth(season.nextStart)}
            {nextIn > 0 && <span className="text-gray-500 dark:text-slate-500"> (za {count(nextIn, 'dan', 'dana', 'dana')})</span>}
          </p>
        </div>
      </div>
    </div>
  )
}

function phaseProgress(start: string, end: string): number {
  const total = differenceInCalendarDays(parseISO(end), parseISO(start)) + 1
  const done = differenceInCalendarDays(startOfToday(), parseISO(start)) + 1
  return Math.min(100, Math.max(0, (done / total) * 100))
}
