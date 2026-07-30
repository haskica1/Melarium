import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { AUTO_OPEN_KEYS, resolveHelpKey } from './helpRoutes'
import type { HelpEntry } from './helpContent'
import {
  getAutoOpenedKeys,
  hasEverOpenedHelp,
  hasSeenWelcome,
  isAutoOpenEnabled,
  markAutoOpened,
  markHelpUsed,
  markWelcomeSeen,
  setAutoOpenEnabled,
} from './helpStorage'

/**
 * The help content chunk, loaded on first use and kept for the session. The prose is tens of KB and
 * most sessions never open help, so it is split out of the main bundle via a dynamic import — the
 * same approach already used for the PDF font and tesseract.js, not route-level `React.lazy`.
 */
let contentPromise: Promise<Record<string, HelpEntry>> | null = null

function loadContent(): Promise<Record<string, HelpEntry>> {
  contentPromise ??= import('./helpContent').then(m => m.HELP_CONTENT)
  return contentPromise
}

/**
 * Resolves the current route's help entry and owns the panel's open state. Called once, in `Layout`,
 * so the button and the panel agree — the icon is rendered in one place rather than per page.
 */
export function useHelp() {
  const { pathname } = useLocation()
  const { user } = useAuth()
  const email = user?.email

  const helpKey = useMemo(() => resolveHelpKey(pathname), [pathname])

  const [open, setOpen] = useState(false)
  const [entry, setEntry] = useState<HelpEntry | null>(null)
  const [loading, setLoading] = useState(false)
  const [loadFailed, setLoadFailed] = useState(false)
  const [showDot, setShowDot] = useState(() => !hasEverOpenedHelp(email))

  // Read once per mount: these change only through this hook's own setters.
  const [autoOpen, setAutoOpen] = useState(() => isAutoOpenEnabled(email))
  const openedKeysRef = useRef<Set<string>>(new Set(getAutoOpenedKeys(email)))

  // The welcome flow lives here rather than inside its own component so it can hold back the
  // page auto-open below. Both are dialogs; opening them together stacks two focus traps and
  // ambushes the user with two modals on their very first screen.
  const [welcomeOpen, setWelcomeOpen] = useState(() => !!user && !hasSeenWelcome(email))

  const show = useCallback((key: string) => {
    setOpen(true)
    setLoading(true)
    setLoadFailed(false)
    loadContent()
      .then(content => setEntry(content[key] ?? null))
      .catch(() => setLoadFailed(true))
      .finally(() => setLoading(false))
  }, [])

  const openHelp = useCallback(() => {
    if (!helpKey) return
    markHelpUsed(email)
    setShowDot(false)
    show(helpKey)
  }, [helpKey, email, show])

  const closeHelp = useCallback(() => setOpen(false), [])

  /**
   * First visit to one of the three core pages opens help by itself — an icon nobody clicks helps
   * nobody. Fires at most once per page, never when the user has turned it off, and the welcome flow
   * pre-marks every key for people who skip the intro (so existing users aren't ambushed on deploy).
   */
  useEffect(() => {
    if (!helpKey || open || !autoOpen || welcomeOpen) return
    if (!AUTO_OPEN_KEYS.includes(helpKey)) return
    if (openedKeysRef.current.has(helpKey)) return

    openedKeysRef.current.add(helpKey)
    markAutoOpened(email, [helpKey])
    markHelpUsed(email)
    setShowDot(false)
    show(helpKey)
  }, [helpKey, open, autoOpen, welcomeOpen, email, show])

  /** `?` opens the current page's help — ignored while typing, so it can't hijack a form. */
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key !== '?' || e.metaKey || e.ctrlKey || e.altKey) return

      const target = e.target as HTMLElement | null
      const tag = target?.tagName
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target?.isContentEditable) return

      if (!helpKey) return
      e.preventDefault()
      openHelp()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [helpKey, openHelp])

  const disableAutoOpen = useCallback(() => {
    setAutoOpen(false)
    setAutoOpenEnabled(email, false)
  }, [email])

  const changeAutoOpen = useCallback((enabled: boolean) => {
    setAutoOpen(enabled)
    setAutoOpenEnabled(email, enabled)
  }, [email])

  /**
   * Ends the welcome flow. `skipAutoOpen` pre-marks every auto-open page as already seen — that is
   * the "Preskoči uvod" path, and it is what keeps users who were on Melarium before this feature
   * existed from getting a modal on every core page after the deploy.
   */
  const finishWelcome = useCallback((skipAutoOpen: boolean) => {
    markWelcomeSeen(email)
    if (skipAutoOpen) {
      markAutoOpened(email, [...AUTO_OPEN_KEYS])
      for (const key of AUTO_OPEN_KEYS) openedKeysRef.current.add(key)
    }
    setWelcomeOpen(false)
  }, [email])

  return {
    /** null when the current route has no entry — the button renders nothing at all then. */
    helpKey,
    entry,
    open,
    loading,
    loadFailed,
    showDot,
    autoOpen,
    openHelp,
    closeHelp,
    disableAutoOpen,
    changeAutoOpen,
    welcomeOpen,
    finishWelcome,
  }
}
