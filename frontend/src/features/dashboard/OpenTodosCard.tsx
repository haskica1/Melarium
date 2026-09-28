import { Link } from 'react-router-dom'
import { CheckSquare } from 'lucide-react'
import clsx from 'clsx'
import { TodoPriority, type DashboardTodo } from '../../core/models'
import DashboardCard, { CardEmpty } from './DashboardCard'
import { relativeDay } from './format'

const PRIORITY_DOT: Record<TodoPriority, string> = {
  [TodoPriority.High]:   'bg-red-500',
  [TodoPriority.Medium]: 'bg-amber-400',
  [TodoPriority.Low]:    'bg-gray-300 dark:bg-slate-600',
}

/** The first few open todos — late ones first — including those with no due date, which the obligations never show. */
export default function OpenTodosCard({ todos, total }: { todos: DashboardTodo[]; total: number }) {
  return (
    <DashboardCard
      icon={<CheckSquare className="w-4 h-4" />}
      title="Otvoreni zadaci"
      badge={total > 0 && <span className="text-xs text-gray-500 dark:text-slate-400">{total}</span>}
    >
      {todos.length === 0 ? (
        <CardEmpty icon="✅">Nema otvorenih zadataka.</CardEmpty>
      ) : (
        <ul className="-mx-1 divide-y divide-honey-100 dark:divide-slate-800">
          {todos.map(t => (
            <li key={t.id}>
              <Link to={t.linkPath} className="flex items-center gap-3 px-1 py-2 rounded-lg hover:bg-honey-50 dark:hover:bg-slate-800/60">
                <span className={clsx('w-2 h-2 rounded-full shrink-0', PRIORITY_DOT[t.priority])} />
                <span className="min-w-0 flex-1">
                  <span className="block text-sm text-gray-900 dark:text-slate-100 truncate">{t.title}</span>
                  {t.scopeName && <span className="block text-xs text-gray-500 dark:text-slate-400 truncate">{t.scopeName}</span>}
                </span>
                <span className={clsx(
                  'text-xs shrink-0',
                  t.isOverdue ? 'font-semibold text-red-600 dark:text-red-400' : 'text-gray-500 dark:text-slate-400',
                )}>
                  {t.dueDate ? relativeDay(t.dueDate.slice(0, 10)) : 'bez roka'}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </DashboardCard>
  )
}
