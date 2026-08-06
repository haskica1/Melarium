import { useState } from 'react'
import { Check, Copy, Gift, Share2, UserPlus } from 'lucide-react'
import clsx from 'clsx'
import { useInviteSummary, useMyInvitations } from '../../core/services/inviteQueries'
import { InvitationStatus, type Invitation } from '../../core/models'
import { useHelpTrigger } from '../../core/help/HelpContext'
import { EmptyState, ErrorState, PageSkeleton } from '../../shared/components'

const STATUS_STYLE: Record<InvitationStatus, string> = {
  [InvitationStatus.Sent]:     'bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300',
  [InvitationStatus.Accepted]: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('bs-BA', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

export default function InvitePage() {
  const { openHelp } = useHelpTrigger()
  const { data: summary, isLoading, isError, refetch } = useInviteSummary()
  const { data: invitations } = useMyInvitations()

  const [copied, setCopied] = useState(false)

  if (isLoading) return <PageSkeleton />
  if (isError || !summary) return <ErrorState message="Greška pri učitavanju pozivnica." onRetry={() => refetch()} />

  async function copyLink() {
    if (!summary) return
    try {
      await navigator.clipboard.writeText(summary.shareUrl)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch { /* clipboard blocked — the field is selectable by hand */ }
  }

  async function share() {
    if (!summary) return
    try {
      await navigator.share({
        title: 'Melarium',
        text: `Koristim Melarium za vođenje pčelinjaka — pridruži se, dobijaš ${summary.inviteeTrialDays} dana Pro paketa.`,
        url: summary.shareUrl,
      })
    } catch { /* the user dismissed the share sheet — not an error */ }
  }

  // Feature-detected rather than sniffed: the share sheet exists on phones, which is where this
  // matters, and its absence on desktop just means the copy button carries the card.
  const canShare = typeof navigator !== 'undefined' && !!navigator.share

  const capReached = summary.rewardDaysRemaining <= 0

  return (
    <div className="max-w-3xl mx-auto animate-fade-in space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold text-gray-800 dark:text-slate-100">Pozovi prijatelja</h1>
        <p className="text-sm text-gray-500 dark:text-slate-400 mt-1">
          Pošaljite svoj link prijatelju pčelaru. On dobija <strong>{summary.inviteeTrialDays} dana</strong> Pro
          paketa umjesto uobičajenih 30, a vi <strong>{summary.rewardDaysPerInvite} dana</strong> kad potvrdi
          svoju e-poštu.
        </p>
      </div>

      {/* ── Share card ── */}
      <div className="bg-white dark:bg-slate-800 rounded-xl border border-honey-200 dark:border-slate-700 p-5 space-y-3">
        <label htmlFor="share-url" className="block text-sm font-medium text-gray-700 dark:text-slate-300">
          Vaš lični link
        </label>
        <div className="flex flex-col sm:flex-row gap-2">
          <input
            id="share-url"
            readOnly
            value={summary.shareUrl}
            onFocus={e => e.currentTarget.select()}
            className="flex-1 min-w-0 px-3.5 py-2.5 rounded-lg border border-gray-200 dark:border-slate-600 bg-gray-50 dark:bg-slate-900 text-sm text-gray-700 dark:text-slate-200 font-mono"
          />
          <div className="flex gap-2">
            <button
              type="button"
              onClick={copyLink}
              className="flex items-center justify-center gap-1.5 px-4 py-2.5 rounded-lg bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
            >
              {copied ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
              {copied ? 'Kopirano' : 'Kopiraj'}
            </button>
            {canShare && (
              <button
                type="button"
                onClick={share}
                className="flex items-center justify-center gap-1.5 px-4 py-2.5 rounded-lg border border-honey-300 dark:border-slate-600 text-honey-700 dark:text-honey-300 text-sm font-semibold hover:bg-honey-50 dark:hover:bg-slate-700 transition-colors"
              >
                <Share2 className="w-4 h-4" />
                Podijeli
              </button>
            )}
          </div>
        </div>
        <p className="text-xs text-gray-500 dark:text-slate-400">
          Pošaljite ga kroz Viber, WhatsApp ili poruku. Osoba koja se registruje preko njega dobija
          <strong> svoju</strong> organizaciju u Melariumu.
        </p>
      </div>

      {/* ── Stats ── */}
      <div className="grid grid-cols-3 gap-3">
        <Stat label="Poslano" value={summary.sentCount} />
        <Stat label="Pridružilo se" value={summary.acceptedCount} />
        <Stat label="Osvojeno dana" value={`+${summary.rewardDaysEarned}`} accent />
      </div>

      {capReached ? (
        <p className="flex items-start gap-2 text-sm text-gray-500 dark:text-slate-400 bg-gray-50 dark:bg-slate-800/60 rounded-lg p-3">
          <Gift className="w-4 h-4 mt-0.5 shrink-0 text-honey-500" />
          Dostigli ste maksimum nagradnih dana za svoju organizaciju. Pozivnice i dalje rade — prijatelji
          dobijaju svojih {summary.inviteeTrialDays} dana.
        </p>
      ) : (
        <p className="text-xs text-gray-400 dark:text-slate-500">
          Još {summary.rewardDaysRemaining} dana možete osvojiti pozivnicama.
        </p>
      )}

      {/* ── History ── */}
      <div>
        <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100 mb-3">
          Vaše pozivnice
        </h2>
        {!invitations?.length ? (
          <EmptyState
            title="Još niste nikoga pozvali"
            description="Pošaljite svoj link prijatelju pčelaru — ovdje ćete vidjeti ko se pridružio."
            onHelp={openHelp}
          />
        ) : (
          <ul className="space-y-2">
            {invitations.map(inv => <InvitationRow key={inv.id} invitation={inv} />)}
          </ul>
        )}
      </div>
    </div>
  )
}

function Stat({ label, value, accent }: { label: string; value: number | string; accent?: boolean }) {
  return (
    <div className="bg-white dark:bg-slate-800 rounded-xl border border-gray-200 dark:border-slate-700 p-4 text-center">
      <p className={clsx(
        'font-display text-2xl font-bold',
        accent ? 'text-honey-600 dark:text-honey-400' : 'text-gray-800 dark:text-slate-100',
      )}>
        {value}
      </p>
      <p className="text-xs text-gray-500 dark:text-slate-400 mt-0.5">{label}</p>
    </div>
  )
}

function InvitationRow({ invitation }: { invitation: Invitation }) {
  return (
    <li className="flex items-center gap-3 bg-white dark:bg-slate-800 rounded-lg border border-gray-200 dark:border-slate-700 px-4 py-3">
      <UserPlus className="w-4 h-4 shrink-0 text-gray-400 dark:text-slate-500" />
      <div className="min-w-0 flex-1">
        <p className="text-sm text-gray-800 dark:text-slate-200 truncate">{invitation.email}</p>
        <p className="text-xs text-gray-400 dark:text-slate-500">
          {invitation.sourceName} · {formatDate(invitation.createdAt)}
        </p>
      </div>
      {!!invitation.rewardDays && (
        <span className="text-xs font-semibold text-honey-600 dark:text-honey-400 shrink-0">
          +{invitation.rewardDays} dana
        </span>
      )}
      {/* statusName carries all three states, including "čeka potvrdu" — rendered as sent by the API. */}
      <span className={clsx(
        'text-xs font-medium px-2.5 py-1 rounded-full shrink-0',
        STATUS_STYLE[invitation.status],
      )}>
        {invitation.statusName}
      </span>
    </li>
  )
}
