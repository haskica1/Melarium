import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { ArrowLeft, CloudOff, CreditCard, Headset, LogOut, Menu, MessageSquarePlus, Moon, QrCode, Search, Settings, Sparkles, Sun, UserPlus, X } from 'lucide-react'
import clsx from 'clsx'
import { useAuth } from '../../core/context/AuthContext'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useTheme } from '../../core/hooks/useTheme'
import { useOnlineStatus } from '../../core/hooks/useOnlineStatus'
import { useOutbox } from '../../core/hooks/useOutbox'
import { useOutboxSync } from '../../core/offline/useOutboxSync'
import QrScannerModal from './QrScannerModal'
import NotificationBell from './NotificationBell'
import FeedbackFormModal from './FeedbackFormModal'
import ContactModal from './ContactModal'
import HelpButton from './HelpButton'
import HelpPanel from './HelpPanel'
import WelcomeModal from './WelcomeModal'
import AnnouncementBanner from './AnnouncementBanner'
import { useAnnouncementBanner } from '../../core/services/announcementQueries'
import { useFeedbackSummary } from '../../core/services/feedbackQueries'
import { useHelp } from '../../core/help/useHelp'
import { HelpProvider } from '../../core/help/HelpContext'
import { CommandPalette } from './CommandPalette'
import { Sidebar, getNavItems, type NavRoleFlags } from './Sidebar'
import FabDock, { type FabAction } from './FabDock'
import AssistantSheet from '../../features/assistant/AssistantSheet'
import { ErrorBoundary } from './ErrorBoundary'
import { canGoBack as hasHistoryBehind } from '../utils/historyStack'

// Root/landing pages never show a back arrow, even if browser history technically allows it.
const ROOT_PATHS = ['/apiaries', '/admin']

export default function Layout() {
  const [mobileOpen, setMobileOpen] = useState(false)
  const [profileOpen, setProfileOpen] = useState(false)
  const [scannerOpen, setScannerOpen] = useState(false)
  const [paletteOpen, setPaletteOpen] = useState(false)
  const [feedbackOpen, setFeedbackOpen] = useState(false)
  const [contactOpen, setContactOpen] = useState(false)
  const [assistantOpen, setAssistantOpen] = useState(false)
  const profileRef = useRef<HTMLDivElement>(null)
  const { user, logout } = useAuth()
  const { isDark, toggleTheme } = useTheme()
  const navigate = useNavigate()
  const { pathname } = useLocation()

  // Offline outbox (SPEC-07): sync triggers + live pending count for the badge.
  useOutboxSync()
  const online = useOnlineStatus()
  const outboxItems = useOutbox(user?.email)

  // Role rules come from usePermissions — this used to re-derive them, so the nav and the pages
  // could disagree about who sees what.
  const { isSystemAdmin, isOrgAdmin, isAdmin, canSeeExpenses, canManageMembers, canSeePastures } =
    usePermissions()
  const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform)

  // Untriaged feedback count for the nav badge (SPEC-13) — only SystemAdmin has the endpoint.
  const { data: feedbackSummary } = useFeedbackSummary({ enabled: isSystemAdmin })

  // Unseen announcements for the nav badge (SPEC-21 D8). Same query key as the banner, so this is
  // the cached result, not a second request — and it is what catches an announcement the banner
  // never showed because a newer one had already replaced it.
  const { data: announcementBanner } = useAnnouncementBanner()

  // Per-page help (SPEC-14). Resolved from the route here, once, so the icon sits in the same place
  // on every page instead of being added to thirty page components by hand.
  const help = useHelp()

  const navFlags: NavRoleFlags = {
    isSystemAdmin,
    canSeeExpenses,
    canManageMembers,
    canSeePastures,
    feedbackNewCount: feedbackSummary?.newCount,
    announcementUnreadCount: announcementBanner?.unreadCount,
  }

  // navigate(-1) mirrors real browser back — re-evaluated on every route change via useLocation().
  const canGoBack = !ROOT_PATHS.includes(pathname) && hasHistoryBehind()

  // ── The floating corner (bottom right) ────────────────────────────────────────
  // Both of these used to place themselves independently and landed on top of each other on a
  // phone. FabDock owns the corner now; everything that wants to float there is an entry here.
  //
  // The assistant needs the server for transcription and interpretation, so it hides offline (the
  // SPEC-07 precedent: voice input disappears rather than failing on tap), and it hides on its own
  // page, where a shortcut to where you already are is just noise.
  const assistantAvailable = online && !pathname.startsWith('/assistant')

  const fabActions: FabAction[] = [
    // Scan is mobile-only here because the desktop header already carries it. Hidden while the
    // hamburger panel is open: the panel reaches the bottom of the viewport and this sits over the
    // right-hand end of its last row, so a tap there used to open the scanner instead of signing
    // out. The panel has its own "Skeniraj" entry, so nothing is lost.
    ...(!mobileOpen ? [{
      key: 'scan',
      mobileOnly: true,
      label: 'Skeniraj QR kod košnice',
      icon: <QrCode className="w-6 h-6" />,
      onClick: () => setScannerOpen(true),
    }] : []),
    ...(assistantAvailable ? [{
      key: 'assistant',
      label: 'Otvori AI asistenta',
      icon: <Sparkles className="w-6 h-6" />,
      onClick: () => setAssistantOpen(true),
    }] : []),
  ]

  const avatarClass = isSystemAdmin
    ? 'bg-purple-100 text-purple-700 dark:bg-purple-500/20 dark:text-purple-300'
    : isOrgAdmin
    ? 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-300'
    : 'bg-honey-100 text-honey-700 dark:bg-honey-500/20 dark:text-honey-300'

  const roleLabel = isSystemAdmin
    ? 'Sistem Admin'
    : isOrgAdmin
    ? `Org Admin · ${user?.organizationName}`
    : isAdmin
    ? `Admin · ${user?.organizationName}`
    : user?.organizationName ?? ''

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  // Close profile dropdown on outside click
  useEffect(() => {
    function onOutsideClick(e: MouseEvent) {
      if (profileRef.current && !profileRef.current.contains(e.target as Node)) {
        setProfileOpen(false)
      }
    }
    if (profileOpen) document.addEventListener('mousedown', onOutsideClick)
    return () => document.removeEventListener('mousedown', onOutsideClick)
  }, [profileOpen])

  // Open the command palette with Ctrl/Cmd+K
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        setPaletteOpen(v => !v)
      }
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [])

  // Close the assistant sheet on navigation (its context is the page it was opened from, and
  // carrying that to the next page would make it a lie) and when the assistant stops being
  // available mid-session — dropping offline used to unmount the whole thing.
  useEffect(() => {
    setAssistantOpen(false)
  }, [pathname, assistantAvailable])

  return (
    <div className="min-h-screen flex">
      <Sidebar flags={navFlags} />

      <div className="flex-1 flex flex-col min-w-0">

        {/* ── Header ────────────────────────────────────────────────────────────── */}
        <header className="sticky top-0 z-30 bg-white/90 dark:bg-slate-900/90 backdrop-blur border-b border-honey-200 dark:border-slate-800 shadow-sm dark:shadow-none">
          <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between gap-4">

            {/* Back (real history back — mirrors browser back) + mobile-only logo */}
            <div className="flex items-center gap-2 min-w-0">
              {canGoBack && (
                <button
                  onClick={() => navigate(-1)}
                  className="shrink-0 p-2 rounded-lg text-gray-500 dark:text-slate-300 hover:bg-honey-100 dark:hover:bg-slate-800 hover:text-honey-700 dark:hover:text-honey-300 transition-colors"
                  aria-label="Nazad"
                  title="Nazad"
                >
                  <ArrowLeft className="w-4 h-4" />
                </button>
              )}
              <Link
                to={isSystemAdmin ? '/admin' : '/apiaries'}
                className="sm:hidden flex items-center gap-2 group shrink-0"
              >
                <span className="text-2xl leading-none">🐝</span>
                <span className="font-display text-xl font-bold text-honey-800 dark:text-honey-300 group-hover:text-honey-600 dark:group-hover:text-honey-400 transition-colors">
                  Melarium
                </span>
              </Link>
            </div>

            {/* ── Desktop utilities ───────────────────────────────────────────── */}
            <div className="hidden sm:flex items-center gap-3">

              {/* Scan (Skeniraj) — kept in the header for quick, always-visible access */}
              <button
                onClick={() => setScannerOpen(true)}
                className="w-8 h-8 rounded-full flex items-center justify-center text-honey-600 dark:text-honey-400 hover:bg-honey-100 dark:hover:bg-honey-500/20 transition-colors"
                aria-label="Skeniraj QR kod"
                title="Skeniraj"
              >
                <QrCode className="w-[18px] h-[18px]" />
              </button>

              {/* Command palette trigger */}
              <button
                onClick={() => setPaletteOpen(true)}
                className="hidden md:flex items-center gap-2 pl-3 pr-2 py-1.5 rounded-xl text-sm text-gray-500 dark:text-slate-400 bg-gray-100 dark:bg-slate-800 hover:bg-gray-200 dark:hover:bg-slate-700 transition-colors"
                aria-label="Otvori pretragu"
              >
                <Search className="w-4 h-4" />
                <span className="hidden lg:inline">Pretraži</span>
                <kbd className="text-[10px] font-mono bg-white dark:bg-slate-900 border border-gray-200 dark:border-slate-700 rounded px-1.5 py-0.5 leading-none">
                  {isMac ? '⌘K' : 'Ctrl K'}
                </kbd>
              </button>

              {/* Per-page help (SPEC-14) — omitted entirely on pages with no entry */}
              {help.helpKey && (
                <HelpButton onClick={help.openHelp} showDot={help.showDot} />
              )}

              {/* Offline outbox badge (SPEC-07) */}
              {outboxItems.length > 0 && (
                <Link
                  to="/outbox"
                  className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-full text-xs font-semibold
                    bg-amber-100 text-amber-800 hover:bg-amber-200
                    dark:bg-amber-500/15 dark:text-amber-300 dark:hover:bg-amber-500/25 transition-colors"
                  title="Neposlani pregledi"
                >
                  <CloudOff className="w-3.5 h-3.5" />
                  {outboxItems.length}
                </Link>
              )}

              {/* Notification bell */}
              <NotificationBell />

              {/* Profile avatar + dropdown */}
              <div ref={profileRef} className="relative">
                <button
                  onClick={() => setProfileOpen(v => !v)}
                  className={clsx(
                    'w-8 h-8 rounded-full flex items-center justify-center font-semibold text-sm select-none',
                    'transition-all hover:ring-2 hover:ring-honey-300 hover:ring-offset-1',
                    avatarClass,
                    profileOpen && 'ring-2 ring-honey-400 ring-offset-1'
                  )}
                  aria-label="Otvori meni profila"
                >
                  {user?.firstName[0] ?? '?'}
                </button>

                {/* Dropdown */}
                {profileOpen && (
                  <div className="absolute right-0 top-11 w-56 bg-white dark:bg-slate-800 rounded-2xl shadow-xl border border-gray-100 dark:border-slate-700 overflow-hidden animate-fade-in">
                    {/* User info */}
                    <div className="px-4 py-3 border-b border-gray-100 dark:border-slate-700">
                      <div className="flex items-center gap-2.5">
                        <div className={clsx('w-9 h-9 rounded-full flex items-center justify-center font-semibold text-sm shrink-0', avatarClass)}>
                          {user?.firstName[0]}
                        </div>
                        <div className="min-w-0">
                          <p className="text-sm font-semibold text-gray-800 dark:text-slate-100 truncate">
                            {user?.firstName} {user?.lastName}
                          </p>
                          <p className="text-xs text-gray-500 dark:text-slate-400 truncate mt-0.5">{roleLabel}</p>
                        </div>
                      </div>
                    </div>
                    {/* Edit profile */}
                    <button
                      onClick={() => { setProfileOpen(false); navigate('/profile') }}
                      className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
                    >
                      <Settings className="w-4 h-4" />
                      Uredi profil
                    </button>
                    {/* Theme toggle — moved off the header into the menu to declutter the toolbar */}
                    <button
                      onClick={toggleTheme}
                      className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
                    >
                      {isDark ? <Sun className="w-4 h-4" /> : <Moon className="w-4 h-4" />}
                      {isDark ? 'Svjetla tema' : 'Tamna tema'}
                    </button>
                    {/* Plan (SPEC-09) */}
                    <button
                      onClick={() => { setProfileOpen(false); navigate('/plans') }}
                      className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
                    >
                      <CreditCard className="w-4 h-4" />
                      Paket i pretplata
                    </button>
                    {/* Invite a friend (SPEC-15). Here rather than in the sidebar: the sidebar is
                        the daily working set, and a referral link is not a daily tool. */}
                    <button
                      onClick={() => { setProfileOpen(false); navigate('/invite') }}
                      className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
                    >
                      <UserPlus className="w-4 h-4" />
                      Pozovi prijatelja
                    </button>
                    {/* Contact (SPEC-20) — a direct line to a human, sitting next to the async
                        form so the two read as the pair they are. */}
                    <button
                      onClick={() => { setProfileOpen(false); setContactOpen(true) }}
                      className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
                    >
                      <Headset className="w-4 h-4" />
                      Kontakt i podrška
                    </button>
                    {/* Feedback (SPEC-13) — reachable from every page, whatever the user is doing */}
                    <button
                      onClick={() => { setProfileOpen(false); setFeedbackOpen(true) }}
                      className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
                    >
                      <MessageSquarePlus className="w-4 h-4" />
                      Prijavi problem / pohvali
                    </button>
                    {/* Sign out */}
                    <button
                      onClick={() => { setProfileOpen(false); handleLogout() }}
                      className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                    >
                      <LogOut className="w-4 h-4" />
                      Odjavi se
                    </button>
                  </div>
                )}
              </div>
            </div>

            {/* ── Mobile: search + help + notifications + dark toggle + hamburger ────── */}
            <div className="sm:hidden flex items-center gap-0.5">
              <button
                onClick={() => setPaletteOpen(true)}
                className="p-2 rounded-lg text-gray-600 dark:text-slate-300 hover:bg-honey-100 dark:hover:bg-slate-800 transition-colors"
                aria-label="Pretraži"
              >
                <Search className="w-5 h-5" />
              </button>
              {/* This app is used on a phone in the field far more than on a desktop, so help has to
                  be one tap away here too — not buried in the hamburger menu. */}
              {help.helpKey && (
                <HelpButton onClick={help.openHelp} showDot={help.showDot} variant="mobile" />
              )}
              {/* Smart alerts (frost, overdue inspections, end of karenca) are the reason to open
                  the app in the field — the bell has to be reachable on a phone, not just desktop. */}
              <NotificationBell />
              <button
                className="p-2 rounded-lg text-gray-600 dark:text-slate-300 hover:bg-honey-100 dark:hover:bg-slate-800 transition-colors"
                onClick={() => setMobileOpen(v => !v)}
                aria-label="Otvori/zatvori meni"
              >
                {mobileOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
              </button>
            </div>
          </div>

          {/* Mobile panel */}
          {mobileOpen && (
            <div className="mobile-menu-panel sm:hidden border-t border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 px-4 py-3 space-y-1 animate-fade-in">
              {/* Nav items — shared list with the desktop Sidebar */}
              {getNavItems(navFlags).map(item => (
                <MobileNavItem
                  key={item.to}
                  to={item.to}
                  icon={item.icon}
                  label={item.label}
                  badge={item.badge}
                  onClick={() => setMobileOpen(false)}
                />
              ))}
              <button
                onClick={() => { setMobileOpen(false); setScannerOpen(true) }}
                className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
              >
                <QrCode className="w-4 h-4 text-honey-600 dark:text-honey-400" />
                Skeniraj
              </button>
              {outboxItems.length > 0 && (
                <MobileNavItem
                  to="/outbox"
                  icon={<CloudOff className="w-4 h-4" />}
                  label={`Neposlani pregledi (${outboxItems.length})`}
                  onClick={() => setMobileOpen(false)}
                />
              )}

              {/* User section */}
              {user && (
                <div className="pt-2 mt-1 border-t border-honey-100 dark:border-slate-800 space-y-1">
                  <div className="flex items-center gap-3 px-3 py-2.5 rounded-xl bg-gray-50 dark:bg-slate-800">
                    <div className={clsx('w-8 h-8 rounded-full flex items-center justify-center font-semibold text-sm shrink-0', avatarClass)}>
                      {user.firstName[0]}
                    </div>
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-gray-800 dark:text-slate-100 truncate">
                        {user.firstName} {user.lastName}
                      </p>
                      <p className="text-xs text-gray-500 dark:text-slate-400 truncate">{roleLabel}</p>
                    </div>
                  </div>
                  <button
                    onClick={() => { setMobileOpen(false); navigate('/profile') }}
                    className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                  >
                    <Settings className="w-4 h-4 text-honey-600 dark:text-honey-400" />
                    Uredi profil
                  </button>
                  <button
                    onClick={toggleTheme}
                    className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                  >
                    {isDark
                      ? <Sun className="w-4 h-4 text-honey-600 dark:text-honey-400" />
                      : <Moon className="w-4 h-4 text-honey-600 dark:text-honey-400" />}
                    {isDark ? 'Svjetla tema' : 'Tamna tema'}
                  </button>
                  <button
                    onClick={() => { setMobileOpen(false); navigate('/plans') }}
                    className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                  >
                    <CreditCard className="w-4 h-4 text-honey-600 dark:text-honey-400" />
                    Paket i pretplata
                  </button>
                  {/* The second copy of the profile menu — the desktop dropdown above is the first.
                      Both must carry every entry; missing this one is the classic bug here. */}
                  <button
                    onClick={() => { setMobileOpen(false); navigate('/invite') }}
                    className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                  >
                    <UserPlus className="w-4 h-4 text-honey-600 dark:text-honey-400" />
                    Pozovi prijatelja
                  </button>
                  <button
                    onClick={() => { setMobileOpen(false); setContactOpen(true) }}
                    className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                  >
                    <Headset className="w-4 h-4 text-honey-600 dark:text-honey-400" />
                    Kontakt i podrška
                  </button>
                  <button
                    onClick={() => { setMobileOpen(false); setFeedbackOpen(true) }}
                    className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                  >
                    <MessageSquarePlus className="w-4 h-4 text-honey-600 dark:text-honey-400" />
                    Prijavi problem / pohvali
                  </button>
                  <button
                    onClick={() => { setMobileOpen(false); handleLogout() }}
                    className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                  >
                    <LogOut className="w-4 h-4" />
                    Odjavi se
                  </button>
                </div>
              )}
            </div>
          )}
        </header>

        {/* ── Offline banner (SPEC-07) ─────────────────────────────────────────── */}
        {!online && (
          <div className="sticky top-14 z-20 bg-amber-100 dark:bg-amber-500/15 border-b border-amber-200 dark:border-amber-500/30 text-amber-800 dark:text-amber-300">
            <div className="max-w-5xl mx-auto px-4 py-2 flex items-center gap-2 text-sm font-medium">
              <CloudOff className="w-4 h-4 shrink-0" />
              Radiš offline — izmjene se čuvaju lokalno.
              {outboxItems.length > 0 && (
                <Link to="/outbox" className="ml-auto underline hover:no-underline shrink-0">
                  Neposlano: {outboxItems.length}
                </Link>
              )}
            </div>
          </div>
        )}

        {/* ── Main Content ──────────────────────────────────────────────────────── */}
        <main className="flex-1 max-w-5xl mx-auto w-full px-4 py-6">
          {/* Outside the boundary on purpose (SPEC-21): the banner is not part of any page, so a
              page that crashes should not take the announcement down with it. */}
          <AnnouncementBanner />
          {/* Keyed on the path so navigating away clears a crashed page — without the key the
              boundary would stay in its error state for the rest of the session. */}
          <ErrorBoundary key={pathname}>
            {/* Lets pages (e.g. an EmptyState) open the single help panel Layout owns. */}
            <HelpProvider value={{ openHelp: help.openHelp, hasHelp: !!help.helpKey }}>
              <Outlet />
            </HelpProvider>
          </ErrorBoundary>
        </main>

        {/* ── Footer ────────────────────────────────────────────────────────────── */}
        <footer className="border-t border-honey-200 dark:border-slate-800 bg-white dark:bg-slate-900 py-4 px-4 text-center text-xs text-gray-400 dark:text-slate-500">
          <p>Melarium App © {new Date().getFullYear()} — Čuvajte vaše kolonije zdravim 🍯</p>
          {/* The footer was dead space, and contact has to be findable without already knowing to
              look under the profile avatar (SPEC-20). */}
          <button
            onClick={() => setContactOpen(true)}
            className="mt-1.5 inline-flex items-center gap-1.5 text-gray-500 dark:text-slate-400 hover:text-honey-600 dark:hover:text-honey-400 hover:underline transition-colors"
          >
            <Headset className="w-3.5 h-3.5" />
            Kontakt i podrška
          </button>
        </footer>
      </div>

      {/* ── Floating actions: scan + AI assistant, one capsule ────────────────── */}
      <FabDock actions={fabActions} />

      {/* AI assistant (SPEC-17) — reachable from every page, so a command can be given without
          navigating away from the hive being looked at. Closes on navigation: its context is the
          page you were on, and carrying that to the next page would make it a lie. */}
      <AssistantSheet open={assistantOpen} onClose={() => setAssistantOpen(false)} />

      {/* ── QR Scanner Modal ──────────────────────────────────────────────────── */}
      {scannerOpen && <QrScannerModal onClose={() => setScannerOpen(false)} />}

      {/* ── Command palette (Ctrl/Cmd+K) ──────────────────────────────────────── */}
      <CommandPalette open={paletteOpen} onClose={() => setPaletteOpen(false)} />

      {/* ── Feedback form (SPEC-13) ───────────────────────────────────────────── */}
      <FeedbackFormModal open={feedbackOpen} onClose={() => setFeedbackOpen(false)} />

      {/* ── Contact (SPEC-20) ─────────────────────────────────────────────────── */}
      {/* One instance, opened from the footer and from both copies of the profile menu. */}
      <ContactModal open={contactOpen} onClose={() => setContactOpen(false)} />

      {/* ── Per-page help + first-run welcome (SPEC-14) ────────────────────────── */}
      <HelpPanel
        open={help.open}
        onClose={help.closeHelp}
        entry={help.entry}
        loading={help.loading}
        loadFailed={help.loadFailed}
        role={user?.role}
        autoOpen={help.autoOpen}
        onDisableAutoOpen={help.disableAutoOpen}
      />
      <WelcomeModal open={help.welcomeOpen} onFinish={help.finishWelcome} />
    </div>
  )
}

// ── Mobile nav item ───────────────────────────────────────────────────────────

function MobileNavItem({ to, icon, label, badge, onClick }: {
  to: string
  icon: React.ReactNode
  label: string
  badge?: number
  onClick: () => void
}) {
  return (
    <NavLink
      to={to}
      onClick={onClick}
      className={({ isActive }) =>
        clsx(
          'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors',
          isActive
            ? 'bg-honey-100 dark:bg-honey-500/15 text-honey-800 dark:text-honey-300'
            : 'text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-800'
        )
      }
    >
      <span className="text-honey-600 dark:text-honey-400">{icon}</span>
      {label}
      {!!badge && badge > 0 && (
        <span className="ml-auto min-w-[18px] h-[18px] rounded-full bg-red-500 text-white text-[10px] font-bold flex items-center justify-center px-1 shrink-0">
          {badge > 99 ? '99+' : badge}
        </span>
      )}
    </NavLink>
  )
}
