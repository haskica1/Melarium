import { useMemo, useState } from 'react'
import {
  addDays, addMonths, endOfMonth, endOfWeek, format,
  isSameDay, isSameMonth, isToday, isPast, parseISO,
  startOfMonth, startOfWeek, subMonths,
} from 'date-fns'
import { ChevronLeft, ChevronRight as ChevronRightIcon, CheckSquare, Droplets, CalendarDays, AlertCircle, Clock, CalendarPlus } from 'lucide-react'
import { Link } from 'react-router-dom'
import clsx from 'clsx'
import { useCalendarEvents } from '../../core/services/queries'
import { FeedingEntryStatus, TodoPriority } from '../../core/models'
import type { CalendarTodo, CalendarFeedingEntry } from '../../core/models'
import { ErrorMessage, VitalCard, PageSkeleton } from '../../shared/components'

// ── Types ──────────────────────────────────────────────────────────────────────

interface DayEvents {
  todos: CalendarTodo[]
  feedings: CalendarFeedingEntry[]
}

// ── Main Page ──────────────────────────────────────────────────────────────────

export default function CalendarPage() {
  const [currentMonth, setCurrentMonth] = useState(new Date())
  const [selectedDay, setSelectedDay] = useState<Date>(new Date())

  const { data, isLoading, isError } = useCalendarEvents()

  const calendarDays = useMemo(() => {
    const start = startOfWeek(startOfMonth(currentMonth), { weekStartsOn: 1 })
    const end   = endOfWeek(endOfMonth(currentMonth),     { weekStartsOn: 1 })
    const days: Date[] = []
    let cur = start
    while (cur <= end) {
      days.push(cur)
      cur = addDays(cur, 1)
    }
    return days
  }, [currentMonth])

  const eventsByDate = useMemo<Map<string, DayEvents>>(() => {
    const map = new Map<string, DayEvents>()
    if (!data) return map

    for (const todo of data.todos) {
      if (!todo.dueDate) continue
      const key = todo.dueDate.slice(0, 10)
      if (!map.has(key)) map.set(key, { todos: [], feedings: [] })
      map.get(key)!.todos.push(todo)
    }
    for (const entry of data.feedingEntries) {
      const key = entry.scheduledDate.slice(0, 10)
      if (!map.has(key)) map.set(key, { todos: [], feedings: [] })
      map.get(key)!.feedings.push(entry)
    }
    return map
  }, [data])

  const monthSummary = useMemo(() => {
    if (!data) return { todos: 0, feedings: 0 }
    const prefix = format(currentMonth, 'yyyy-MM')
    let todos = 0, feedings = 0
    for (const [key, events] of eventsByDate) {
      if (!key.startsWith(prefix)) continue
      todos    += events.todos.length
      feedings += events.feedings.length
    }
    return { todos, feedings }
  }, [data, eventsByDate, currentMonth])

  const overdueCount = useMemo(() => {
    if (!data) return 0
    const isOverdue = (dateStr: string) => { const d = parseISO(dateStr); return isPast(d) && !isToday(d) }
    const t = data.todos.filter(td => td.dueDate && !td.isCompleted && isOverdue(td.dueDate)).length
    const f = data.feedingEntries.filter(fe => fe.status !== FeedingEntryStatus.Completed && isOverdue(fe.scheduledDate)).length
    return t + f
  }, [data])

  const todayCount = useMemo(() => {
    const ev = eventsByDate.get(format(new Date(), 'yyyy-MM-dd'))
    return (ev?.todos.length ?? 0) + (ev?.feedings.length ?? 0)
  }, [eventsByDate])

  const selectedKey    = format(selectedDay, 'yyyy-MM-dd')
  const selectedEvents = eventsByDate.get(selectedKey) ?? { todos: [], feedings: [] }

  if (isLoading) return <PageSkeleton />
  if (isError)   return <ErrorMessage message="Greška pri učitavanju kalendarskih događaja." />

  return (
    <div className="space-y-6 animate-fade-in">

      {/* ── Hero ──────────────────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex items-center gap-4">
          <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
            📅
          </div>
          <div className="min-w-0">
            <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Kalendar</h1>
            <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">Vaši zadaci i rasporedi hranjenja na jednom mjestu</p>
          </div>
          <Link
            to="/calendar/settings"
            className="ml-auto shrink-0 inline-flex items-center gap-2 px-3 py-2 rounded-xl text-sm font-medium bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 text-honey-800 dark:text-honey-300 hover:bg-white dark:hover:bg-slate-700 transition-colors"
          >
            <CalendarPlus className="w-4 h-4" />
            <span className="hidden sm:inline">Poveži kalendar</span>
          </Link>
        </div>
      </div>

      {/* ── Vitals strip ──────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
        <VitalCard icon="📋" label="Zadaci"    value={String(monthSummary.todos)}    sub={format(currentMonth, 'MMMM')} gradient="from-honey-400 to-honey-600" />
        <VitalCard icon="🌿" label="Hranjenja" value={String(monthSummary.feedings)} sub={format(currentMonth, 'MMMM')} gradient="from-emerald-400 to-teal-600" />
        <VitalCard icon="⚠️" label="Zakasnjelo" value={String(overdueCount)} sub={overdueCount > 0 ? 'treba pažnju' : 'sve u redu'} gradient={overdueCount > 0 ? 'from-red-400 to-rose-600' : 'from-slate-400 to-slate-500'} />
        <VitalCard icon="📌" label="Danas"     value={String(todayCount)}    sub={format(new Date(), 'EEE, MMM d')} gradient="from-sky-400 to-blue-600" />
      </div>

      {/* ── Bento: calendar + selected day ────────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">

        {/* Calendar */}
        <div className="lg:col-span-7 xl:col-span-8">

      {/* ── Calendar card ────────────────────────────────────────────────────── */}
      <div className="card p-4 sm:p-6">

        {/* Month navigation */}
        <div className="flex items-center justify-between mb-5">
          <button
            onClick={() => setCurrentMonth(m => subMonths(m, 1))}
            className="p-2 rounded-xl text-gray-500 dark:text-slate-400 hover:bg-honey-100 dark:hover:bg-slate-800 hover:text-honey-800 dark:hover:text-honey-300 transition-colors"
            aria-label="Prethodni mjesec"
          >
            <ChevronLeft className="w-5 h-5" />
          </button>
          <h2 className="font-display text-xl font-bold text-gray-800 dark:text-slate-100 select-none">
            {format(currentMonth, 'MMMM yyyy')}
          </h2>
          <button
            onClick={() => setCurrentMonth(m => addMonths(m, 1))}
            className="p-2 rounded-xl text-gray-500 dark:text-slate-400 hover:bg-honey-100 dark:hover:bg-slate-800 hover:text-honey-800 dark:hover:text-honey-300 transition-colors"
            aria-label="Sljedeći mjesec"
          >
            <ChevronRightIcon className="w-5 h-5" />
          </button>
        </div>

        {/* Day-of-week header */}
        <div className="grid grid-cols-7 mb-2">
          {['Pon', 'Uto', 'Sri', 'Čet', 'Pet', 'Sub', 'Ned'].map(d => (
            <div key={d} className="text-center text-[11px] font-bold text-gray-400 dark:text-slate-500 uppercase tracking-wide py-1">
              {d}
            </div>
          ))}
        </div>

        {/* Calendar grid */}
        <div className="grid grid-cols-7 gap-1">
          {calendarDays.map(day => {
            const key          = format(day, 'yyyy-MM-dd')
            const events       = eventsByDate.get(key)
            const todosCount   = events?.todos.length ?? 0
            const feedingCount = events?.feedings.length ?? 0
            const inMonth      = isSameMonth(day, currentMonth)
            const selected     = isSameDay(day, selectedDay)
            const todayDate    = isToday(day)

            const hasOverdue = events?.todos.some(t => !t.isCompleted && isPast(parseISO(t.dueDate!)))
              || events?.feedings.some(f => f.status !== FeedingEntryStatus.Completed && isPast(parseISO(f.scheduledDate)))

            return (
              <button
                key={key}
                onClick={() => setSelectedDay(day)}
                className={clsx(
                  'relative flex flex-col items-center justify-start pt-1.5 pb-2 px-0.5 rounded-xl transition-all min-h-[56px]',
                  !inMonth && 'opacity-25 pointer-events-none',
                  selected
                    ? 'bg-honey-500 shadow-honey shadow-sm'
                    : todayDate
                    ? 'bg-honey-50 dark:bg-honey-500/15 ring-2 ring-honey-400 dark:ring-honey-500/50 ring-inset'
                    : 'hover:bg-honey-50 dark:hover:bg-slate-800',
                )}
              >
                {/* Day number */}
                <span className={clsx(
                  'text-sm font-semibold leading-none',
                  selected  ? 'text-white'       :
                  todayDate ? 'text-honey-700 dark:text-honey-300'   :
                              'text-gray-700 dark:text-slate-300',
                )}>
                  {format(day, 'd')}
                </span>

                {/* Event dots */}
                {(todosCount > 0 || feedingCount > 0) && (
                  <div className="flex items-center justify-center gap-0.5 mt-1.5 flex-wrap">
                    {todosCount > 0 && (
                      <span className={clsx(
                        'w-1.5 h-1.5 rounded-full flex-shrink-0',
                        selected
                          ? 'bg-white/80'
                          : hasOverdue
                          ? 'bg-red-500'
                          : 'bg-honey-500',
                      )} />
                    )}
                    {feedingCount > 0 && (
                      <span className={clsx(
                        'w-1.5 h-1.5 rounded-full flex-shrink-0',
                        selected ? 'bg-white/70' : 'bg-emerald-500',
                      )} />
                    )}
                  </div>
                )}
              </button>
            )
          })}
        </div>

        {/* Legend */}
        <div className="flex items-center gap-5 mt-5 pt-4 border-t border-gray-100 dark:border-slate-800">
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-honey-500" />
            <span className="text-xs text-gray-500 dark:text-slate-400">Zadaci</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500" />
            <span className="text-xs text-gray-500 dark:text-slate-400">Hranjenja</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-red-500" />
            <span className="text-xs text-gray-500 dark:text-slate-400">Zakasnjelo</span>
          </div>
        </div>
      </div>

        </div>

        {/* Selected-day panel */}
        <div className="lg:col-span-5 xl:col-span-4">
      <div className="card p-4 sm:p-6 animate-fade-in lg:sticky lg:top-20">
        <div className="flex items-center gap-2 mb-4">
          <CalendarDays className="w-4 h-4 text-honey-600 dark:text-honey-400" />
          <h3 className="font-display text-base font-bold text-gray-800 dark:text-slate-100">
            {format(selectedDay, 'EEEE, MMMM d, yyyy')}
          </h3>
          {isToday(selectedDay) && (
            <span className="text-xs font-semibold px-2 py-0.5 bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300 rounded-full">
              Danas
            </span>
          )}
        </div>

        {selectedEvents.todos.length === 0 && selectedEvents.feedings.length === 0 ? (
          <EmptyDay />
        ) : (
          <div className="space-y-5">
            {selectedEvents.todos.length > 0 && (
              <section>
                <SectionLabel icon={<CheckSquare className="w-3.5 h-3.5" />} color="honey" count={selectedEvents.todos.length}>
                  Zadaci
                </SectionLabel>
                <div className="space-y-2 mt-2">
                  {selectedEvents.todos.map(todo => <TodoCard key={todo.id} todo={todo} />)}
                </div>
              </section>
            )}
            {selectedEvents.feedings.length > 0 && (
              <section>
                <SectionLabel icon={<Droplets className="w-3.5 h-3.5" />} color="emerald" count={selectedEvents.feedings.length}>
                  Hranjenja
                </SectionLabel>
                <div className="space-y-2 mt-2">
                  {selectedEvents.feedings.map(f => <FeedingCard key={f.id} entry={f} />)}
                </div>
              </section>
            )}
          </div>
        )}
      </div>
        </div>
      </div>

    </div>
  )
}

// ── Helper components ──────────────────────────────────────────────────────────

/* VitalCard now lives in shared/components (with count-up animation). */

function EmptyDay() {
  return (
    <div className="flex flex-col items-center justify-center py-10 text-gray-400 dark:text-slate-500">
      <CalendarDays className="w-10 h-10 mb-3 opacity-30" />
      <p className="text-sm font-medium">Nema događaja ovog dana</p>
      <p className="text-xs mt-0.5 opacity-70">Odaberite drugi datum za pregled rasporeda</p>
    </div>
  )
}

function SectionLabel({
  icon, color, count, children,
}: {
  icon: React.ReactNode
  color: 'honey' | 'emerald'
  count: number
  children: React.ReactNode
}) {
  const cls = color === 'honey'
    ? 'text-honey-700 bg-honey-50 border-honey-100 dark:text-honey-300 dark:bg-honey-500/10 dark:border-honey-500/20'
    : 'text-emerald-700 bg-emerald-50 border-emerald-100 dark:text-emerald-300 dark:bg-emerald-500/10 dark:border-emerald-500/20'

  return (
    <div className={clsx('inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg border text-xs font-bold uppercase tracking-wide', cls)}>
      {icon}
      {children}
      <span className="ml-0.5 opacity-70">({count})</span>
    </div>
  )
}

function TodoCard({ todo }: { todo: CalendarTodo }) {
  const overdue = !todo.isCompleted && todo.dueDate && isPast(parseISO(todo.dueDate))

  const priorityConfig = {
    [TodoPriority.High]:   { label: 'Visok',  cls: 'text-red-600 bg-red-50 border-red-100 dark:text-red-300 dark:bg-red-500/10 dark:border-red-500/20' },
    [TodoPriority.Medium]: { label: 'Srednji', cls: 'text-amber-600 bg-amber-50 border-amber-100 dark:text-amber-300 dark:bg-amber-500/10 dark:border-amber-500/20' },
    [TodoPriority.Low]:    { label: 'Nizak',  cls: 'text-blue-600 bg-blue-50 border-blue-100 dark:text-blue-300 dark:bg-blue-500/10 dark:border-blue-500/20' },
  }
  const pc = priorityConfig[todo.priority] ?? priorityConfig[TodoPriority.Medium]

  const context     = todo.beehiveName ?? todo.apiaryName
  const contextKind = todo.beehiveId ? 'Košnica' : 'Pčelinjak'

  return (
    <div className={clsx(
      'flex items-start gap-3 p-3.5 rounded-xl border transition-all',
      todo.isCompleted  ? 'bg-gray-50 border-gray-100 opacity-60 dark:bg-slate-800/50 dark:border-slate-800' :
      overdue           ? 'bg-red-50 border-red-200 dark:bg-red-500/10 dark:border-red-500/20' :
                          'bg-white border-honey-100 hover:border-honey-200 dark:bg-slate-900 dark:border-slate-800 dark:hover:border-slate-700',
    )}>
      {/* Checkbox visual */}
      <div className={clsx(
        'flex-shrink-0 mt-0.5 w-4 h-4 rounded border-2 flex items-center justify-center',
        todo.isCompleted ? 'bg-green-500 border-green-500' : 'border-gray-300 dark:border-slate-600',
      )}>
        {todo.isCompleted && <span className="text-white text-[10px] font-bold">✓</span>}
      </div>

      <div className="min-w-0 flex-1">
        <p className={clsx(
          'text-sm font-semibold leading-snug',
          todo.isCompleted ? 'line-through text-gray-400 dark:text-slate-500' :
          overdue          ? 'text-red-800 dark:text-red-300' :
                             'text-gray-800 dark:text-slate-100',
        )}>
          {todo.title}
        </p>

        <div className="flex flex-wrap items-center gap-1.5 mt-1.5">
          {context && (
            <span className="text-xs text-gray-500 dark:text-slate-400">
              {contextKind}: <span className="font-medium text-gray-700 dark:text-slate-300">{context}</span>
            </span>
          )}
          <span className={clsx('text-xs px-2 py-0.5 rounded-full border font-medium', pc.cls)}>
            {pc.label}
          </span>
          {overdue && (
            <span className="flex items-center gap-0.5 text-xs text-red-600 font-semibold">
              <AlertCircle className="w-3 h-3" /> Zakasnjelo
            </span>
          )}
          {todo.isCompleted && (
            <span className="text-xs text-green-600 font-medium">Završeno</span>
          )}
        </div>

        {todo.notes && (
          <p className="text-xs text-gray-400 dark:text-slate-500 mt-1.5 line-clamp-2 leading-relaxed">{todo.notes}</p>
        )}
      </div>
    </div>
  )
}

function FeedingCard({ entry }: { entry: CalendarFeedingEntry }) {
  const completed = entry.status === FeedingEntryStatus.Completed
  const overdue   = !completed && isPast(parseISO(entry.scheduledDate))

  return (
    <Link
      to={`/feedings/${entry.dietId}`}
      className={clsx(
        'flex items-start gap-3 p-3.5 rounded-xl border transition-all group',
        completed ? 'bg-gray-50 border-gray-100 opacity-60 dark:bg-slate-800/50 dark:border-slate-800' :
        overdue   ? 'bg-red-50 border-red-200 hover:border-red-300 dark:bg-red-500/10 dark:border-red-500/20 dark:hover:border-red-500/40' :
                    'bg-emerald-50 border-emerald-200 hover:border-emerald-300 dark:bg-emerald-500/10 dark:border-emerald-500/20 dark:hover:border-emerald-500/40',
      )}
    >
      <Droplets className={clsx(
        'w-4 h-4 mt-0.5 flex-shrink-0 transition-transform group-hover:scale-110',
        completed ? 'text-gray-400 dark:text-slate-500' :
        overdue   ? 'text-red-500 dark:text-red-400'  :
                    'text-emerald-600 dark:text-emerald-400',
      )} />

      <div className="min-w-0 flex-1">
        <p className={clsx(
          'text-sm font-semibold leading-snug',
          completed ? 'line-through text-gray-400 dark:text-slate-500' :
          overdue   ? 'text-red-800 dark:text-red-300' :
                      'text-emerald-900 dark:text-emerald-200',
        )}>
          {entry.dietName}
        </p>

        <div className="flex flex-wrap items-center gap-1.5 mt-1.5">
          <span className="text-xs text-gray-500 dark:text-slate-400">
            Košnica: <span className="font-medium text-gray-700 dark:text-slate-300">{entry.beehiveName}</span>
          </span>
          <span className="text-xs text-gray-400 dark:text-slate-500">·</span>
          <span className="text-xs text-gray-500 dark:text-slate-400">{entry.foodTypeName}</span>
        </div>

        <div className="flex items-center gap-1.5 mt-1.5">
          {completed ? (
            <span className="flex items-center gap-1 text-xs text-green-600 font-semibold">
              <span className="w-1.5 h-1.5 rounded-full bg-green-500" />
              Završeno
            </span>
          ) : overdue ? (
            <span className="flex items-center gap-1 text-xs text-red-600 font-semibold">
              <AlertCircle className="w-3 h-3" /> Zakasnjelo
            </span>
          ) : (
            <span className="flex items-center gap-1 text-xs text-emerald-700 font-semibold">
              <Clock className="w-3 h-3" /> Na čekanju
            </span>
          )}
        </div>
      </div>

      <ChevronRightIcon className="w-4 h-4 text-gray-300 dark:text-slate-600 group-hover:text-gray-400 dark:group-hover:text-slate-500 flex-shrink-0 mt-0.5 transition-colors" />
    </Link>
  )
}
