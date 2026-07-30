import { Link } from 'react-router-dom'
import { CircleHelp, GraduationCap, Info, Loader2 } from 'lucide-react'
import { Modal } from './Modal'
import { LearningCategoryLabels } from '../../core/models'
import type { HelpEntry, HelpRoleName } from '../../core/help/helpContent'

interface HelpPanelProps {
  open: boolean
  onClose: () => void
  entry: HelpEntry | null
  loading: boolean
  loadFailed: boolean
  role: string | undefined
  autoOpen: boolean
  onDisableAutoOpen: () => void
}

export default function HelpPanel({
  open,
  onClose,
  entry,
  loading,
  loadFailed,
  role,
  autoOpen,
  onDisableAutoOpen,
}: HelpPanelProps) {
  const roleNote = entry?.notesByRole?.[role as HelpRoleName]

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={entry?.title ?? 'Pomoć'}
      size="lg"
      icon={
        <div className="w-10 h-10 bg-honey-100 dark:bg-honey-500/15 rounded-full flex items-center justify-center">
          <CircleHelp className="w-5 h-5 text-honey-600 dark:text-honey-400" />
        </div>
      }
      footer={
        autoOpen ? (
          <div className="flex flex-col sm:flex-row sm:items-center gap-3">
            <button
              type="button"
              onClick={onDisableAutoOpen}
              className="text-xs text-gray-500 dark:text-slate-400 hover:text-gray-700 dark:hover:text-slate-200 underline text-left"
            >
              Ne prikazuj automatski pomoć na novim stranicama
            </button>
            <button onClick={onClose} className="btn-primary text-sm sm:ml-auto justify-center">
              Razumijem
            </button>
          </div>
        ) : (
          <button onClick={onClose} className="btn-primary text-sm w-full justify-center">
            Razumijem
          </button>
        )
      }
    >
      {loading && (
        <div className="flex items-center justify-center py-10">
          <Loader2 className="w-5 h-5 animate-spin text-honey-500" />
        </div>
      )}

      {!loading && loadFailed && (
        <p className="text-sm text-gray-500 dark:text-slate-400">
          Pomoć se nije mogla učitati. Provjerite konekciju i pokušajte ponovo.
        </p>
      )}

      {!loading && !loadFailed && !entry && (
        <p className="text-sm text-gray-500 dark:text-slate-400">
          Za ovu stranicu još nema pomoći.
        </p>
      )}

      {!loading && entry && (
        <div className="space-y-5">
          <p className="text-sm text-gray-700 dark:text-slate-200 leading-relaxed">{entry.summary}</p>

          {entry.steps && entry.steps.length > 0 && (
            <section>
              <h3 className="text-sm font-semibold text-gray-800 dark:text-slate-100 mb-2">
                Kako koristiti
              </h3>
              <ol className="space-y-2">
                {entry.steps.map((step, i) => (
                  <li key={i} className="flex gap-2.5 text-sm text-gray-700 dark:text-slate-300">
                    <span className="shrink-0 w-5 h-5 rounded-full bg-honey-100 dark:bg-honey-500/15 text-honey-700 dark:text-honey-300 text-xs font-bold flex items-center justify-center mt-0.5">
                      {i + 1}
                    </span>
                    <span className="leading-relaxed">{step}</span>
                  </li>
                ))}
              </ol>
            </section>
          )}

          {entry.tips && entry.tips.length > 0 && (
            <section>
              <h3 className="text-sm font-semibold text-gray-800 dark:text-slate-100 mb-2">
                Dobro je znati
              </h3>
              <ul className="space-y-2">
                {entry.tips.map((tip, i) => (
                  <li key={i} className="flex gap-2.5 text-sm text-gray-700 dark:text-slate-300">
                    <span className="shrink-0 text-honey-500 mt-0.5">•</span>
                    <span className="leading-relaxed">{tip}</span>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {roleNote && (
            <div className="flex gap-2.5 p-3 rounded-xl bg-blue-50 dark:bg-blue-500/10 border border-blue-100 dark:border-blue-500/20">
              <Info className="w-4 h-4 text-blue-500 shrink-0 mt-0.5" />
              <p className="text-sm text-blue-800 dark:text-blue-200 leading-relaxed">{roleNote}</p>
            </div>
          )}

          {entry.faq && entry.faq.length > 0 && (
            <section>
              <h3 className="text-sm font-semibold text-gray-800 dark:text-slate-100 mb-2">
                Česta pitanja
              </h3>
              <div className="space-y-1.5">
                {entry.faq.map((item, i) => (
                  <details
                    key={i}
                    className="group rounded-xl border border-gray-100 dark:border-slate-800 px-3 py-2"
                  >
                    <summary className="cursor-pointer text-sm font-medium text-gray-800 dark:text-slate-100 marker:content-none list-none flex items-center gap-2">
                      <span className="text-honey-500 transition-transform group-open:rotate-90">›</span>
                      {item.q}
                    </summary>
                    <p className="mt-2 pl-4 text-sm text-gray-600 dark:text-slate-300 leading-relaxed">
                      {item.a}
                    </p>
                  </details>
                ))}
              </div>
            </section>
          )}

          {entry.learningCategory !== undefined && (
            <Link
              to={`/learning?category=${entry.learningCategory}`}
              onClick={onClose}
              className="flex items-center gap-2 text-sm font-medium text-honey-700 dark:text-honey-300 hover:underline"
            >
              <GraduationCap className="w-4 h-4" />
              Saznaj više u Edukaciji: {LearningCategoryLabels[entry.learningCategory]}
            </Link>
          )}
        </div>
      )}
    </Modal>
  )
}
