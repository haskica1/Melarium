import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { CheckCircle2, GraduationCap, Paperclip, Video } from 'lucide-react'
import { useLearningTopics } from '../../core/services/learningQueries'
import { LearningCategory, LearningCategoryLabels, MonthLabels } from '../../core/models'
import type { LearningTopicSummary } from '../../core/models'
import { EmptyState, VitalsSkeleton } from '../../shared/components'

const CATEGORIES = Object.values(LearningCategory).filter(v => typeof v === 'number') as LearningCategory[]

// "Aktuelno u {mjesecu}" — locative month names (can't be derived from MonthLabels).
const MONTH_LOCATIVE = [
  'januaru', 'februaru', 'martu', 'aprilu', 'maju', 'junu',
  'julu', 'augustu', 'septembru', 'oktobru', 'novembru', 'decembru',
] as const

export default function LearningPage() {
  const { data: topics = [], isLoading } = useLearningTopics()
  const [category, setCategory] = useState<LearningCategory | 0>(0)

  const currentMonth = new Date().getMonth() + 1

  const filtered = useMemo(
    () => topics.filter(t => category === 0 || t.category === category),
    [topics, category],
  )
  const aktuelno = filtered.filter(t => t.months?.includes(currentMonth))
  const readCount = topics.filter(t => t.isRead).length

  const grouped = useMemo(() => {
    const map = new Map<LearningCategory, LearningTopicSummary[]>()
    for (const t of filtered) {
      const arr = map.get(t.category) ?? []
      arr.push(t)
      map.set(t.category, arr)
    }
    return CATEGORIES.filter(c => map.has(c)).map(c => [c, map.get(c)!] as const)
  }, [filtered])

  return (
    <div className="animate-fade-in space-y-6">
      {/* Hero */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center gap-4 min-w-0">
            <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
              🎓
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Edukacija</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
                Kratke praktične teme za čitanje ili slušanje — pročitano {readCount} od {topics.length}.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Category filter chips */}
      <div className="flex items-center gap-2 flex-wrap">
        <FilterChip label="Sve" active={category === 0} onClick={() => setCategory(0)} />
        {CATEGORIES.map(c => (
          <FilterChip
            key={c}
            label={LearningCategoryLabels[c]}
            active={category === c}
            onClick={() => setCategory(c)}
          />
        ))}
      </div>

      {isLoading && <VitalsSkeleton />}

      {!isLoading && topics.length === 0 && (
        <EmptyState
          title="Još nema objavljenih tema."
          description="Edukativne teme objavljuje administrator platforme."
        />
      )}

      {!isLoading && aktuelno.length > 0 && (
        <section className="space-y-3">
          <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 px-1">
            Aktuelno u {MONTH_LOCATIVE[currentMonth - 1]}
          </h2>
          <div className="grid sm:grid-cols-2 gap-3">
            {aktuelno.map(t => <TopicCard key={t.id} topic={t} highlight />)}
          </div>
        </section>
      )}

      {!isLoading && grouped.map(([cat, items]) => (
        <section key={cat} className="space-y-3">
          <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 px-1">
            {LearningCategoryLabels[cat]}
          </h2>
          <div className="grid sm:grid-cols-2 gap-3">
            {items.map(t => <TopicCard key={t.id} topic={t} />)}
          </div>
        </section>
      ))}
    </div>
  )
}

function FilterChip({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className={`px-3 py-1.5 rounded-full text-sm font-medium border transition-colors ${
        active
          ? 'bg-honey-500 border-honey-500 text-white'
          : 'bg-white dark:bg-slate-800 border-honey-200 dark:border-slate-700 text-gray-600 dark:text-slate-300 hover:bg-honey-50 dark:hover:bg-slate-700'
      }`}
    >
      {label}
    </button>
  )
}

function TopicCard({ topic, highlight = false }: { topic: LearningTopicSummary; highlight?: boolean }) {
  return (
    <Link
      to={`/learning/${topic.id}`}
      className={`block bg-white dark:bg-slate-900 rounded-2xl border px-5 py-4 shadow-sm dark:shadow-none transition-colors ${
        highlight
          ? 'border-honey-300 dark:border-honey-500/40 hover:border-honey-400'
          : 'border-honey-100 dark:border-slate-800 hover:border-honey-200 dark:hover:border-slate-700'
      }`}
    >
      <div className="flex items-start gap-3">
        <div className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 bg-honey-50 text-honey-600 dark:bg-honey-500/15 dark:text-honey-300">
          <GraduationCap className="w-5 h-5" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-gray-900 dark:text-slate-100 truncate">{topic.title}</h3>
            {topic.isRead && <CheckCircle2 className="w-4 h-4 text-emerald-500 shrink-0" aria-label="Pročitano" />}
          </div>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400 line-clamp-2">{topic.summary}</p>
          <div className="mt-2 flex items-center gap-2 flex-wrap">
            <span className="text-xs text-honey-700 dark:text-honey-300 bg-honey-100 dark:bg-honey-500/15 rounded-full px-2 py-0.5">
              {topic.categoryName}
            </span>
            {topic.months && topic.months.length > 0 && (
              <span className="text-xs text-gray-400 dark:text-slate-500">
                {topic.months.map(m => MonthLabels[m - 1].slice(0, 3).toLowerCase()).join(', ')}
              </span>
            )}
            {topic.videoUrl && <Video className="w-3.5 h-3.5 text-gray-400 dark:text-slate-500" aria-label="Sadrži video" />}
            {topic.fileUrl && <Paperclip className="w-3.5 h-3.5 text-gray-400 dark:text-slate-500" aria-label="Sadrži prilog" />}
          </div>
        </div>
      </div>
    </Link>
  )
}
