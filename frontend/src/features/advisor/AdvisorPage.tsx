import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { formatDistanceToNow } from 'date-fns'
import { useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Bot, Loader2, Plus, Trash2 } from 'lucide-react'
import {
  useAdvisorConversations,
  useAdvisorConversation,
  useCreateAdvisorConversation,
  useSendAdvisorMessage,
  useDeleteAdvisorConversation,
  advisorQueryKeys,
} from '../../core/services/advisorQueries'
import { Link } from 'react-router-dom'
import { useBeehive } from '../../core/services/queries'
import { useMyPlan } from '../../core/services/planService'
import { ConfirmDialog } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'
import { ChatThread } from './ChatThread'
import { ChatInput } from './ChatInput'
import { PlanType, type AdvisorConversationSummary } from '../../core/models'

export default function AdvisorPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const initialBeehiveId = searchParams.get('beehiveId')

  const [activeId, setActiveId] = useState<number | null>(null)
  const [newMode, setNewMode] = useState<boolean>(!!initialBeehiveId)
  const [newBeehiveId, setNewBeehiveId] = useState<number | null>(initialBeehiveId ? Number(initialBeehiveId) : null)
  const [pending, setPending] = useState<string | null>(null)
  const [confirmDeleteId, setConfirmDeleteId] = useState<number | null>(null)

  const qc = useQueryClient()
  const { toast } = useToast()

  const { data: conversations = [], isLoading: listLoading } = useAdvisorConversations()
  const { data: detail } = useAdvisorConversation(activeId ?? 0)
  const { data: newHive } = useBeehive(newBeehiveId ?? 0)
  const createMut = useCreateAdvisorConversation()
  const sendMut = useSendAdvisorMessage(activeId ?? 0)
  const deleteMut = useDeleteAdvisorConversation()

  const showThread = activeId !== null || newMode
  const isThinking = createMut.isPending || sendMut.isPending

  // Hive chip in the thread header.
  const chipLabel = newMode
    ? (newBeehiveId ? (newHive?.name ?? `Košnica #${newBeehiveId}`) : null)
    : (detail?.beehiveId != null ? (detail.beehiveName ?? '(obrisana)') : null)

  function startNew(beehiveId: number | null = null) {
    setActiveId(null)
    setNewMode(true)
    setNewBeehiveId(beehiveId)
    setPending(null)
  }

  function selectConversation(id: number) {
    setActiveId(id)
    setNewMode(false)
    setNewBeehiveId(null)
    setPending(null)
  }

  async function handleSend(message: string) {
    setPending(message)
    try {
      if (newMode) {
        const created = await createMut.mutateAsync({ beehiveId: newBeehiveId, message })
        qc.setQueryData(advisorQueryKeys.conversation(created.id), created)
        setActiveId(created.id)
        setNewMode(false)
        setNewBeehiveId(null)
        if (initialBeehiveId) setSearchParams({}, { replace: true })
      } else if (activeId) {
        await sendMut.mutateAsync({ message })
      }
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Slanje nije uspjelo. Pokušajte ponovo.')
      throw e // let ChatInput keep the text for retry
    } finally {
      setPending(null)
    }
  }

  async function handleDelete() {
    if (confirmDeleteId == null) return
    try {
      await deleteMut.mutateAsync(confirmDeleteId)
      if (activeId === confirmDeleteId) setActiveId(null)
      toast.success('Razgovor obrisan.')
    } catch {
      toast.error('Brisanje nije uspjelo.')
    } finally {
      setConfirmDeleteId(null)
    }
  }

  const messages = detail?.messages ?? []

  return (
    <div className="animate-fade-in">
      <AdvisorPlanBanner />
      <div className="flex h-[calc(100dvh-8.5rem)] rounded-3xl border border-honey-200 dark:border-slate-800 overflow-hidden shadow-card dark:shadow-none bg-white dark:bg-slate-900">

        {/* ── Conversation list ─────────────────────────────────────────────── */}
        <aside className={`${showThread ? 'hidden lg:flex' : 'flex'} flex-col w-full lg:w-72 border-r border-honey-100 dark:border-slate-800 shrink-0`}>
          <div className="flex items-center justify-between gap-2 px-4 py-3 border-b border-honey-100 dark:border-slate-800">
            <div className="flex items-center gap-2 min-w-0">
              <Bot className="w-5 h-5 text-honey-500 shrink-0" />
              <h1 className="font-display font-semibold text-gray-800 dark:text-slate-100 truncate">AI Savjetnik</h1>
            </div>
            <button
              onClick={() => startNew(null)}
              className="shrink-0 flex items-center gap-1 px-2.5 py-1.5 rounded-lg bg-honey-500 hover:bg-honey-600 text-white text-xs font-semibold transition-colors"
            >
              <Plus className="w-3.5 h-3.5" /> Novi
            </button>
          </div>

          <div className="flex-1 overflow-y-auto p-2 space-y-1">
            {listLoading ? (
              <div className="flex justify-center py-8"><Loader2 className="w-5 h-5 animate-spin text-honey-500" /></div>
            ) : conversations.length === 0 ? (
              <p className="text-center text-sm text-gray-400 dark:text-slate-500 py-8 px-4">Još nema razgovora. Započnite novi.</p>
            ) : (
              conversations.map(c => (
                <ConversationItem
                  key={c.id}
                  conversation={c}
                  active={c.id === activeId}
                  onSelect={() => selectConversation(c.id)}
                  onDelete={() => setConfirmDeleteId(c.id)}
                />
              ))
            )}
          </div>
        </aside>

        {/* ── Thread ────────────────────────────────────────────────────────── */}
        <section className={`${showThread ? 'flex' : 'hidden lg:flex'} flex-col flex-1 min-w-0`}>
          {!showThread ? (
            <div className="flex-1 flex flex-col items-center justify-center text-center p-8 gap-4">
              <div className="w-16 h-16 rounded-2xl bg-honey-100 dark:bg-honey-500/15 flex items-center justify-center">
                <Bot className="w-8 h-8 text-honey-500" />
              </div>
              <div>
                <p className="font-display text-lg font-semibold text-gray-700 dark:text-slate-200">Pitajte AI savjetnika</p>
                <p className="mt-1 text-sm text-gray-500 dark:text-slate-400 max-w-sm">
                  Postavite pitanje o pčelama, bolestima, matici ili prihrani — glasom ili tekstom.
                </p>
              </div>
              <button onClick={() => startNew(null)} className="btn-primary text-sm">
                <Plus className="w-4 h-4" /> Novi razgovor
              </button>
            </div>
          ) : (
            <>
              {/* Thread header */}
              <div className="flex items-center gap-2 px-4 py-3 border-b border-honey-100 dark:border-slate-800">
                <button
                  onClick={() => { setActiveId(null); setNewMode(false) }}
                  className="lg:hidden p-1.5 -ml-1.5 rounded-lg text-gray-500 dark:text-slate-400 hover:bg-gray-100 dark:hover:bg-slate-800 transition-colors"
                  aria-label="Nazad na listu"
                >
                  <ArrowLeft className="w-5 h-5" />
                </button>
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-gray-800 dark:text-slate-100 truncate">{detail?.title ?? 'Novi razgovor'}</p>
                </div>
                {chipLabel && (
                  <span className="shrink-0 text-xs text-honey-700 dark:text-honey-300 bg-honey-100 dark:bg-honey-500/15 rounded-full px-2.5 py-1">
                    🐝 {chipLabel}
                  </span>
                )}
                {activeId != null && (
                  <button
                    onClick={() => setConfirmDeleteId(activeId)}
                    className="shrink-0 p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                    aria-label="Obriši razgovor"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                )}
              </div>

              {newMode && messages.length === 0 && !pending ? (
                <div className="flex-1 flex flex-col items-center justify-center text-center p-8 gap-2 text-gray-400 dark:text-slate-500">
                  <Bot className="w-10 h-10 text-honey-300" />
                  <p className="text-sm">Postavite prvo pitanje.</p>
                </div>
              ) : (
                <ChatThread messages={messages} pendingUserText={pending} isThinking={isThinking} />
              )}

              {/* Disclaimer */}
              <p className="px-4 py-1.5 text-[11px] leading-snug text-gray-400 dark:text-slate-500 border-t border-honey-50 dark:border-slate-800/60">
                Savjeti su informativni — za bolesti koje podliježu prijavi obavezno kontaktiraj veterinarsku službu.
              </p>

              <ChatInput onSend={handleSend} disabled={isThinking} />
            </>
          )}
        </section>
      </div>

      <ConfirmDialog
        isOpen={confirmDeleteId !== null}
        title="Obriši razgovor"
        message="Obrisati ovaj razgovor sa savjetnikom? Ova radnja se ne može poništiti."
        onConfirm={handleDelete}
        onCancel={() => setConfirmDeleteId(null)}
        isLoading={deleteMut.isPending}
      />
    </div>
  )
}

// ── Conversation list item ──────────────────────────────────────────────────────

interface ConversationItemProps {
  conversation: AdvisorConversationSummary
  active: boolean
  onSelect: () => void
  onDelete: () => void
}

/** Proactive plan hint (SPEC-09): locked for Free, remaining-quota counter for Standard. */
function AdvisorPlanBanner() {
  const { data: plan } = useMyPlan()
  if (!plan) return null

  const eff = plan.effectivePlan
  if (eff === PlanType.Free) {
    return (
      <Link
        to="/plans"
        className="mb-3 flex items-center gap-2.5 px-4 py-2.5 rounded-xl border border-honey-200 dark:border-honey-500/30
          bg-honey-50 dark:bg-honey-500/10 text-sm font-medium text-honey-800 dark:text-honey-300
          hover:bg-honey-100 dark:hover:bg-honey-500/20 transition-colors"
      >
        🔒 AI savjetnik je dio plaćenih paketa — nadogradite paket da postavljate pitanja.
        <span className="ml-auto underline shrink-0">Pogledaj pakete</span>
      </Link>
    )
  }

  const limit = plan.usage.advisorMessagesLimit
  if (limit != null && limit > 0) {
    const remaining = Math.max(0, limit - plan.usage.advisorMessagesThisMonth)
    return (
      <div className="mb-3 px-4 py-2 rounded-xl bg-gray-50 dark:bg-slate-800/60 text-xs text-gray-500 dark:text-slate-400">
        AI poruke ovog mjeseca: <strong className="text-gray-700 dark:text-slate-200">{plan.usage.advisorMessagesThisMonth}/{limit}</strong>
        {remaining === 0 && ' — iskorišteno. Nadogradite na Pro za neograničeno.'}
      </div>
    )
  }
  return null
}

function ConversationItem({ conversation: c, active, onSelect, onDelete }: ConversationItemProps) {
  return (
    <div
      onClick={onSelect}
      className={`group flex items-start gap-2 px-3 py-2.5 rounded-xl cursor-pointer transition-colors ${
        active ? 'bg-honey-100 dark:bg-honey-500/15' : 'hover:bg-gray-50 dark:hover:bg-slate-800'
      }`}
    >
      <div className="min-w-0 flex-1">
        <p className={`text-sm font-medium truncate ${active ? 'text-honey-800 dark:text-honey-300' : 'text-gray-800 dark:text-slate-100'}`}>
          {c.title}
        </p>
        <div className="flex items-center gap-2 mt-0.5">
          {c.beehiveId != null && (
            <span className="text-[10px] text-honey-600 dark:text-honey-400 bg-honey-50 dark:bg-honey-500/10 rounded px-1.5 py-0.5 truncate max-w-[90px]">
              {c.beehiveName ?? '(obrisana)'}
            </span>
          )}
          <span className="text-[11px] text-gray-400 dark:text-slate-500">
            {formatDistanceToNow(new Date(c.lastMessageAt), { addSuffix: true })}
          </span>
        </div>
      </div>
      <button
        onClick={(e) => { e.stopPropagation(); onDelete() }}
        className="shrink-0 p-1 rounded-lg text-gray-300 dark:text-slate-600 opacity-0 group-hover:opacity-100 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-all"
        aria-label="Obriši razgovor"
      >
        <Trash2 className="w-3.5 h-3.5" />
      </button>
    </div>
  )
}
