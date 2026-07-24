import { useEffect, useRef, useState } from 'react'
import { Bell } from 'lucide-react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { notificationService, type Notification } from '../../core/services/notificationService'
import clsx from 'clsx'
import { formatDistanceToNow } from 'date-fns'

const TYPE_ICONS: Record<string, string> = {
  AccountCreated:          '🎉',
  OrganizationAssigned:    '🏢',
  OrganizationUnassigned:  '🏢',
  ApiaryAssigned:          '🌿',
  ApiaryUnassigned:        '🌿',
  BeehiveAssigned:         '🐝',
  BeehiveUnassigned:       '🐝',
  BeehiveCreated:          '🪵',
  TodoCreated:             '✅',
  // Smart alerts & weekly summary (SPEC-04)
  InspectionOverdue:       '⏰',
  HoneyLevelDrop:          '📉',
  FrostWarning:            '❄️',
  OldQueen:                '👑',
  WeeklySummary:           '📰',
  // Treatment register (SPEC-08)
  StripsLeftIn:            '💊',
  KarencaEnded:            '🍯',
  // Learning module (SPEC-06)
  LearningTopicPublished:  '🎓',
  // Plans & billing (SPEC-09)
  PlanExpiring:            '⏳',
  // Calendar sync (SPEC-11) — daily 08:00 agenda
  DailyAgenda:             '📅',
}

export default function NotificationBell() {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const queryClient = useQueryClient()

  const { data } = useQuery({
    queryKey: ['notifications'],
    queryFn: () => notificationService.getAll(),
    refetchInterval: 30_000,
  })

  const markAllRead = useMutation({
    mutationFn: () => notificationService.markAllRead(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })

  const markRead = useMutation({
    mutationFn: (id: number) => notificationService.markRead(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })

  // Mark all as read when dropdown opens
  useEffect(() => {
    if (open && (data?.unreadCount ?? 0) > 0) {
      markAllRead.mutate()
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  // Close on outside click
  useEffect(() => {
    function onOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    if (open) document.addEventListener('mousedown', onOutside)
    return () => document.removeEventListener('mousedown', onOutside)
  }, [open])

  const unread = data?.unreadCount ?? 0
  const notifications = data?.notifications ?? []

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen(v => !v)}
        className={clsx(
          'relative flex items-center justify-center w-8 h-8 rounded-full transition-all',
          'text-gray-600 dark:text-slate-300 hover:bg-honey-100 dark:hover:bg-slate-800 hover:text-honey-700 dark:hover:text-honey-300',
          open && 'bg-honey-100 dark:bg-slate-800 text-honey-700 dark:text-honey-300'
        )}
        aria-label="Obavještenja"
      >
        <Bell className="w-4 h-4" />
        {unread > 0 && (
          <span className="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 flex items-center justify-center rounded-full bg-red-500 text-white text-[10px] font-bold px-0.5">
            {unread > 99 ? '99+' : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-11 w-80 bg-white dark:bg-slate-900 rounded-2xl shadow-xl border border-gray-100 dark:border-slate-700 overflow-hidden animate-fade-in z-50">
          {/* Header */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 dark:border-slate-800">
            <span className="text-sm font-semibold text-gray-800 dark:text-slate-100">Obavještenja</span>
            {notifications.length > 0 && (
              <button
                onClick={() => markAllRead.mutate()}
                className="text-xs text-honey-600 dark:text-honey-400 hover:text-honey-800 dark:hover:text-honey-300 font-medium transition-colors"
              >
                Označi sve kao pročitano
              </button>
            )}
          </div>

          {/* List */}
          <div className="max-h-96 overflow-y-auto divide-y divide-gray-50 dark:divide-slate-800">
            {notifications.length === 0 ? (
              <div className="px-4 py-8 text-center">
                <Bell className="w-8 h-8 text-gray-300 dark:text-slate-600 mx-auto mb-2" />
                <p className="text-sm text-gray-400 dark:text-slate-500">Nema obavještenja</p>
              </div>
            ) : (
              notifications.map(n => (
                <NotificationItem
                  key={n.id}
                  notification={n}
                  onRead={() => !n.isRead && markRead.mutate(n.id)}
                />
              ))
            )}
          </div>
        </div>
      )}
    </div>
  )
}

function NotificationItem({
  notification: n,
  onRead,
}: {
  notification: Notification
  onRead: () => void
}) {
  const icon = TYPE_ICONS[n.type] ?? '🔔'
  const time = formatDistanceToNow(new Date(n.createdAt), { addSuffix: true })
  // The weekly summary is a multi-line bullet list — render it in full, not clamped to two lines.
  const isMultiline = n.type === 'WeeklySummary'

  return (
    <div
      onClick={onRead}
      className={clsx(
        'flex gap-3 px-4 py-3 cursor-pointer transition-colors',
        n.isRead
          ? 'bg-white dark:bg-slate-900 hover:bg-gray-50 dark:hover:bg-slate-800'
          : 'bg-honey-50 dark:bg-honey-500/10 hover:bg-honey-100 dark:hover:bg-honey-500/20'
      )}
    >
      <span className="text-lg shrink-0 mt-0.5">{icon}</span>
      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-1">
          <p className={clsx('text-sm font-medium truncate', n.isRead ? 'text-gray-700 dark:text-slate-300' : 'text-gray-900 dark:text-slate-100')}>
            {n.title}
          </p>
          {!n.isRead && (
            <span className="w-2 h-2 rounded-full bg-honey-500 shrink-0 mt-1" />
          )}
        </div>
        <p className={clsx('text-xs text-gray-500 dark:text-slate-400 mt-0.5', isMultiline ? 'whitespace-pre-line' : 'line-clamp-2')}>{n.message}</p>
        <p className="text-[10px] text-gray-400 dark:text-slate-500 mt-1">{time}</p>
      </div>
    </div>
  )
}
