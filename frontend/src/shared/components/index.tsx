import { AlertTriangle, Loader2, PackageOpen, RotateCw } from 'lucide-react'
import { useRef } from 'react'
import { Modal } from './Modal'

export { VitalCard } from './VitalCard'
export type { VitalCardProps } from './VitalCard'
export { Modal } from './Modal'
export type { ModalProps } from './Modal'

// ── LoadingSpinner ─────────────────────────────────────────────────────────────

export function LoadingSpinner({ message = 'Učitavanje…' }: { message?: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-20 gap-3 animate-fade-in">
      <Loader2 className="w-8 h-8 text-honey-500 animate-spin" />
      <p className="text-sm text-gray-500 dark:text-slate-400">{message}</p>
    </div>
  )
}

// ── Skeleton loaders ────────────────────────────────────────────────────────────

export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`skeleton rounded-xl bg-honey-100/70 dark:bg-slate-800/80 ${className}`} />
}

/** A hero + vitals + content skeleton that mirrors the Command-Center layout. */
export function PageSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-6 animate-fade-in">
      <Skeleton className="h-24 sm:h-28 rounded-3xl" />
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
        {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-[88px] rounded-2xl" />)}
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        {Array.from({ length: rows }).map((_, i) => <Skeleton key={i} className="h-28" />)}
      </div>
    </div>
  )
}

/** Just the vitals row + content rows (for pages that render their own header). */
export function VitalsSkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <>
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
        {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-[88px] rounded-2xl" />)}
      </div>
      <div className="space-y-3">
        {Array.from({ length: rows }).map((_, i) => <Skeleton key={i} className="h-16 rounded-2xl" />)}
      </div>
    </>
  )
}

// ── ErrorMessage ──────────────────────────────────────────────────────────────

export function ErrorMessage({ message }: { message: string }) {
  return (
    <div className="flex items-center gap-3 p-4 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 rounded-xl animate-fade-in">
      <AlertTriangle className="w-5 h-5 text-red-500 shrink-0" />
      <p className="text-sm text-red-700 dark:text-red-300">{message}</p>
    </div>
  )
}

// ── ErrorState ────────────────────────────────────────────────────────────────

interface ErrorStateProps {
  /** What failed, in the user's terms — e.g. "Greška pri učitavanju vrcanja." */
  message?: string
  onRetry?: () => void
}

/**
 * Page-level failure. Use this instead of letting a failed query fall through to `EmptyState`:
 * "Nema vrcanja" on a broken request is indistinguishable from the data actually being gone, which
 * is the worst thing this app can tell a beekeeper.
 *
 * `ErrorMessage` above is the inline banner for a *part* of a page; this replaces the content.
 */
export function ErrorState({ message = 'Greška pri učitavanju podataka.', onRetry }: ErrorStateProps) {
  return (
    <div role="alert" className="flex flex-col items-center justify-center py-16 gap-4 text-center animate-fade-in">
      <div className="w-16 h-16 bg-red-100 dark:bg-red-500/15 rounded-full flex items-center justify-center">
        <AlertTriangle className="w-8 h-8 text-red-500" />
      </div>
      <div>
        <p className="font-display text-lg font-semibold text-gray-700 dark:text-slate-200">{message}</p>
        <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
          Provjerite internet konekciju i pokušajte ponovo.
        </p>
      </div>
      {onRetry && (
        <button onClick={onRetry} className="btn-secondary text-sm">
          <RotateCw className="w-4 h-4" />
          Pokušaj ponovo
        </button>
      )}
    </div>
  )
}

// ── EmptyState ────────────────────────────────────────────────────────────────

interface EmptyStateProps {
  title: string
  description?: string
  action?: React.ReactNode
}

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 gap-4 text-center animate-fade-in">
      <div className="w-16 h-16 bg-honey-100 dark:bg-honey-500/15 rounded-full flex items-center justify-center">
        <PackageOpen className="w-8 h-8 text-honey-400" />
      </div>
      <div>
        <p className="font-display text-lg font-semibold text-gray-700 dark:text-slate-200">{title}</p>
        {description && <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">{description}</p>}
      </div>
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}

// ── ConfirmDialog ─────────────────────────────────────────────────────────────

interface ConfirmDialogProps {
  isOpen: boolean
  title: string
  message: string
  confirmLabel?: string
  onConfirm: () => void
  onCancel: () => void
  isLoading?: boolean
}

export function ConfirmDialog({
  isOpen,
  title,
  message,
  confirmLabel = 'Obriši',
  onConfirm,
  onCancel,
  isLoading,
}: ConfirmDialogProps) {
  // Focus the safe option first — the destructive button should never be one stray Enter away.
  const cancelRef = useRef<HTMLButtonElement>(null)

  return (
    <Modal
      open={isOpen}
      onClose={onCancel}
      title={title}
      size="sm"
      initialFocusRef={cancelRef}
      icon={
        <div className="w-10 h-10 bg-red-100 dark:bg-red-500/15 rounded-full flex items-center justify-center">
          <AlertTriangle className="w-5 h-5 text-red-500" />
        </div>
      }
      footer={
        <div className="flex gap-3 justify-end">
          <button ref={cancelRef} onClick={onCancel} className="btn-secondary text-sm">
            Otkaži
          </button>
          <button onClick={onConfirm} className="btn-danger text-sm" disabled={isLoading}>
            {isLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : confirmLabel}
          </button>
        </div>
      }
    >
      <p className="text-sm text-gray-600 dark:text-slate-400">{message}</p>
    </Modal>
  )
}

// ── PageHeader ────────────────────────────────────────────────────────────────

interface PageHeaderProps {
  title: string
  subtitle?: string
  actions?: React.ReactNode
  backButton?: React.ReactNode
}

export function PageHeader({ title, subtitle, actions, backButton }: PageHeaderProps) {
  return (
    <div className="flex flex-col gap-1 mb-6 sm:flex-row sm:items-center sm:justify-between">
      <div>
        {backButton && <div className="mb-2">{backButton}</div>}
        <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-100">{title}</h1>
        {subtitle && <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">{subtitle}</p>}
      </div>
      {actions && <div className="flex gap-2 mt-3 sm:mt-0">{actions}</div>}
    </div>
  )
}

// ── FormHeader (hero band for forms) ────────────────────────────────────────────

interface FormHeaderProps {
  icon: string
  title: string
  subtitle?: string
  actions?: React.ReactNode
}

/**
 * Hero band for create/edit forms. Used to also render its own "Nazad" link, but Layout's header
 * already has a real back button on every non-root page, so that was pure duplication — removed.
 */
export function FormHeader({ icon, title, subtitle, actions }: FormHeaderProps) {
  return (
    <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                    bg-gradient-to-br from-honey-100 via-white to-honey-50
                    dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none mb-6">
      <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
      <div className="relative p-5 sm:p-6">
        <div className="flex items-center gap-3">
          <div className="w-12 h-12 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-2xl shadow-honey dark:shadow-none">
            {icon}
          </div>
          <div className="min-w-0 flex-1">
            <h1 className="font-display text-xl sm:text-2xl font-bold text-gray-900 dark:text-slate-50 truncate">{title}</h1>
            {subtitle && <p className="text-sm text-gray-500 dark:text-slate-400 mt-0.5 truncate">{subtitle}</p>}
          </div>
          {actions && <div className="flex gap-2 shrink-0">{actions}</div>}
        </div>
      </div>
    </div>
  )
}

// ── HoneyLevelBadge ───────────────────────────────────────────────────────────

import { HoneyLevel } from '../../core/models'

const honeyLevelConfig: Record<HoneyLevel, { label: string; className: string }> = {
  [HoneyLevel.Low]:    { label: 'Nisko',   className: 'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300' },
  [HoneyLevel.Medium]: { label: 'Srednje', className: 'bg-yellow-100 text-yellow-700 dark:bg-yellow-500/15 dark:text-yellow-300' },
  [HoneyLevel.High]:   { label: 'Visoko',  className: 'bg-green-100 text-green-700 dark:bg-green-500/15 dark:text-green-300' },
}

export function HoneyLevelBadge({ level }: { level: HoneyLevel }) {
  const config = honeyLevelConfig[level] ?? { label: 'Nepoznato', className: 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300' }
  return (
    <span className={`badge ${config.className}`}>
      🍯 {config.label}
    </span>
  )
}
