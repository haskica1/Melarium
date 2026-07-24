import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import {
  BarChart2, Bot, CalendarDays, ChevronsLeft, ChevronsRight, Droplets, GraduationCap, Home,
  LayoutDashboard, Pill, ReceiptText, Tent, Users,
} from 'lucide-react'
import clsx from 'clsx'

export interface NavRoleFlags {
  isSystemAdmin: boolean
  isOrgAdmin: boolean
  isAdmin: boolean
  canSeeExpenses: boolean
}

export interface NavItemDef {
  to: string
  icon: React.ReactNode
  label: string
}

/** Single source of truth for main nav — consumed by both the desktop Sidebar and the mobile panel. */
export function getNavItems(flags: NavRoleFlags): NavItemDef[] {
  const items: Array<NavItemDef & { visible: boolean }> = [
    flags.isSystemAdmin
      ? { to: '/admin', icon: <LayoutDashboard className="w-4 h-4" />, label: 'Kontrolna ploča', visible: true }
      : { to: '/apiaries', icon: <Home className="w-4 h-4" />, label: 'Pčelinjaci', visible: true },
    { to: '/members', icon: <Users className="w-4 h-4" />, label: 'Članovi', visible: flags.isOrgAdmin || flags.isAdmin },
    { to: '/pastures', icon: <Tent className="w-4 h-4" />, label: 'Pašnjaci', visible: flags.isOrgAdmin || flags.isSystemAdmin },
    { to: '/expenses', icon: <ReceiptText className="w-4 h-4" />, label: 'Troškovi', visible: flags.canSeeExpenses },
    { to: '/harvests', icon: <Droplets className="w-4 h-4" />, label: 'Vrcanja', visible: true },
    { to: '/treatments', icon: <Pill className="w-4 h-4" />, label: 'Tretmani', visible: true },
    { to: '/advisor', icon: <Bot className="w-4 h-4" />, label: 'AI Savjetnik', visible: true },
    { to: '/learning', icon: <GraduationCap className="w-4 h-4" />, label: 'Edukacija', visible: true },
    { to: '/calendar', icon: <CalendarDays className="w-4 h-4" />, label: 'Kalendar', visible: true },
    { to: '/stats', icon: <BarChart2 className="w-4 h-4" />, label: 'Statistike', visible: true },
  ]
  return items.filter(i => i.visible)
}

const STORAGE_KEY = 'melarium-sidebar-expanded'

interface SidebarProps {
  flags: NavRoleFlags
}

/** Desktop icon-rail navigation (replaces the horizontal nav-pill row that used to overflow). */
export function Sidebar({ flags }: SidebarProps) {
  const [expanded, setExpanded] = useState(() => localStorage.getItem(STORAGE_KEY) !== 'false')

  function toggle() {
    setExpanded(v => {
      localStorage.setItem(STORAGE_KEY, String(!v))
      return !v
    })
  }

  return (
    <aside
      className={clsx(
        'hidden sm:flex flex-col shrink-0 h-screen sticky top-0 z-40 border-r border-honey-200 dark:border-slate-800 bg-white/90 dark:bg-slate-900/90 backdrop-blur transition-all duration-200',
        expanded ? 'w-56' : 'w-16',
      )}
    >
      <div className={clsx('h-14 flex items-center border-b border-honey-100 dark:border-slate-800 shrink-0', expanded ? 'px-4 gap-2' : 'justify-center')}>
        <span className="text-2xl leading-none">🐝</span>
        {expanded && (
          <span className="font-display text-lg font-bold text-honey-800 dark:text-honey-300 truncate">Melarium</span>
        )}
      </div>

      <nav className="flex-1 overflow-y-auto py-3 px-2 space-y-1">
        {getNavItems(flags).map(item => (
          <NavLink
            key={item.to}
            to={item.to}
            title={expanded ? undefined : item.label}
            className={({ isActive }) => clsx(
              'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors',
              !expanded && 'justify-center',
              isActive
                ? 'bg-honey-100 dark:bg-honey-500/15 text-honey-800 dark:text-honey-300'
                : 'text-gray-600 dark:text-slate-300 hover:bg-honey-50 dark:hover:bg-slate-800 hover:text-honey-700 dark:hover:text-honey-300',
            )}
          >
            {item.icon}
            {expanded && <span className="truncate">{item.label}</span>}
          </NavLink>
        ))}
      </nav>

      <button
        onClick={toggle}
        className="h-11 flex items-center justify-center gap-2 border-t border-honey-100 dark:border-slate-800 text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors shrink-0"
        aria-label={expanded ? 'Suzi meni' : 'Proširi meni'}
      >
        {expanded ? <ChevronsLeft className="w-4 h-4" /> : <ChevronsRight className="w-4 h-4" />}
      </button>
    </aside>
  )
}
