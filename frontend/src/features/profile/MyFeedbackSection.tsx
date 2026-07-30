import { format } from 'date-fns'
import { MessageSquarePlus } from 'lucide-react'
import { useMyFeedback } from '../../core/services/feedbackQueries'
import { FeedbackStatus, FeedbackTypeEmojis } from '../../core/models'
import { ErrorMessage, Skeleton } from '../../shared/components'

const STATUS_STYLE: Record<FeedbackStatus, string> = {
  [FeedbackStatus.New]:       'bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300',
  [FeedbackStatus.InReview]:  'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  [FeedbackStatus.Resolved]:  'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  [FeedbackStatus.Dismissed]: 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
}

/**
 * The submitter's own feedback history. Closes the loop — without this, sending feedback feels like
 * shouting into a void and people stop bothering.
 */
export default function MyFeedbackSection({ onNew }: { onNew: () => void }) {
  // isPending/isSuccess, not isLoading — see the note in FeedbackAdminPage: between retries
  // isLoading and isError are both false, which would show "još niste poslali ništa" on a failure.
  const { data: items = [], isPending, isError, isSuccess } = useMyFeedback()

  return (
    <div className="card space-y-4">
      <div className="flex items-center gap-2">
        <MessageSquarePlus className="w-4 h-4 text-honey-500" />
        <h3 className="font-semibold text-gray-700 dark:text-slate-200">Moje povratne informacije</h3>
        <button type="button" onClick={onNew} className="ml-auto btn-secondary text-xs">
          Nova
        </button>
      </div>

      {isPending && (
        <div className="space-y-2">
          <Skeleton className="h-14" />
          <Skeleton className="h-14" />
        </div>
      )}

      {isError && <ErrorMessage message="Greška pri učitavanju vaših povratnih informacija." />}

      {isSuccess && items.length === 0 && (
        <p className="text-sm text-gray-500 dark:text-slate-400">
          Još niste poslali ništa. Ako nešto ne radi ili imate ideju — javite nam, čitamo sve.
        </p>
      )}

      {isSuccess && items.length > 0 && (
        <ul className="space-y-2">
          {items.map(item => (
            <li
              key={item.id}
              className="rounded-xl border border-gray-100 dark:border-slate-800 px-3 py-2.5"
            >
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-base leading-none">{FeedbackTypeEmojis[item.type]}</span>
                <span className="text-sm font-medium text-gray-800 dark:text-slate-100 min-w-0 truncate">
                  {item.subject}
                </span>
                <span className={`text-xs rounded-full px-2 py-0.5 shrink-0 ${STATUS_STYLE[item.status]}`}>
                  {item.statusName}
                </span>
              </div>
              <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                {format(new Date(item.createdAt), 'dd.MM.yyyy')}
              </p>
              {item.adminResponse && (
                <p className="mt-2 text-sm text-gray-700 dark:text-slate-300 bg-gray-50 dark:bg-slate-800/60 rounded-lg px-3 py-2">
                  <span className="font-medium">Odgovor: </span>{item.adminResponse}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
