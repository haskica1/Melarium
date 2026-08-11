import { useEffect, useMemo, useRef, useState } from 'react'
import { AlertTriangle, Loader2, Mic, Send, Sparkles, Square } from 'lucide-react'
import { useVoiceInput } from '../../core/hooks/useVoiceInput'
import { useOnlineStatus } from '../../core/hooks/useOnlineStatus'
import { useToast } from '../../core/context/ToastContext'
import { assistantService } from '../../core/services/assistantService'
import {
  useAddAssistantTurn,
  useConfirmAssistantActions,
  useRejectAssistantActions,
  useStartAssistantSession,
} from '../../core/services/assistantQueries'
import { useApiaries, useAllBeehives } from '../../core/services/queries'
import { plural } from '../../shared/utils/plural'
import { MarkdownMessage, Modal } from '../../shared/components'
import type { AssistantAction, AssistantSessionDetail, AssistantTurn } from '../../core/models'
import { DeletePreview, ProposalCard, UpdateSummary, isDeleteKind, isDraftComplete, type ProposalDraft } from './ProposalCard'

interface AssistantThreadProps {
  /** Page the user is on — fills gaps in the command, never overrides a spoken name. */
  contextApiaryId?: number | null
  contextBeehiveId?: number | null
  /** Existing session to continue; omit to start fresh. */
  initialSession?: AssistantSessionDetail
  onSessionChanged?: (session: AssistantSessionDetail) => void
}

const EXAMPLES = [
  'Pregledana košnica 2 na pčelinjaku Zlatna dolina, 5 ramova legla, med zadovoljavajući, pregled za 10 dana.',
  'Na pčelinjaku Zlatna dolina dodaj zadatak Dodaj postolje, srednji prioritet, do sljedeće sedmice.',
  'Pčele su agresivne i ima dosta matičnjaka, šta da radim?',
]

export function AssistantThread({
  contextApiaryId, contextBeehiveId, initialSession, onSessionChanged,
}: AssistantThreadProps) {
  const [session, setSession] = useState<AssistantSessionDetail | undefined>(initialSession)
  const [text, setText] = useState('')
  const [transcribing, setTranscribing] = useState(false)
  const [drafts, setDrafts] = useState<Record<number, ProposalDraft>>({})
  const [checked, setChecked] = useState<Record<number, boolean>>({})
  // Phase C, SPEC-17 §7.3: a batch touching an existing record needs a second, separate confirmation
  // on top of the first "Potvrdi" — this is that second step, shown only when it applies.
  const [confirmingDestructive, setConfirmingDestructive] = useState(false)

  const online = useOnlineStatus()
  const voice = useVoiceInput()
  const { toast } = useToast()
  const bottomRef = useRef<HTMLDivElement>(null)
  // The transcript is kept beside the text so an edit before sending does not erase what was heard.
  const transcriptRef = useRef<string | null>(null)

  const { data: apiaries = [] } = useApiaries()
  const { data: beehives = [] } = useAllBeehives()

  const start = useStartAssistantSession()
  const addTurn = useAddAssistantTurn(session?.id ?? 0)
  const confirm = useConfirmAssistantActions()
  const reject = useRejectAssistantActions()

  const isRecording = voice.state === 'recording'
  const thinking = start.isPending || addTurn.isPending
  const busy = thinking || transcribing || confirm.isPending

  const pendingTurn = useMemo(() => lastPendingTurn(session), [session])
  // Only the very last turn's question is still live — answering it (or sending anything else)
  // produces a newer turn, at which point the old buttons must stop being tappable.
  const lastTurn = session?.turns[session.turns.length - 1]
  const clarification = lastTurn?.role === 'Assistant' && lastTurn.candidates.length > 0 ? lastTurn : undefined

  // Seed a draft per proposal from what the assistant resolved, so an untouched card confirms as-is.
  useEffect(() => {
    if (!pendingTurn) return
    setDrafts(prev => {
      const next = { ...prev }
      for (const action of pendingTurn.actions) {
        if (next[action.id]) continue
        next[action.id] = {
          apiaryId: action.apiaryId ?? null,
          beehiveId: action.beehiveId ?? null,
          fields: { ...action.fields },
        }
      }
      return next
    })
    setChecked(prev => {
      const next = { ...prev }
      for (const action of pendingTurn.actions)
        if (next[action.id] === undefined) next[action.id] = true
      return next
    })
  }, [pendingTurn])

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [session, confirm.isPending])

  // A new turn means a stale review — never leave the second-confirmation modal open over cards
  // that no longer match what it is showing.
  useEffect(() => {
    setConfirmingDestructive(false)
  }, [pendingTurn?.id])

  function applySession(updated: AssistantSessionDetail) {
    setSession(updated)
    onSessionChanged?.(updated)
  }

  async function send(messageOverride?: string) {
    const message = (messageOverride ?? text).trim()
    if (!message || busy) return

    const payload = {
      text: message,
      transcript: transcriptRef.current,
      apiaryId: contextApiaryId ?? null,
      beehiveId: contextBeehiveId ?? null,
    }

    try {
      const updated = session
        ? await addTurn.mutateAsync(payload)
        : await start.mutateAsync(payload)
      applySession(updated)
      setText('')             // only on success — a failed send keeps the text for retry
      transcriptRef.current = null
    } catch {
      /* apiClient surfaces the message; the text stays so nothing dictated is lost */
    }
  }

  /** A tapped candidate is sent exactly like typed text — same pipeline, same validation, nothing skipped. */
  async function answerClarification(candidateText: string) {
    await send(candidateText)
  }

  async function toggleMic() {
    if (!isRecording) {
      await voice.startRecording()
      return
    }

    const blob = await voice.stopRecording()
    setTranscribing(true)
    let transcript = ''
    try {
      transcript = await assistantService.transcribe(blob)
    } catch {
      toast.error('Transkripcija nije uspjela. Pokušajte ponovo ili ukucajte naredbu.')
    } finally {
      setTranscribing(false)
      voice.reset()
    }

    const spoken = transcript.trim()
    if (!spoken) return
    transcriptRef.current = spoken
    // The command lands in the box for review — it creates records, so it is never auto-sent.
    setText(prev => (prev.trim() ? `${prev.trim()} ${spoken}` : spoken))
  }

  const selected = pendingTurn?.actions.filter(a => checked[a.id]) ?? []
  const blocked = selected.filter(a => !isDraftComplete(a, drafts[a.id] ?? emptyDraft()))
  const canConfirm = selected.length > 0 && blocked.length === 0 && !busy
  const destructiveSelected = selected.filter(a => a.isDestructive)

  /** The visible "Potvrdi" button: opens the review modal for a destructive batch instead of
   *  executing straight away; a create-only batch (or the modal's own "Da, potvrdi") goes through. */
  function handlePrimaryConfirm() {
    if (!canConfirm) return
    if (destructiveSelected.length > 0 && !confirmingDestructive) {
      setConfirmingDestructive(true)
      return
    }
    void confirmSelected()
  }

  async function confirmSelected() {
    if (!pendingTurn || !session || !canConfirm) return
    setConfirmingDestructive(false)
    try {
      const response = await confirm.mutateAsync({
        turnId: pendingTurn.id,
        payload: {
          actions: selected.map(a => ({
            id: a.id,
            apiaryId: drafts[a.id]?.apiaryId ?? null,
            beehiveId: drafts[a.id]?.beehiveId ?? null,
            fields: drafts[a.id]?.fields ?? {},
          })),
        },
      })
      const failed = response.results.filter(r => r.status === 'Failed')
      if (failed.length === 0) toast.success(response.message)
      else toast.error(response.message)
      applySession(await assistantService.getSession(session.id))
    } catch {
      /* message surfaced by the interceptor */
    }
  }

  async function rejectAll() {
    if (!pendingTurn || !session) return
    try {
      await reject.mutateAsync(pendingTurn.id)
      applySession(await assistantService.getSession(session.id))
    } catch {
      /* message surfaced by the interceptor */
    }
  }

  return (
    <div className="flex flex-col h-full min-h-0">
      {/* Thread */}
      <div className="flex-1 min-h-0 overflow-y-auto px-3 py-4 space-y-4">
        {!session && (
          <div className="text-center py-6">
            <Sparkles className="w-8 h-8 mx-auto text-honey-500" />
            <p className="mt-2 text-sm font-semibold text-gray-800 dark:text-slate-100">
              Recite šta ste uradili ili postavite pitanje o pčelarstvu.
            </p>
            <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
              Naredbe se izvršavaju tek kad ih potvrdite — pitanja dobijaju odgovor odmah.
            </p>
            <div className="mt-4 space-y-2 text-left">
              {EXAMPLES.map(example => (
                <button
                  key={example}
                  type="button"
                  onClick={() => setText(example)}
                  className="w-full text-left text-xs rounded-xl border border-gray-200 dark:border-slate-700 px-3 py-2 text-gray-600 dark:text-slate-300 hover:border-honey-300 hover:bg-honey-50/50 dark:hover:bg-slate-800 transition-colors"
                >
                  „{example}"
                </button>
              ))}
            </div>
          </div>
        )}

        {session?.turns.map(turn => (
          <div key={turn.id} className={turn.role === 'User' ? 'flex justify-end' : ''}>
            {turn.role === 'User' ? (
              <div className="max-w-[85%] rounded-2xl rounded-br-sm bg-honey-500 text-white px-3.5 py-2 text-sm">
                {turn.content}
              </div>
            ) : (
              <div className="space-y-2">
                <div className="max-w-[95%] rounded-2xl rounded-bl-sm bg-gray-100 dark:bg-slate-800 dark:text-slate-100 px-3.5 py-2 text-sm">
                  <MarkdownMessage content={turn.content} />
                </div>
                {turn.id === clarification?.id && (
                  <div className="flex flex-wrap gap-1.5 max-w-[95%]">
                    {turn.candidates.map(candidate => (
                      <button
                        key={candidate.text}
                        type="button"
                        onClick={() => void answerClarification(candidate.text)}
                        disabled={busy}
                        className="text-xs rounded-full border border-honey-300 dark:border-honey-800 bg-white dark:bg-slate-800 text-honey-700 dark:text-honey-300 px-3 py-1.5 hover:bg-honey-50 dark:hover:bg-slate-700 disabled:opacity-50 transition-colors"
                      >
                        {candidate.label}
                      </button>
                    ))}
                  </div>
                )}
                {turn.actions.length > 0 && (
                  <div className="space-y-2">
                    {turn.actions.map(action => (
                      <ProposalCard
                        key={action.id}
                        action={action}
                        draft={drafts[action.id] ?? draftFrom(action)}
                        checked={checked[action.id] ?? true}
                        disabled={busy}
                        apiaries={apiaries}
                        beehives={beehives}
                        onToggle={value => setChecked(prev => ({ ...prev, [action.id]: value }))}
                        onChange={value => setDrafts(prev => ({ ...prev, [action.id]: value }))}
                      />
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>
        ))}

        {thinking && (
          <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-slate-400">
            <Loader2 className="w-4 h-4 animate-spin" />
            Asistent razmišlja…
          </div>
        )}

        <div ref={bottomRef} />
      </div>

      {/* Confirmation bar — only while something is awaiting a decision */}
      {pendingTurn && pendingTurn.actions.length > 0 && (
        <div className="border-t border-honey-100 dark:border-slate-800 bg-honey-50/60 dark:bg-slate-800/60 px-3 py-2.5">
          <div className="flex items-center gap-2">
            <p className="flex-1 text-xs text-gray-600 dark:text-slate-300">
              {selected.length === 0
                ? 'Nijedna radnja nije označena.'
                : `Potvrđujete ${selected.length} ${plural(selected.length, 'radnju', 'radnje', 'radnji')}.`}
            </p>
            <button type="button" onClick={rejectAll} disabled={busy} className="btn-secondary text-sm py-1.5 px-3">
              Odbaci
            </button>
            <button type="button" onClick={handlePrimaryConfirm} disabled={!canConfirm} className="btn-primary text-sm py-1.5 px-4">
              {confirm.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Potvrdi'}
            </button>
          </div>
        </div>
      )}

      {/* Second confirmation (SPEC-17 §7.3) — only ever shown for a batch that updates or deletes an
          existing record; a create-only batch skips straight to executing. */}
      <Modal
        open={confirmingDestructive}
        onClose={() => setConfirmingDestructive(false)}
        title="Potvrdite izmjene"
        description="Ovo mijenja ili briše postojeće zapise. Provjerite prije nego nastavite."
        size="sm"
        icon={
          <div className="w-10 h-10 bg-red-100 dark:bg-red-500/15 rounded-full flex items-center justify-center">
            <AlertTriangle className="w-5 h-5 text-red-500" />
          </div>
        }
        footer={
          <div className="flex gap-3 justify-end">
            <button
              onClick={() => setConfirmingDestructive(false)}
              disabled={confirm.isPending}
              className="btn-secondary text-sm"
            >
              Odustani
            </button>
            <button onClick={() => void confirmSelected()} disabled={confirm.isPending} className="btn-danger text-sm">
              {confirm.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Da, potvrdi'}
            </button>
          </div>
        }
      >
        <div className="space-y-2">
          {destructiveSelected.map(a => (
            <div key={a.id}>
              {isDeleteKind(a.kind) ? <DeletePreview action={a} /> : <UpdateSummary action={a} />}
            </div>
          ))}
        </div>
      </Modal>

      {/* Disclaimer — the assistant now also gives advice (SPEC-18), not just data entry */}
      <p className="px-4 py-1.5 text-[11px] leading-snug text-gray-400 dark:text-slate-500 border-t border-honey-50 dark:border-slate-800/60">
        Savjeti su informativni — za bolesti koje podliježu prijavi obavezno kontaktiraj veterinarsku službu.
      </p>

      {/* Input */}
      <div className="border-t border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 p-3">
        {!online && (
          <p className="pb-2 text-xs text-gray-500 dark:text-slate-400">
            Asistent traži internet vezu — obrada govora i razumijevanje naredbe rade na serveru.
          </p>
        )}
        {isRecording && (
          <div className="flex items-center gap-2 px-2 pb-2 text-sm text-red-500">
            <span className="w-2 h-2 rounded-full bg-red-500 animate-pulse" />
            Snimanje…
            {voice.liveTranscript && (
              <span className="text-gray-400 dark:text-slate-500 truncate">{voice.liveTranscript}</span>
            )}
          </div>
        )}
        <div className="flex items-end gap-2">
          <textarea
            value={text}
            onChange={e => setText(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); void send() }
            }}
            rows={1}
            placeholder="Recite ili napišite šta ste uradili, ili postavite pitanje…"
            disabled={!online || (busy && !transcribing)}
            className="flex-1 resize-none max-h-40 px-4 py-2.5 rounded-2xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
          />
          <button
            type="button"
            onClick={toggleMic}
            disabled={!online || transcribing || thinking}
            className={`shrink-0 w-11 h-11 rounded-2xl flex items-center justify-center transition-colors disabled:opacity-50 ${
              isRecording
                ? 'bg-red-500 hover:bg-red-600 text-white'
                : 'bg-gray-100 dark:bg-slate-800 text-gray-600 dark:text-slate-300 hover:bg-honey-100 dark:hover:bg-slate-700 hover:text-honey-700'
            }`}
            aria-label={isRecording ? 'Zaustavi snimanje' : 'Snimi naredbu'}
          >
            {transcribing ? <Loader2 className="w-5 h-5 animate-spin" />
              : isRecording ? <Square className="w-5 h-5" />
              : <Mic className="w-5 h-5" />}
          </button>
          <button
            type="button"
            onClick={() => void send()}
            disabled={!online || busy || !text.trim()}
            className="shrink-0 w-11 h-11 rounded-2xl flex items-center justify-center bg-honey-500 hover:bg-honey-600 text-white transition-colors disabled:opacity-50"
            aria-label="Pošalji"
          >
            <Send className="w-5 h-5" />
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Helpers ───────────────────────────────────────────────────────────────────

/** The newest assistant turn that still has undecided proposals. */
function lastPendingTurn(session?: AssistantSessionDetail): AssistantTurn | undefined {
  if (!session) return undefined
  for (let i = session.turns.length - 1; i >= 0; i--) {
    const turn = session.turns[i]
    if (turn.role === 'Assistant' && turn.actions.some(a => a.status === 'Pending'))
      return turn
  }
  return undefined
}

function draftFrom(action: AssistantAction): ProposalDraft {
  return {
    apiaryId: action.apiaryId ?? null,
    beehiveId: action.beehiveId ?? null,
    fields: { ...action.fields },
  }
}

function emptyDraft(): ProposalDraft {
  return { apiaryId: null, beehiveId: null, fields: {} }
}
