import { Link } from 'react-router-dom'
import { ArrowRight, GraduationCap } from 'lucide-react'
import { LearningCategoryLabels, MonthLabels, type DashboardTopic } from '../../core/models'
import DashboardCard from './DashboardCard'

/** One Edukacija topic marked for this month — unread first (SPEC-29). */
export default function LearningSpotlight({ topic }: { topic: DashboardTopic }) {
  const month = MonthLabels[new Date().getMonth()].toLowerCase()

  return (
    <DashboardCard icon={<GraduationCap className="w-4 h-4" />} title="Aktuelno u Edukaciji" action={{ to: '/learning', label: 'Sve teme' }}>
      <Link to={`/learning/${topic.id}`} className="group block rounded-2xl -m-1 p-1">
        <p className="text-xs font-medium text-honey-700 dark:text-honey-400">
          {LearningCategoryLabels[topic.category]} · za {month}
        </p>
        <p className="mt-1 font-display text-lg font-bold text-gray-900 dark:text-slate-50 group-hover:text-honey-800 dark:group-hover:text-honey-300">
          {topic.title}
        </p>
        <p className="mt-1 text-sm text-gray-600 dark:text-slate-400 line-clamp-2">{topic.summary}</p>
        <span className="mt-2 inline-flex items-center gap-1 text-sm font-medium text-honey-700 dark:text-honey-400">
          Pročitaj <ArrowRight className="w-4 h-4 transition-transform group-hover:translate-x-0.5" />
        </span>
      </Link>
    </DashboardCard>
  )
}
