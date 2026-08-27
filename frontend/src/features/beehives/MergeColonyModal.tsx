import { useMemo, useState } from 'react'
import { AlertTriangle, Combine, Loader2 } from 'lucide-react'
import { useToast } from '../../core/context/ToastContext'
import { format } from 'date-fns'
import { Modal } from '../../shared/components'
import { useAllBeehives, useApiaries } from '../../core/services/queries'
import { useCreateBeehiveMerge, useMergePreview } from '../../core/services/beehiveMergeQueries'
import { errorMessage } from '../../core/services/apiClient'
import { MergeMethod, MergeMethodLabels, MergeQueenOutcome, MergeReason, MergeReasonLabels } from '../../core/models'
import type { Beehive } from '../../core/models'

// Object.entries on a numeric enum's label map yields the keys as strings — hence the Number().
const REASONS = Object.entries(MergeReasonLabels).map(([value, label]) => ({
  value: Number(value) as MergeReason,
  label,
}))

const METHODS = Object.entries(MergeMethodLabels).map(([value, label]) => ({
  value: Number(value) as MergeMethod,
  label,
}))

// Same field styling the other forms declare locally (TreatmentFormPage, DietFormPage).
const inputClass =
  'w-full min-w-0 px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-base sm:text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all'
const labelClass = 'block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5'

interface Props {
  open: boolean
  onClose: () => void
  /** The hive that leaves the apiary — the dialog is always opened from it. */
  source: Beehive
  onMerged?: () => void
}

/**
 * Sastavljanje društava (SPEC-19 §7.2). Always launched from the hive that disappears, so the
 * direction cannot be read backwards. Confirmation is in two steps: the form's own button opens a summary
 * of exactly what will happen, and only that summary's button writes.
 *
 * The form is a child that mounts only while the dialog is open, so its queries run when the
 * beekeeper opens it. Mounted with the hive page instead, they fetched once on page load and then
 * never revalidated (`refetchOnWindowFocus` is off app-wide): the dropdown kept showing the hives as
 * they stood when the page loaded, and only a full reload brought in the rest.
 */
export function MergeColonyModal({ open, onClose, source, onMerged }: Props) {
  if (!open) return null
  return <MergeColonyDialog onClose={onClose} source={source} onMerged={onMerged} />
}

function MergeColonyDialog({ onClose, source, onMerged }: Omit<Props, 'open'>) {
  const { toast } = useToast()
  // 'always': the receiving hive is chosen out of this list, so opening the dialog has to revalidate
  // it — a hive added (or merged away) elsewhere must not be missing from the merge that follows.
  const { data: hives = [], isPending: hivesLoading } = useAllBeehives({ refetchOnMount: 'always' })
  const { data: apiaries = [] } = useApiaries()
  const createMerge = useCreateBeehiveMerge()

  const [targetId, setTargetId] = useState<number | ''>('')
  const [mergedAt, setMergedAt] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [reason, setReason] = useState<MergeReason>(MergeReason.WeakColony)
  const [method, setMethod] = useState<MergeMethod>(MergeMethod.Newspaper)
  const [queenOutcome, setQueenOutcome] = useState<MergeQueenOutcome | ''>('')
  const [notes, setNotes] = useState('')
  const [confirming, setConfirming] = useState(false)

  // `previewLoading` covers the first load and the refetch every target change triggers — until it
  // clears, nothing here knows which queens are involved.
  const {
    data: preview,
    isFetching: previewLoading,
    isError: previewFailed,
    refetch: refetchPreview,
  } = useMergePreview(source.id, typeof targetId === 'number' ? targetId : undefined)

  const apiaryName = useMemo(
    () => new Map(apiaries.map(a => [a.id, a.name])),
    [apiaries],
  )

  /** Grouped by apiary — cross-apiary merges are allowed (D4), so the list spans locations. */
  const grouped = useMemo(() => {
    const candidates = hives.filter(h => h.id !== source.id && !h.mergedIntoBeehiveId)
    const byApiary = new Map<number, Beehive[]>()
    for (const hive of candidates) {
      const list = byApiary.get(hive.apiaryId) ?? []
      list.push(hive)
      byApiary.set(hive.apiaryId, list)
    }
    return [...byApiary.entries()]
      .map(([id, list]) => ({
        apiaryId: id,
        name: apiaryName.get(id) ?? 'Pčelinjak',
        hives: list.sort((a, b) => a.name.localeCompare(b.name)),
      }))
      .sort((a, b) => a.name.localeCompare(b.name))
  }, [hives, source.id, apiaryName])

  const target = typeof targetId === 'number' ? hives.find(h => h.id === targetId) : undefined
  // The preview is part of the answer, not decoration: the confirmation step states real numbers
  // (open todos, feedings, treatments), so it must not be reachable while they are still in flight.
  const canSubmit =
    typeof targetId === 'number' &&
    queenOutcome !== '' &&
    !!preview &&
    !previewLoading &&
    !createMerge.isPending

  function handleClose() {
    if (createMerge.isPending) return
    onClose() // unmounting clears the form — there is nothing to reset by hand
  }

  async function submit() {
    if (typeof targetId !== 'number' || queenOutcome === '' || createMerge.isPending) return
    try {
      await createMerge.mutateAsync({
        sourceBeehiveId: source.id,
        targetBeehiveId: targetId,
        mergedAt,
        reason,
        method,
        queenOutcome,
        notes: notes.trim() || undefined,
      })
      toast.success(`Društvo iz košnice ${source.name} je sastavljeno s košnicom ${target?.name ?? ''}.`)
      onClose()
      onMerged?.()
    } catch (error) {
      setConfirming(false)
      toast.error(errorMessage(error))
    }
  }

  return (
    <>
      <Modal
        open={!confirming}
        onClose={handleClose}
        title="Sastavi društvo"
        description={`Društvo iz košnice ${source.name} prelazi u drugu košnicu. Ova košnica nakon toga izlazi iz pčelinjaka.`}
        size="lg"
        closeOnBackdropClick={false}
        icon={
          <div className="w-10 h-10 bg-honey-100 dark:bg-honey-500/15 rounded-full flex items-center justify-center">
            <Combine className="w-5 h-5 text-honey-600 dark:text-honey-400" />
          </div>
        }
        footer={
          <div className="flex gap-3 justify-end">
            <button onClick={handleClose} className="btn-secondary text-sm">Odustani</button>
            <button
              onClick={() => setConfirming(true)}
              disabled={!canSubmit}
              className="btn-primary text-sm disabled:opacity-50"
            >
              {previewLoading && typeof targetId === 'number'
                ? <><Loader2 className="w-4 h-4 animate-spin" /> Učitavanje…</>
                : 'Nastavi'}
            </button>
          </div>
        }
      >
        <div className="space-y-5">
          {/* Receiving hive */}
          <div>
            <label htmlFor="merge-target" className={labelClass}>Prijemna košnica <span className="text-red-500">*</span></label>
            <select
              id="merge-target"
              className={inputClass}
              value={targetId}
              disabled={hivesLoading}
              onChange={e => {
                setTargetId(e.target.value ? Number(e.target.value) : '')
                setQueenOutcome('')
              }}
            >
              <option value="">{hivesLoading ? 'Učitavanje košnica…' : 'Odaberite košnicu…'}</option>
              {grouped.map(group => (
                <optgroup key={group.apiaryId} label={group.name}>
                  {group.hives.map(h => (
                    <option key={h.id} value={h.id}>{h.name}</option>
                  ))}
                </optgroup>
              ))}
            </select>
            <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
              {!hivesLoading && grouped.length === 0
                ? 'Nema druge košnice kojoj bi se ovo društvo moglo pripojiti.'
                : 'Društvo se pripaja jačoj košnici — ona ostaje na svom mjestu.'}
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label htmlFor="merge-date" className={labelClass}>Datum <span className="text-red-500">*</span></label>
              <input
                id="merge-date"
                type="date"
                className={inputClass}
                value={mergedAt}
                max={format(new Date(), 'yyyy-MM-dd')}
                onChange={e => setMergedAt(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="merge-reason" className={labelClass}>Razlog</label>
              <select id="merge-reason" className={inputClass} value={reason} onChange={e => setReason(Number(e.target.value) as MergeReason)}>
                {REASONS.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
              </select>
            </div>
          </div>

          <div>
            <label htmlFor="merge-method" className={labelClass}>Metoda</label>
            <select id="merge-method" className={inputClass} value={method} onChange={e => setMethod(Number(e.target.value) as MergeMethod)}>
              {METHODS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
            </select>
          </div>

          {/* Queen — never defaulted (SPEC-19 D2). While the preview is in flight the options say
              so; before this they claimed the hive had no queen and disabled themselves. */}
          <fieldset>
            <legend className={labelClass}>Matica <span className="text-red-500">*</span></legend>
            <div className="space-y-2">
              <QueenOption
                checked={queenOutcome === MergeQueenOutcome.KeptTarget}
                onChange={() => setQueenOutcome(MergeQueenOutcome.KeptTarget)}
                disabled={!target || previewLoading || !preview?.targetQueenSummary}
                label={`Ostaje matica košnice ${target?.name ?? '…'} (prijemne)`}
                hint={
                  !target ? 'Prvo odaberite prijemnu košnicu.'
                  : previewLoading ? 'Provjeravam maticu prijemne košnice…'
                  : preview?.targetQueenSummary
                    ? `Matica ${preview.targetQueenSummary}. Matica ove košnice se zatvara.`
                    : 'Prijemna košnica nema aktivnu maticu.'
                }
              />
              <QueenOption
                checked={queenOutcome === MergeQueenOutcome.KeptSource}
                onChange={() => setQueenOutcome(MergeQueenOutcome.KeptSource)}
                disabled={!target || !preview?.sourceQueenSummary}
                label={`Ostaje matica košnice ${source.name} (pripojene)`}
                hint={
                  !preview ? 'Provjeravam maticu ove košnice…'
                  : preview.sourceQueenSummary
                    ? `Matica ${preview.sourceQueenSummary} prelazi u košnicu ${target?.name ?? '…'}.`
                    : 'Ova košnica nema aktivnu maticu.'
                }
              />
              <QueenOption
                checked={queenOutcome === MergeQueenOutcome.None}
                onChange={() => setQueenOutcome(MergeQueenOutcome.None)}
                label="Nijedna — društvo ostaje bez matice"
                hint="Obje aktivne matice se zatvaraju."
              />
            </div>

            {previewFailed && (
              <p className="mt-2 text-xs text-red-600 dark:text-red-400">
                Podaci o maticama i posljedicama nisu učitani.{' '}
                <button type="button" onClick={() => void refetchPreview()} className="underline font-medium">
                  Pokušajte ponovo
                </button>
              </p>
            )}
          </fieldset>

          <div>
            <label htmlFor="merge-notes" className={labelClass}>Napomena</label>
            <textarea
              id="merge-notes"
              className={inputClass}
              rows={2}
              maxLength={1000}
              value={notes}
              onChange={e => setNotes(e.target.value)}
              placeholder="npr. slabo društvo, dvije ulice pčela"
            />
          </div>
        </div>
      </Modal>

      {/* Second confirmation — the summary of consequences, with real numbers (§7.2) */}
      <Modal
        open={confirming}
        onClose={() => setConfirming(false)}
        title="Potvrdite sastavljanje"
        description="Košnica trajno izlazi iz pčelinjaka. Poništiti se može samo unutar 24 sata."
        size="sm"
        closeOnBackdropClick={false}
        icon={
          <div className="w-10 h-10 bg-red-100 dark:bg-red-500/15 rounded-full flex items-center justify-center">
            <AlertTriangle className="w-5 h-5 text-red-500" />
          </div>
        }
        footer={
          <div className="flex gap-3 justify-end">
            <button onClick={() => setConfirming(false)} disabled={createMerge.isPending} className="btn-secondary text-sm">
              Nazad
            </button>
            <button onClick={() => void submit()} disabled={createMerge.isPending} className="btn-danger text-sm">
              {createMerge.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Da, sastavi'}
            </button>
          </div>
        }
      >
        <ul className="space-y-2 text-sm text-gray-700 dark:text-slate-300">
          <li>
            Košnica <strong>{source.name}</strong> izlazi iz pčelinjaka
            {preview?.apiaryName ? <> <strong>{preview.apiaryName}</strong></> : null}, a društvo prelazi u
            košnicu <strong>{target?.name}</strong>.
          </li>
          {preview && preview.openTodoCount > 0 && (
            <li>Briše se {preview.openTodoCount} {preview.openTodoCount === 1 ? 'otvoren zadatak' : 'otvorenih zadataka'}.</li>
          )}
          {preview?.activeDietNames.map(name => (
            <li key={name}>Košnica se skida s prehrane „{name}”.</li>
          ))}
          {preview?.ongoingTreatmentNames.map(name => (
            <li key={name}>Prekida se učešće u tretmanu „{name}” (zapis u registru ostaje).</li>
          ))}
          {queenOutcome === MergeQueenOutcome.KeptTarget && preview?.sourceQueenSummary && (
            <li>Matica {preview.sourceQueenSummary} iz košnice {source.name} se zatvara.</li>
          )}
          {queenOutcome === MergeQueenOutcome.KeptSource && (
            <>
              {preview?.targetQueenSummary && <li>Matica {preview.targetQueenSummary} iz košnice {target?.name} se zatvara.</li>}
              {preview?.sourceQueenSummary && <li>Matica {preview.sourceQueenSummary} prelazi u košnicu {target?.name}.</li>}
            </>
          )}
          {queenOutcome === MergeQueenOutcome.None && <li>Obje aktivne matice se zatvaraju — društvo ostaje bez matice.</li>}
          <li className="text-gray-500 dark:text-slate-400">
            Pregledi, vrcanja i tretmani ostaju zabilježeni na košnici {source.name}.
          </li>
        </ul>

        {preview?.karencaUntil && (
          <div className="mt-4 flex gap-2.5 p-3 rounded-xl border border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-500/10">
            <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5 text-amber-600 dark:text-amber-400" />
            <p className="text-xs text-amber-800 dark:text-amber-300">
              Košnica je u karenci
              {preview.karencaProductName ? <> nakon tretmana „{preview.karencaProductName}”</> : null} do{' '}
              <strong>{format(new Date(preview.karencaUntil), 'dd.MM.yyyy.')}</strong> — pčele je nose sa sobom
              u košnicu {target?.name}.
            </p>
          </div>
        )}
      </Modal>
    </>
  )
}

function QueenOption({
  checked, onChange, disabled = false, label, hint,
}: {
  checked: boolean
  onChange: () => void
  disabled?: boolean
  label: string
  hint: string
}) {
  return (
    <label
      className={`flex gap-3 p-3 rounded-xl border cursor-pointer transition-colors ${
        disabled
          ? 'opacity-50 cursor-not-allowed border-gray-200 dark:border-slate-800'
          : checked
          ? 'border-honey-400 bg-honey-50 dark:bg-honey-500/10 dark:border-honey-500/50'
          : 'border-gray-200 dark:border-slate-800 hover:bg-gray-50 dark:hover:bg-slate-800/50'
      }`}
    >
      <input
        type="radio"
        name="queen-outcome"
        className="mt-0.5 shrink-0"
        checked={checked}
        disabled={disabled}
        onChange={onChange}
      />
      <span className="min-w-0">
        <span className="block text-sm font-medium text-gray-800 dark:text-slate-100">{label}</span>
        <span className="block text-xs text-gray-500 dark:text-slate-400">{hint}</span>
      </span>
    </label>
  )
}
