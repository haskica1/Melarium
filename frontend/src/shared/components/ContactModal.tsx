import { useEffect, useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { Check, Copy, Headset, Mail, MessageCircle, MessageCircleMore, Phone } from 'lucide-react'
import clsx from 'clsx'
import { Modal } from './Modal'
import { useAuth } from '../../core/context/AuthContext'
import {
  CONTACT_EMAIL,
  CONTACT_PHONE_DISPLAY,
  CONTACT_PHONE_E164,
  CONTACT_RESPONSE_PROMISE,
  buildEmailMessage,
  buildWhatsappText,
  mailtoUrl,
  telUrl,
  viberUrl,
  whatsappUrl,
  type ContactContext,
} from '../../core/contact/contactInfo'

interface Channel {
  key: string
  icon: React.ReactNode
  label: string
  value: string
  href: string
  /** Put on the clipboard by the row's copy button. Every row has one — see the note below. */
  copyValue: string
  /** Only real web URLs open in a tab; `tel:` and `viber:` hand off to the OS in place. */
  newTab?: boolean
}

/**
 * The one place a user can reach a human (SPEC-20). Rendered as a modal rather than a page, and
 * that is the whole point: `/login` and a `/kontakt` route would be two routes, so opening contact
 * from the sign-in screen would unmount the form and discard the typed email and password — which
 * is precisely the user most likely to need it.
 *
 * Works signed out. `useAuth()` returns null there, and the prefilled messages simply carry less.
 */
export default function ContactModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { user } = useAuth()
  const { pathname } = useLocation()
  const [copiedKey, setCopiedKey] = useState<string | null>(null)
  const [copyFailedKey, setCopyFailedKey] = useState<string | null>(null)
  const valueRefs = useRef<Record<string, HTMLElement | null>>({})

  // The dialog stays mounted while closed, so without this a stale "Kopirano" or failure hint
  // would still be on screen the next time it opens.
  useEffect(() => {
    if (!open) {
      setCopiedKey(null)
      setCopyFailedKey(null)
    }
  }, [open])

  const context: ContactContext | null = user
    ? {
        firstName: user.firstName,
        lastName: user.lastName,
        email: user.email,
        role: user.role,
        organizationName: user.organizationName,
        pathname,
      }
    : null

  const mail = buildEmailMessage(context)

  const channels: Channel[] = [
    {
      key: 'whatsapp',
      icon: <MessageCircle className="w-5 h-5 text-emerald-600 dark:text-emerald-400" />,
      label: 'WhatsApp',
      value: CONTACT_PHONE_DISPLAY,
      href: whatsappUrl(buildWhatsappText(context)),
      copyValue: CONTACT_PHONE_E164,
      newTab: true,
    },
    {
      key: 'viber',
      icon: <MessageCircleMore className="w-5 h-5 text-purple-600 dark:text-purple-400" />,
      label: 'Viber',
      value: CONTACT_PHONE_DISPLAY,
      href: viberUrl(),
      copyValue: CONTACT_PHONE_E164,
    },
    {
      key: 'phone',
      icon: <Phone className="w-5 h-5 text-blue-600 dark:text-blue-400" />,
      label: 'Pozovite nas',
      value: CONTACT_PHONE_DISPLAY,
      href: telUrl(),
      copyValue: CONTACT_PHONE_E164,
    },
    {
      key: 'email',
      icon: <Mail className="w-5 h-5 text-honey-600 dark:text-honey-400" />,
      label: 'Email',
      value: CONTACT_EMAIL,
      href: mailtoUrl(mail.subject, mail.body),
      copyValue: CONTACT_EMAIL,
    },
  ]

  /** Puts the row's own text under the caret so the user can copy it by hand. */
  function selectValue(key: string) {
    const node = valueRefs.current[key]
    if (!node) return
    const range = document.createRange()
    range.selectNodeContents(node)
    const selection = window.getSelection()
    selection?.removeAllRanges()
    selection?.addRange(range)
  }

  async function copy(key: string, value: string) {
    try {
      await navigator.clipboard.writeText(value)
      setCopyFailedKey(null)
      setCopiedKey(key)
      setTimeout(() => setCopiedKey(null), 2000)
    } catch {
      // The Clipboard API is refused on plain http and inside some in-app browsers. Swallowing
      // that would reproduce the exact silent failure this button exists to prevent, so select the
      // text instead and say so — a manual copy still works.
      selectValue(key)
      setCopiedKey(null)
      setCopyFailedKey(key)
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Kontakt i podrška"
      description={`Javite nam se bilo kojim kanalom. ${CONTACT_RESPONSE_PROMISE}`}
      size="sm"
      icon={
        <div className="w-10 h-10 rounded-full bg-honey-100 dark:bg-honey-500/15 flex items-center justify-center">
          <Headset className="w-5 h-5 text-honey-600 dark:text-honey-400" />
        </div>
      }
    >
      <ul className="space-y-2">
        {channels.map(channel => (
          <li key={channel.key}>
            <div className="flex items-center gap-1 rounded-xl border border-gray-200 dark:border-slate-700 hover:border-honey-300 dark:hover:border-honey-500/40 transition-colors">
              <a
                href={channel.href}
                target={channel.newTab ? '_blank' : undefined}
                rel={channel.newTab ? 'noreferrer' : undefined}
                className="flex-1 flex items-center gap-3 min-w-0 pl-3 pr-1 py-3 rounded-xl focus:outline-none focus-visible:ring-2 focus-visible:ring-honey-400"
              >
                <span className="shrink-0">{channel.icon}</span>
                <span className="min-w-0">
                  <span className="block text-sm font-semibold text-gray-800 dark:text-slate-100">
                    {channel.label}
                  </span>
                  <span
                    ref={node => { valueRefs.current[channel.key] = node }}
                    className="block text-xs text-gray-500 dark:text-slate-400 truncate"
                  >
                    {channel.value}
                  </span>
                </span>
              </a>
              {/* Every row copies, deliberately: a `viber:` link on a desktop without Viber
                  installed does nothing at all — no error, no hint — and the user concludes the app
                  is broken. The same goes for `tel:` on a machine with no phone app. */}
              <button
                type="button"
                onClick={() => copy(channel.key, channel.copyValue)}
                className={clsx(
                  'shrink-0 mr-2 p-2 rounded-lg transition-colors',
                  copiedKey === channel.key
                    ? 'text-emerald-600 dark:text-emerald-400'
                    : 'text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800',
                )}
                aria-label={copiedKey === channel.key ? 'Kopirano' : `Kopiraj: ${channel.value}`}
                title={copiedKey === channel.key ? 'Kopirano' : 'Kopiraj'}
              >
                {copiedKey === channel.key ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
              </button>
            </div>
            {copyFailedKey === channel.key && (
              <p role="status" className="mt-1 px-3 text-xs text-gray-500 dark:text-slate-400">
                Kopiranje nije dozvoljeno u ovom pregledniku — tekst je označen, kopirajte ga ručno.
              </p>
            )}
          </li>
        ))}
      </ul>
    </Modal>
  )
}
