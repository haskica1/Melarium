import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Combine, Loader2, Undo2 } from 'lucide-react'
import { format, formatDistanceToNowStrict } from 'date-fns'
import { bs } from 'date-fns/locale'
import { useToast } from '../../core/context/ToastContext'
import { ConfirmDialog } from '../../shared/components'
import { useMergesByBeehive, useUndoBeehiveMerge } from '../../core/services/beehiveMergeQueries'
import { errorMessage } from '../../core/services/apiClient'
import type { BeehiveMerge } from '../../core/models'

/**
 * "Sastavljena društva" — the colonies this hive took in (SPEC-19 §7.3). Renders nothing for the
 * overwhelming majority of hives, which never received one, so it costs no vertical space there.
 *
 * The undo button appears only while the server still reports an open window (`canUndoUntil`); the
 * client never computes the deadline itself.
 */
export function MergeSection({ beehiveId }: { beehiveId: number }) {
  const { toast } = useToast()
  const { data: merges = [] } = useMergesByBeehive(beehiveId)
  const undo = useUndoBeehiveMerge()
  const [undoTarget, setUndoTarget] = useState<BeehiveMerge | null>(null)

  async function handleUndo() {
    const target = undoTarget
    if (!target) return
    try {
      await undo.mutateAsync(target.id)
      toast.success(`Sastavljanje je poništeno — košnica ${target.sourceBeehiveName} je vraćena u pčelinjak.`)
    } catch (error) {
      toast.error(errorMessage(error))
    }
    setUndoTarget(null)
  }

  if (merges.length === 0) return null

  return (
    <div className="card">
      <div className="flex items-center gap-2 mb-4">
        <Combine className="w-5 h-5 text-honey-500" />
        <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">
          Sastavljena društva
        </h2>
      </div>

      <ul className="space-y-3">
        {merges.map(merge => (
          <li
            key={merge.id}
            className="p-3 rounded-xl border border-honey-100 dark:border-slate-800 bg-honey-50/50 dark:bg-slate-800/40"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="text-sm text-gray-800 dark:text-slate-100">
                  Primljeno društvo iz košnice{' '}
                  <Link
                    to={`/beehives/${merge.sourceBeehiveId}`}
                    className="font-semibold text-honey-700 dark:text-honey-400 hover:underline"
                  >
                    {merge.sourceBeehiveName}
                  </Link>
                </p>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
                  {format(new Date(merge.mergedAt), 'dd.MM.yyyy.')} · {merge.reasonName} · {merge.methodName}
                </p>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">{merge.queenOutcomeName}</p>
                {merge.notes && (
                  <p className="mt-1.5 text-xs italic text-gray-600 dark:text-slate-300">📝 {merge.notes}</p>
                )}
              </div>
            </div>

            {merge.canUndoUntil && (
              <div className="mt-3 pt-3 border-t border-honey-100 dark:border-slate-800 flex items-center justify-between gap-3">
                <p className="text-xs text-gray-500 dark:text-slate-400">
                  Može se poništiti još{' '}
                  {formatDistanceToNowStrict(new Date(merge.canUndoUntil), { locale: bs })}
                </p>
                <button
                  onClick={() => setUndoTarget(merge)}
                  className="btn-secondary text-xs shrink-0"
                  disabled={undo.isPending}
                >
                  {undo.isPending && undoTarget?.id === merge.id
                    ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                    : <Undo2 className="w-3.5 h-3.5" />}
                  Poništi
                </button>
              </div>
            )}
          </li>
        ))}
      </ul>

      <ConfirmDialog
        isOpen={!!undoTarget}
        title="Poništi sastavljanje"
        message={
          undoTarget
            ? `Košnica ${undoTarget.sourceBeehiveName} se vraća u pčelinjak. Vraćaju se i njeni zadaci, matica i prehrana — sve u stanje prije sastavljanja.`
            : ''
        }
        confirmLabel="Poništi sastavljanje"
        onConfirm={handleUndo}
        onCancel={() => setUndoTarget(null)}
        isLoading={undo.isPending}
      />
    </div>
  )
}
