import { Link } from 'react-router-dom'
import { ChevronRight } from 'lucide-react'
import clsx from 'clsx'

/** The shell every dashboard block shares: icon, title, optional count and a "see all" link. */
export default function DashboardCard({
  icon,
  title,
  badge,
  action,
  className,
  children,
}: {
  icon: React.ReactNode
  title: string
  badge?: React.ReactNode
  action?: { to: string; label: string }
  className?: string
  children: React.ReactNode
}) {
  return (
    <section className={clsx('card flex flex-col min-w-0', className)}>
      <div className="flex items-center gap-2.5 mb-4">
        <span className="w-8 h-8 shrink-0 rounded-xl bg-honey-100 dark:bg-honey-500/15 flex items-center justify-center text-honey-700 dark:text-honey-300">
          {icon}
        </span>
        <h2 className="font-semibold text-gray-900 dark:text-slate-100 truncate">{title}</h2>
        {badge}
        {action && (
          <Link
            to={action.to}
            className="ml-auto shrink-0 inline-flex items-center gap-0.5 text-xs font-medium text-honey-700 dark:text-honey-400 hover:text-honey-900 dark:hover:text-honey-300"
          >
            {action.label} <ChevronRight className="w-3.5 h-3.5" />
          </Link>
        )}
      </div>
      {children}
    </section>
  )
}

/** A quiet one-line message for a block with nothing to show. */
export function CardEmpty({ icon, children }: { icon?: string; children: React.ReactNode }) {
  return (
    <p className="flex items-center gap-2 text-sm text-gray-500 dark:text-slate-400 py-2">
      {icon && <span className="text-base">{icon}</span>}
      {children}
    </p>
  )
}
