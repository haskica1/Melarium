import { AlertTriangle, CheckCircle2, ClipboardList, Combine, Leaf, Pencil, Trash2, XCircle } from 'lucide-react'
import { format } from 'date-fns'
import {
  HoneyLevel,
  HoneyLevelLabels,
  MergeQueenOutcomeLabels,
  MergeReason,
  MergeReasonLabels,
  TodoPriority,
  TodoPriorityLabels,
  type Apiary,
  type AssistantAction,
  type AssistantActionKind,
  type AssistantFields,
  type AssistantIssue,
  type Beehive,
  type MergeQueenOutcome,
} from '../../core/models'

/** What the card holds while the user edits it, before confirmation. */
export interface ProposalDraft {
  apiaryId: number | null
  beehiveId: number | null
  fields: AssistantFields
}

interface ProposalCardProps {
  action: AssistantAction
  draft: ProposalDraft
  checked: boolean
  disabled?: boolean
  apiaries: Apiary[]
  beehives: Beehive[]
  onToggle: (checked: boolean) => void
  onChange: (draft: ProposalDraft) => void
}

/**
 * Phase C kinds all point at a record the beekeeper already has. Their target was found by the
 * resolver (title/date match), never re-picked from an apiary/hive dropdown — the card shows it,
 * it does not let the user swap in a different one.
 */
const EXISTING_RECORD_KINDS: ReadonlySet<AssistantActionKind> =
  new Set(['UpdateTodo', 'CompleteTodo', 'UpdateInspection', 'DeleteTodo', 'DeleteInspection'])

function isExistingRecordKind(kind: AssistantActionKind): boolean {
  return EXISTING_RECORD_KINDS.has(kind)
}

export function isDeleteKind(kind: AssistantActionKind): boolean {
  return kind === 'DeleteTodo' || kind === 'DeleteInspection'
}

/** Bosnian explanation of why the card still needs the user, or null when it is ready. */
export function issueMessage(issue: AssistantIssue, unresolved: string[]): string | null {
  const numbers = unresolved.join(', ')
  switch (issue) {
    case 'ApiaryNotFound':        return 'Nisam prepoznao taj pčelinjak — odaberite ga ispod.'
    case 'ApiaryAmbiguous':       return 'Više pčelinjaka odgovara — odaberite koji.'
    case 'HiveNotFound':          return numbers ? `Nisam pronašao košnicu ${numbers} — odaberite je ispod.` : 'Nisam pronašao tu košnicu.'
    case 'HiveAmbiguous':         return numbers ? `Košnica ${numbers} postoji na više pčelinjaka — odaberite koju.` : 'Više košnica odgovara.'
    case 'NoHivesInApiary':       return 'Taj pčelinjak nema nijednu košnicu.'
    case 'MissingHive':           return 'Nije rečeno za koju košnicu — odaberite je ispod.'
    case 'MissingTitle':          return 'Zadatak nema naslov — upišite ga ispod.'
    case 'TodoNotFound':          return 'Nisam pronašao taj zadatak. Provjerite naslov ili recite ga preciznije.'
    case 'TodoAmbiguous':         return 'Više zadataka odgovara tom naslovu — recite preciznije (npr. uz pčelinjak).'
    case 'InspectionNotFound':    return 'Nisam pronašao taj pregled.'
    case 'InspectionAmbiguous':   return 'Više pregleda odgovara tom datumu.'
    case 'MergeTargetNotFound':   return 'Nisam prepoznao prijemnu košnicu — odaberite je ispod.'
    case 'MergeSourceAmbiguous':  return 'Sastavlja se jedna po jedna košnica — recite koju pripajate.'
    case 'MissingQueenOutcome':   return 'Nije rečeno koja matica ostaje — odaberite ispod.'
    default:                      return null
  }
}

/**
 * A create draft is confirmable once it has a target and, for a todo, a title. An existing-record
 * draft (Phase C) has no dropdown to complete — it is confirmable exactly when the resolver found
 * the one record it means, i.e. `issue === 'None'`.
 */
export function isDraftComplete(action: AssistantAction, draft: ProposalDraft): boolean {
  if (isExistingRecordKind(action.kind)) return action.issue === 'None'
  if (action.kind === 'CreateInspection') return draft.beehiveId != null
  // A merge needs both hives and, above all, the queen — which the server refuses to default.
  if (action.kind === 'MergeBeehive') {
    return draft.beehiveId != null
      && draft.fields.targetBeehiveId != null
      && draft.fields.targetBeehiveId !== draft.beehiveId
      && draft.fields.queenOutcome != null
  }
  return (draft.beehiveId != null || draft.apiaryId != null) && !!draft.fields.title?.trim()
}

export function ProposalCard({
  action, draft, checked, disabled, apiaries, beehives, onToggle, onChange,
}: ProposalCardProps) {
  const isInspection = action.kind === 'CreateInspection' || action.kind === 'UpdateInspection'
  const isMerge = action.kind === 'MergeBeehive'
  const isExisting = isExistingRecordKind(action.kind)
  const settled = action.status !== 'Pending'
  const complete = isDraftComplete(action, draft)
  const problem = settled ? null : issueMessage(action.issue, action.unresolvedHiveNumbers)
  const resolvedExisting = isExisting && action.issue === 'None'

  // A hive implies its apiary, so the apiary select only narrows the hive list.
  const hivesForApiary = draft.apiaryId == null
    ? beehives
    : beehives.filter(b => b.apiaryId === draft.apiaryId)

  // The apiary/hive lists are separate requests, and on a field connection they can fail while the
  // proposal itself arrived fine. Without these fallbacks the card would claim "Nije određeno" and
  // show empty selects for a target the server had already resolved — with Potvrdi still enabled,
  // which is the worst version: correct behaviour that looks broken.
  const apiaryOptions = draft.apiaryId != null && !apiaries.some(a => a.id === draft.apiaryId)
    ? [{ id: draft.apiaryId, name: action.apiaryName ?? `Pčelinjak #${draft.apiaryId}` }, ...apiaries]
    : apiaries
  const hiveOptions = draft.beehiveId != null && !hivesForApiary.some(b => b.id === draft.beehiveId)
    ? [{ id: draft.beehiveId, name: action.beehiveName ?? `Košnica #${draft.beehiveId}` }, ...hivesForApiary]
    : hivesForApiary

  const set = (patch: Partial<ProposalDraft>) => onChange({ ...draft, ...patch })
  const setField = (patch: Partial<AssistantFields>) => onChange({ ...draft, fields: { ...draft.fields, ...patch } })

  function selectApiary(value: string) {
    const apiaryId = value ? Number(value) : null
    // Changing the apiary invalidates a hive that belongs to a different one.
    const hiveStillValid = draft.beehiveId != null
      && beehives.some(b => b.id === draft.beehiveId && (apiaryId == null || b.apiaryId === apiaryId))
    set({ apiaryId, beehiveId: hiveStillValid ? draft.beehiveId : null })
  }

  function selectHive(value: string) {
    const beehiveId = value ? Number(value) : null
    const hive = beehives.find(b => b.id === beehiveId)
    set({ beehiveId, apiaryId: hive?.apiaryId ?? draft.apiaryId })
  }

  return (
    <div className={`rounded-2xl border p-3 transition-colors ${
      settled
        ? action.status === 'Confirmed'
          ? 'border-green-200 bg-green-50 dark:border-green-900 dark:bg-green-950/30'
          : action.status === 'Failed'
            ? 'border-red-200 bg-red-50 dark:border-red-900 dark:bg-red-950/30'
            : 'border-gray-200 bg-gray-50 dark:border-slate-700 dark:bg-slate-800/50 opacity-70'
        : checked
          ? 'border-honey-300 bg-honey-50/60 dark:border-honey-900 dark:bg-slate-800'
          : 'border-gray-200 bg-white dark:border-slate-700 dark:bg-slate-900 opacity-60'
    }`}>
      {/* Header */}
      <div className="flex items-start gap-2">
        {!settled && (
          <input
            type="checkbox"
            checked={checked}
            disabled={disabled}
            onChange={e => onToggle(e.target.checked)}
            className="mt-1 w-4 h-4 accent-honey-500 shrink-0"
            aria-label={`Uključi: ${action.kindLabel}`}
          />
        )}
        <div className="shrink-0 mt-0.5 text-honey-600 dark:text-honey-400">
          {settled
            ? action.status === 'Confirmed' ? <CheckCircle2 className="w-4 h-4 text-green-600" />
              : action.status === 'Failed' ? <XCircle className="w-4 h-4 text-red-600" />
              : <XCircle className="w-4 h-4 text-gray-400" />
            : <ActionIcon kind={action.kind} />}
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-gray-800 dark:text-slate-100">{action.kindLabel}</p>
          <p className="text-xs text-gray-500 dark:text-slate-400 truncate">
            {settled || isExisting
              ? action.targetSummary
              : (draftTarget(action, draft, apiaries, beehives) ?? 'Nije određeno')}
          </p>
        </div>
      </div>

      {settled ? (
        <p className={`mt-2 text-xs ${action.status === 'Failed' ? 'text-red-600 dark:text-red-400' : 'text-gray-500 dark:text-slate-400'}`}>
          {action.status === 'Confirmed' && 'Izvršeno.'}
          {action.status === 'Rejected' && 'Odbijeno.'}
          {action.status === 'Failed' && (action.errorMessage ?? 'Nije uspjelo.')}
        </p>
      ) : (
        <div className="mt-3 space-y-2">
          {problem && (
            <p className="flex items-start gap-1.5 text-xs text-amber-700 dark:text-amber-400">
              <AlertTriangle className="w-3.5 h-3.5 shrink-0 mt-px" />
              {problem}
            </p>
          )}

          {resolvedExisting ? (
            isDeleteKind(action.kind) ? (
              <DeletePreview action={action} />
            ) : action.kind === 'CompleteTodo' ? (
              <CompletePreview action={action} />
            ) : (
              <UpdateFields action={action} draft={draft} disabled={disabled} setField={setField} />
            )
          ) : isExisting ? (
            // Unresolved existing-record action: there is no dropdown for "which record" — the way
            // forward is the follow-up question's buttons (Phase B) or a more specific retyped command.
            null
          ) : (
            <>
              {/* Target — always editable, so a wrongly guessed hive costs one tap, not a new command. */}
              <div className="grid grid-cols-2 gap-2">
                <label className="text-xs">
                  <span className="block text-gray-500 dark:text-slate-400 mb-1">Pčelinjak</span>
                  <select
                    className="form-input py-1.5 text-sm"
                    value={draft.apiaryId ?? ''}
                    disabled={disabled}
                    onChange={e => selectApiary(e.target.value)}
                  >
                    <option value="">— odaberite —</option>
                    {apiaryOptions.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
                  </select>
                </label>

                <label className="text-xs">
                  <span className="block text-gray-500 dark:text-slate-400 mb-1">
                    {isMerge ? 'Pripojena košnica' : 'Košnica'}{' '}
                    {(isInspection || isMerge) && <span className="text-red-500">*</span>}
                  </span>
                  <select
                    className="form-input py-1.5 text-sm"
                    value={draft.beehiveId ?? ''}
                    disabled={disabled}
                    onChange={e => selectHive(e.target.value)}
                  >
                    <option value="">{isInspection || isMerge ? '— odaberite —' : '— cijeli pčelinjak —'}</option>
                    {hiveOptions.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
                  </select>
                </label>
              </div>

              {isMerge ? (
                <MergeFields
                  draft={draft}
                  beehives={beehives}
                  disabled={disabled}
                  setField={setField}
                />
              ) : isInspection ? (
                <>
                  <div className="grid grid-cols-2 gap-2">
                    <label className="text-xs">
                      <span className="block text-gray-500 dark:text-slate-400 mb-1">Datum</span>
                      <input
                        type="date"
                        className="form-input py-1.5 text-sm"
                        max={format(new Date(), 'yyyy-MM-dd')}
                        value={draft.fields.date ?? ''}
                        disabled={disabled}
                        onChange={e => setField({ date: e.target.value || null })}
                      />
                    </label>
                    <label className="text-xs">
                      <span className="block text-gray-500 dark:text-slate-400 mb-1">Nivo meda</span>
                      <select
                        className="form-input py-1.5 text-sm"
                        value={draft.fields.honeyLevel ?? HoneyLevel.Medium}
                        disabled={disabled}
                        onChange={e => setField({ honeyLevel: Number(e.target.value) as HoneyLevel })}
                      >
                        {[HoneyLevel.Low, HoneyLevel.Medium, HoneyLevel.High].map(l => (
                          <option key={l} value={l}>{HoneyLevelLabels[l]}</option>
                        ))}
                      </select>
                    </label>
                  </div>
                  <label className="block text-xs">
                    <span className="block text-gray-500 dark:text-slate-400 mb-1">Stanje legla</span>
                    <input
                      type="text"
                      className="form-input py-1.5 text-sm"
                      value={draft.fields.broodStatus ?? ''}
                      disabled={disabled}
                      onChange={e => setField({ broodStatus: e.target.value || null })}
                    />
                  </label>
                </>
              ) : (
                <>
                  <label className="block text-xs">
                    <span className="block text-gray-500 dark:text-slate-400 mb-1">
                      Naslov <span className="text-red-500">*</span>
                    </span>
                    <input
                      type="text"
                      className="form-input py-1.5 text-sm"
                      value={draft.fields.title ?? ''}
                      disabled={disabled}
                      onChange={e => setField({ title: e.target.value || null })}
                    />
                  </label>
                  <div className="grid grid-cols-2 gap-2">
                    <label className="text-xs">
                      <span className="block text-gray-500 dark:text-slate-400 mb-1">Prioritet</span>
                      <select
                        className="form-input py-1.5 text-sm"
                        value={draft.fields.priority ?? TodoPriority.Medium}
                        disabled={disabled}
                        onChange={e => setField({ priority: Number(e.target.value) as TodoPriority })}
                      >
                        {[TodoPriority.Low, TodoPriority.Medium, TodoPriority.High].map(p => (
                          <option key={p} value={p}>{TodoPriorityLabels[p]}</option>
                        ))}
                      </select>
                    </label>
                    <label className="text-xs">
                      <span className="block text-gray-500 dark:text-slate-400 mb-1">Rok</span>
                      <input
                        type="date"
                        className="form-input py-1.5 text-sm"
                        value={draft.fields.dueDate ?? ''}
                        disabled={disabled}
                        onChange={e => setField({ dueDate: e.target.value || null })}
                      />
                    </label>
                  </div>
                </>
              )}

              <label className="block text-xs">
                <span className="block text-gray-500 dark:text-slate-400 mb-1">Napomena</span>
                <textarea
                  rows={2}
                  className="form-input py-1.5 text-sm resize-none"
                  value={draft.fields.notes ?? ''}
                  disabled={disabled}
                  onChange={e => setField({ notes: e.target.value || null })}
                />
              </label>
            </>
          )}

          {checked && !complete && (
            <p className="text-xs text-amber-700 dark:text-amber-400">
              {isExisting ? 'Ovo se još ne može potvrditi — pogledajte poruku iznad.' : 'Dopunite označena polja da biste mogli potvrditi.'}
            </p>
          )}
        </div>
      )}
    </div>
  )
}

// ── Phase C: existing-record previews ───────────────────────────────────────────

function ActionIcon({ kind }: { kind: AssistantActionKind }) {
  if (isDeleteKind(kind)) return <Trash2 className="w-4 h-4" />
  if (kind === 'UpdateTodo' || kind === 'UpdateInspection') return <Pencil className="w-4 h-4" />
  if (kind === 'CompleteTodo') return <CheckCircle2 className="w-4 h-4" />
  if (kind === 'MergeBeehive') return <Combine className="w-4 h-4" />
  return kind === 'CreateInspection' ? <ClipboardList className="w-4 h-4" /> : <Leaf className="w-4 h-4" />
}

// Numeric enums on the wire (the API takes no string enums), so the option values are numbers and
// every read back off a <select> goes through Number().
const QUEEN_OUTCOMES = Object.entries(MergeQueenOutcomeLabels).map(([value, label]) => ({
  value: Number(value) as MergeQueenOutcome,
  label,
}))

const MERGE_REASONS = Object.entries(MergeReasonLabels).map(([value, label]) => ({
  value: Number(value) as MergeReason,
  label,
}))

/**
 * The merge-specific half of the card (SPEC-19 §8). The receiving hive and the queen are separate
 * from the standard target selects above, which pick the hive that <i>leaves</i> the apiary.
 *
 * The queen select has no pre-selected value on purpose: leaving it blank is what keeps the card
 * unconfirmable until a human states the one irreversible choice.
 */
function MergeFields({
  draft, beehives, disabled, setField,
}: {
  draft: ProposalDraft
  beehives: { id: number; name: string; apiaryId: number }[]
  disabled?: boolean
  setField: (patch: Partial<AssistantFields>) => void
}) {
  const targetOptions = beehives.filter(b => b.id !== draft.beehiveId)
  const known = draft.fields.targetBeehiveId != null
    && !targetOptions.some(b => b.id === draft.fields.targetBeehiveId)
  const options = known
    ? [{
        id: draft.fields.targetBeehiveId!,
        name: draft.fields.targetBeehiveName ?? `Košnica #${draft.fields.targetBeehiveId}`,
        apiaryId: 0,
      }, ...targetOptions]
    : targetOptions

  return (
    <>
      <label className="block text-xs">
        <span className="block text-gray-500 dark:text-slate-400 mb-1">
          Prijemna košnica <span className="text-red-500">*</span>
        </span>
        <select
          className="form-input py-1.5 text-sm"
          value={draft.fields.targetBeehiveId ?? ''}
          disabled={disabled}
          onChange={e => {
            const id = e.target.value ? Number(e.target.value) : null
            setField({
              targetBeehiveId: id,
              targetBeehiveName: options.find(b => b.id === id)?.name ?? null,
            })
          }}
        >
          <option value="">— odaberite —</option>
          {options.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
        </select>
      </label>

      <label className="block text-xs">
        <span className="block text-gray-500 dark:text-slate-400 mb-1">
          Matica <span className="text-red-500">*</span>
        </span>
        <select
          className="form-input py-1.5 text-sm"
          value={draft.fields.queenOutcome ?? ''}
          disabled={disabled}
          onChange={e => setField({ queenOutcome: e.target.value ? Number(e.target.value) as MergeQueenOutcome : null })}
        >
          <option value="">— odaberite —</option>
          {QUEEN_OUTCOMES.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      </label>

      <div className="grid grid-cols-2 gap-2">
        <label className="text-xs">
          <span className="block text-gray-500 dark:text-slate-400 mb-1">Datum</span>
          <input
            type="date"
            className="form-input py-1.5 text-sm"
            max={format(new Date(), 'yyyy-MM-dd')}
            value={draft.fields.date ?? ''}
            disabled={disabled}
            onChange={e => setField({ date: e.target.value || null })}
          />
        </label>
        <label className="text-xs">
          <span className="block text-gray-500 dark:text-slate-400 mb-1">Razlog</span>
          <select
            className="form-input py-1.5 text-sm"
            value={draft.fields.mergeReason ?? MergeReason.Other}
            disabled={disabled}
            onChange={e => setField({ mergeReason: Number(e.target.value) as MergeReason })}
          >
            {MERGE_REASONS.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
          </select>
        </label>
      </div>
    </>
  )
}

/**
 * The merge's entry in the second-confirmation modal. Read-only, and deliberately spells out that
 * the hive leaves the apiary — that is the part a "Sastavljanje društava" label does not convey.
 */
export function MergePreview({ action }: { action: AssistantAction }) {
  const f = action.fields
  const queen = QUEEN_OUTCOMES.find(o => o.value === f.queenOutcome)?.label

  return (
    <div className="rounded-xl border border-red-200 dark:border-red-900 bg-red-50 dark:bg-red-950/30 p-2.5 space-y-1.5">
      <p className="text-xs font-medium text-red-700 dark:text-red-400">
        Košnica {action.beehiveName ?? action.targetSummary} izlazi iz pčelinjaka
      </p>
      <p className="text-xs text-gray-700 dark:text-slate-300">
        Društvo prelazi u košnicu <strong>{f.targetBeehiveName ?? '—'}</strong>.
        {queen ? ` ${queen}.` : ''}
      </p>
      <p className="text-xs text-gray-500 dark:text-slate-400">
        Otvoreni zadaci se brišu, košnica se skida s prehrane, a učešće u tretmanima u toku se prekida.
        Poništiti se može unutar 24 sata.
      </p>
    </div>
  )
}

/**
 * Read-only — the target is fixed, and there is nothing to edit before deleting it. Exported so the
 * second-confirmation modal (SPEC-17 §7.3) can show the exact same "what disappears" content the card
 * already shows, instead of a second, drifting copy of the same fields.
 */
export function DeletePreview({ action }: { action: AssistantAction }) {
  const f = action.previousFields ?? {}
  const isInspection = action.kind === 'DeleteInspection'

  return (
    <div className="rounded-xl border border-red-200 dark:border-red-900 bg-red-50 dark:bg-red-950/30 p-2.5 space-y-1.5">
      <p className="text-xs font-medium text-red-700 dark:text-red-400">
        Ovo će trajno obrisati:
      </p>
      <dl className="text-xs text-gray-700 dark:text-slate-300 space-y-0.5">
        {isInspection ? (
          <>
            <Row label="Datum" value={f.date ? format(new Date(f.date), 'dd.MM.yyyy') : '—'} />
            <Row label="Med" value={f.honeyLevel != null ? HoneyLevelLabels[f.honeyLevel] : '—'} />
            {f.broodStatus && <Row label="Leglo" value={f.broodStatus} />}
            {f.notes && <Row label="Napomena" value={f.notes} />}
          </>
        ) : (
          <>
            <Row label="Naslov" value={f.title ?? '—'} />
            <Row label="Prioritet" value={f.priority != null ? TodoPriorityLabels[f.priority] : '—'} />
            <Row label="Rok" value={f.dueDate ? format(new Date(f.dueDate), 'dd.MM.yyyy') : 'bez roka'} />
            {f.notes && <Row label="Napomena" value={f.notes} />}
          </>
        )}
      </dl>
      {isInspection && (
        <p className="text-xs text-red-600 dark:text-red-400">
          Fotografije uz ovaj pregled će također biti obrisane.
        </p>
      )}
    </div>
  )
}

/** Read-only — completing a todo is a one-tap, reversible toggle everywhere else in the app too. */
function CompletePreview({ action }: { action: AssistantAction }) {
  const f = action.previousFields ?? {}
  return (
    <div className="rounded-xl border border-gray-200 dark:border-slate-700 bg-gray-50 dark:bg-slate-800/50 p-2.5 space-y-1">
      <p className="text-xs text-gray-700 dark:text-slate-300">
        Označiti kao završeno: <span className="font-medium">{f.title ?? action.targetSummary}</span>
      </p>
      {f.dueDate && (
        <p className="text-xs text-gray-500 dark:text-slate-400">Rok bio: {format(new Date(f.dueDate), 'dd.MM.yyyy')}</p>
      )}
    </div>
  )
}

/** Editable — same inputs as a create card, seeded with the AI's proposed values, with a "prije" hint per changed field. */
function UpdateFields({ action, draft, disabled, setField }: {
  action: AssistantAction
  draft: ProposalDraft
  disabled?: boolean
  setField: (patch: Partial<AssistantFields>) => void
}) {
  const prev = action.previousFields ?? {}
  const isInspection = action.kind === 'UpdateInspection'

  return (
    <>
      {isInspection ? (
        <>
          <div className="grid grid-cols-2 gap-2">
            <label className="text-xs">
              <span className="block text-gray-500 dark:text-slate-400 mb-1">Nivo meda</span>
              <select
                className="form-input py-1.5 text-sm"
                value={draft.fields.honeyLevel ?? prev.honeyLevel ?? HoneyLevel.Medium}
                disabled={disabled}
                onChange={e => setField({ honeyLevel: Number(e.target.value) as HoneyLevel })}
              >
                {[HoneyLevel.Low, HoneyLevel.Medium, HoneyLevel.High].map(l => (
                  <option key={l} value={l}>{HoneyLevelLabels[l]}</option>
                ))}
              </select>
              <FieldHint prev={prev.honeyLevel != null ? HoneyLevelLabels[prev.honeyLevel] : null}
                         next={draft.fields.honeyLevel != null ? HoneyLevelLabels[draft.fields.honeyLevel] : null} />
            </label>
          </div>
          <label className="block text-xs">
            <span className="block text-gray-500 dark:text-slate-400 mb-1">Stanje legla</span>
            <input
              type="text"
              className="form-input py-1.5 text-sm"
              value={draft.fields.broodStatus ?? ''}
              disabled={disabled}
              onChange={e => setField({ broodStatus: e.target.value || null })}
            />
            <FieldHint prev={prev.broodStatus ?? null} next={draft.fields.broodStatus ?? null} />
          </label>
        </>
      ) : (
        <>
          <label className="block text-xs">
            <span className="block text-gray-500 dark:text-slate-400 mb-1">Naslov</span>
            <input
              type="text"
              className="form-input py-1.5 text-sm"
              value={draft.fields.title ?? ''}
              disabled={disabled}
              onChange={e => setField({ title: e.target.value || null })}
            />
            <FieldHint prev={prev.title ?? null} next={draft.fields.title ?? null} />
          </label>
          <div className="grid grid-cols-2 gap-2">
            <label className="text-xs">
              <span className="block text-gray-500 dark:text-slate-400 mb-1">Prioritet</span>
              <select
                className="form-input py-1.5 text-sm"
                value={draft.fields.priority ?? prev.priority ?? TodoPriority.Medium}
                disabled={disabled}
                onChange={e => setField({ priority: Number(e.target.value) as TodoPriority })}
              >
                {[TodoPriority.Low, TodoPriority.Medium, TodoPriority.High].map(p => (
                  <option key={p} value={p}>{TodoPriorityLabels[p]}</option>
                ))}
              </select>
              <FieldHint prev={prev.priority != null ? TodoPriorityLabels[prev.priority] : null}
                         next={draft.fields.priority != null ? TodoPriorityLabels[draft.fields.priority] : null} />
            </label>
            <label className="text-xs">
              <span className="block text-gray-500 dark:text-slate-400 mb-1">Rok</span>
              <input
                type="date"
                className="form-input py-1.5 text-sm"
                value={draft.fields.dueDate ?? ''}
                disabled={disabled}
                onChange={e => setField({ dueDate: e.target.value || null })}
              />
              <FieldHint prev={prev.dueDate ? format(new Date(prev.dueDate), 'dd.MM.yyyy') : null}
                         next={draft.fields.dueDate ? format(new Date(draft.fields.dueDate), 'dd.MM.yyyy') : null} />
            </label>
          </div>
        </>
      )}

      <label className="block text-xs">
        <span className="block text-gray-500 dark:text-slate-400 mb-1">Napomena</span>
        <textarea
          rows={2}
          className="form-input py-1.5 text-sm resize-none"
          value={draft.fields.notes ?? ''}
          disabled={disabled}
          onChange={e => setField({ notes: e.target.value || null })}
        />
        <FieldHint prev={prev.notes ?? null} next={draft.fields.notes ?? null} />
      </label>
    </>
  )
}

/**
 * Read-only "polje: prije → poslije" list, one line per field that actually changed. Used by the
 * second-confirmation modal (SPEC-17 §7.3) — a plain review of what an update does, separate from
 * `UpdateFields`'s editable inputs which the modal has no business re-opening.
 */
export function UpdateSummary({ action }: { action: AssistantAction }) {
  const prev = action.previousFields ?? {}
  const next = action.fields
  const isInspection = action.kind === 'UpdateInspection'

  const rows: Array<{ label: string; from: string; to: string }> = isInspection
    ? [
        { label: 'Med', from: prev.honeyLevel != null ? HoneyLevelLabels[prev.honeyLevel] : '—',
          to: next.honeyLevel != null ? HoneyLevelLabels[next.honeyLevel] : (prev.honeyLevel != null ? HoneyLevelLabels[prev.honeyLevel] : '—') },
        { label: 'Leglo', from: prev.broodStatus ?? '—', to: next.broodStatus ?? prev.broodStatus ?? '—' },
        { label: 'Napomena', from: prev.notes ?? '—', to: next.notes ?? prev.notes ?? '—' },
      ]
    : [
        { label: 'Naslov', from: prev.title ?? '—', to: next.title ?? prev.title ?? '—' },
        { label: 'Prioritet', from: prev.priority != null ? TodoPriorityLabels[prev.priority] : '—',
          to: next.priority != null ? TodoPriorityLabels[next.priority] : (prev.priority != null ? TodoPriorityLabels[prev.priority] : '—') },
        { label: 'Rok', from: prev.dueDate ? format(new Date(prev.dueDate), 'dd.MM.yyyy') : 'bez roka',
          to: next.dueDate ? format(new Date(next.dueDate), 'dd.MM.yyyy') : (prev.dueDate ? format(new Date(prev.dueDate), 'dd.MM.yyyy') : 'bez roka') },
        { label: 'Napomena', from: prev.notes ?? '—', to: next.notes ?? prev.notes ?? '—' },
      ]

  const changed = rows.filter(r => r.from !== r.to)

  return (
    <div className="rounded-xl border border-gray-200 dark:border-slate-700 bg-gray-50 dark:bg-slate-800/50 p-2.5 space-y-1">
      <p className="text-xs font-medium text-gray-700 dark:text-slate-300">{action.targetSummary}</p>
      {changed.length === 0 ? (
        <p className="text-xs text-gray-500 dark:text-slate-400">Bez izmjena — vrijednosti ostaju iste.</p>
      ) : (
        <dl className="text-xs text-gray-700 dark:text-slate-300 space-y-0.5">
          {changed.map(r => (
            <div key={r.label} className="flex gap-1.5">
              <dt className="text-gray-400 dark:text-slate-500 shrink-0">{r.label}:</dt>
              <dd className="truncate">{r.from} → <span className="font-medium">{r.to}</span></dd>
            </div>
          ))}
        </dl>
      )}
    </div>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex gap-1.5">
      <dt className="text-gray-400 dark:text-slate-500 shrink-0">{label}:</dt>
      <dd className="truncate">{value}</dd>
    </div>
  )
}

/** "prije: X" — shown only when editing actually changed the value from what it is today. */
function FieldHint({ prev, next }: { prev: string | null; next: string | null }) {
  if (prev === next || !prev) return null
  return <span className="block mt-0.5 text-[11px] text-gray-400 dark:text-slate-500">prije: {prev}</span>
}

function draftTarget(
  action: AssistantAction, draft: ProposalDraft, apiaries: Apiary[], beehives: Beehive[],
): string | null {
  const hive = beehives.find(b => b.id === draft.beehiveId)
  const apiary = apiaries.find(a => a.id === (hive?.apiaryId ?? draft.apiaryId))

  if (hive && apiary) return `${apiary.name} · ${hive.name}`
  if (hive) return hive.name

  // Lists unavailable but the target is still the one the server resolved — use the names it sent
  // rather than reporting a target we do have as unknown.
  const hiveName = !hive && draft.beehiveId === action.beehiveId ? action.beehiveName : null
  const apiaryName = apiary?.name
    ?? (draft.apiaryId === action.apiaryId || draft.beehiveId === action.beehiveId ? action.apiaryName : null)

  if (apiaryName && hiveName) return `${apiaryName} · ${hiveName}`
  if (hiveName) return hiveName
  if (apiaryName) return apiaryName
  return null
}
