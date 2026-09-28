import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { ClipboardPlus, Mic, QrCode, SquareCheck, Zap } from 'lucide-react'
import { TodoPriority, TodoPriorityLabels } from '../../core/models'
import { useAllBeehives, useApiaries, useCreateTodo } from '../../core/services/queries'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useOnlineStatus } from '../../core/hooks/useOnlineStatus'
import { Modal } from '../../shared/components'
import { openAssistant, openQrScanner } from '../../shared/utils/layoutEvents'
import { dashboardQueryKeys } from '../../core/services/dashboardQueries'
import DashboardCard from './DashboardCard'

/**
 * Four shortcuts from the start page (SPEC-29). The scanner and the assistant are the layout's own
 * instances; a new inspection and a new todo first ask where, because both belong to a hive.
 */
export default function QuickActions() {
  const online = useOnlineStatus()
  const { canEditDelete } = usePermissions()
  const [dialog, setDialog] = useState<'inspection' | 'todo' | null>(null)

  return (
    <DashboardCard icon={<Zap className="w-4 h-4" />} title="Brze akcije">
      <div className="grid grid-cols-2 gap-2.5">
        <ActionButton icon={<ClipboardPlus className="w-5 h-5" />} label="Novi pregled" onClick={() => setDialog('inspection')} />
        <ActionButton icon={<QrCode className="w-5 h-5" />} label="Skeniraj QR" onClick={openQrScanner} />
        {canEditDelete && (
          <ActionButton icon={<SquareCheck className="w-5 h-5" />} label="Novi zadatak" onClick={() => setDialog('todo')} />
        )}
        <ActionButton
          icon={<Mic className="w-5 h-5" />}
          label="Glasovni unos"
          onClick={openAssistant}
          disabled={!online}
          hint={online ? undefined : 'Potreban je internet'}
        />
      </div>

      <InspectionTargetModal open={dialog === 'inspection'} onClose={() => setDialog(null)} />
      <QuickTodoModal open={dialog === 'todo'} onClose={() => setDialog(null)} />
    </DashboardCard>
  )
}

function ActionButton({ icon, label, onClick, disabled, hint }: {
  icon: React.ReactNode; label: string; onClick: () => void; disabled?: boolean; hint?: string
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={hint}
      className="flex flex-col items-center justify-center gap-1.5 rounded-2xl border border-honey-200 dark:border-slate-700
                 bg-honey-50/60 dark:bg-slate-800/60 px-3 py-3.5 text-sm font-medium text-honey-800 dark:text-honey-200
                 hover:bg-honey-100 dark:hover:bg-slate-800 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
    >
      {icon}
      {label}
    </button>
  )
}

// ── Where does it go? ─────────────────────────────────────────────────────────

function useHiveOptions() {
  const { data: hives } = useAllBeehives()
  const { data: apiaries } = useApiaries()
  return useMemo(() => {
    const names = new Map((apiaries ?? []).map(a => [a.id, a.name]))
    return (hives ?? [])
      .filter(h => !h.isLocked)
      .map(h => ({ id: h.id, apiaryId: h.apiaryId, label: `${h.name} — ${names.get(h.apiaryId) ?? ''}` }))
      .sort((a, b) => a.label.localeCompare(b.label, 'bs'))
  }, [hives, apiaries])
}

function InspectionTargetModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const options = useHiveOptions()
  const [hiveId, setHiveId] = useState<number | ''>('')

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Novi pregled"
      description="Pregled se upisuje za jednu košnicu."
      size="sm"
      footer={(
        <div className="flex justify-end gap-2">
          <button type="button" className="btn-secondary" onClick={onClose}>Odustani</button>
          <button
            type="button"
            className="btn-primary"
            disabled={!hiveId}
            onClick={() => { onClose(); navigate(`/inspections/new?beehiveId=${hiveId}`) }}
          >
            Nastavi
          </button>
        </div>
      )}
    >
      <label className="form-label" htmlFor="quick-inspection-hive">Košnica</label>
      <select
        id="quick-inspection-hive"
        className="form-input"
        value={hiveId}
        onChange={e => setHiveId(e.target.value ? Number(e.target.value) : '')}
      >
        <option value="">Izaberite košnicu…</option>
        {options.map(o => <option key={o.id} value={o.id}>{o.label}</option>)}
      </select>
    </Modal>
  )
}

function QuickTodoModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const options = useHiveOptions()
  const { data: apiaries } = useApiaries()
  const queryClient = useQueryClient()
  const create = useCreateTodo(dashboardQueryKeys.dashboard)

  const [target, setTarget] = useState('')
  const [title, setTitle] = useState('')
  const [dueDate, setDueDate] = useState('')
  const [priority, setPriority] = useState<TodoPriority>(TodoPriority.Medium)
  const [error, setError] = useState('')

  function reset() {
    setTarget(''); setTitle(''); setDueDate(''); setPriority(TodoPriority.Medium); setError('')
  }

  async function save() {
    if (!target) return setError('Izaberite pčelinjak ili košnicu.')
    if (!title.trim()) return setError('Naziv je obavezan.')
    const [kind, id] = target.split(':')
    try {
      await create.mutateAsync({
        title: title.trim(),
        dueDate: dueDate || null,
        priority,
        apiaryId: kind === 'a' ? Number(id) : undefined,
        beehiveId: kind === 'b' ? Number(id) : undefined,
      })
      await queryClient.invalidateQueries({ queryKey: ['todos'] })
      reset()
      onClose()
    } catch {
      setError('Zadatak nije spremljen. Pokušajte ponovo.')
    }
  }

  return (
    <Modal
      open={open}
      onClose={() => { reset(); onClose() }}
      title="Novi zadatak"
      size="sm"
      footer={(
        <div className="flex justify-end gap-2">
          <button type="button" className="btn-secondary" onClick={() => { reset(); onClose() }}>Odustani</button>
          <button type="button" className="btn-primary" disabled={create.isPending} onClick={save}>
            {create.isPending ? 'Čuvanje…' : 'Spremi'}
          </button>
        </div>
      )}
    >
      <div className="space-y-3">
        <div>
          <label className="form-label" htmlFor="quick-todo-target">Za</label>
          <select id="quick-todo-target" className="form-input" value={target} onChange={e => setTarget(e.target.value)}>
            <option value="">Izaberite…</option>
            <optgroup label="Pčelinjak">
              {(apiaries ?? []).filter(a => !a.isLocked).map(a => <option key={`a${a.id}`} value={`a:${a.id}`}>{a.name}</option>)}
            </optgroup>
            <optgroup label="Košnica">
              {options.map(o => <option key={`b${o.id}`} value={`b:${o.id}`}>{o.label}</option>)}
            </optgroup>
          </select>
        </div>
        <div>
          <label className="form-label" htmlFor="quick-todo-title">Naziv</label>
          <input id="quick-todo-title" className="form-input" value={title} onChange={e => setTitle(e.target.value)} placeholder="Zamijeniti okvire" />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="form-label" htmlFor="quick-todo-due">Rok</label>
            <input id="quick-todo-due" type="date" className="form-input" value={dueDate} onChange={e => setDueDate(e.target.value)} />
          </div>
          <div>
            <label className="form-label" htmlFor="quick-todo-priority">Prioritet</label>
            <select
              id="quick-todo-priority"
              className="form-input"
              value={priority}
              onChange={e => setPriority(Number(e.target.value) as TodoPriority)}
            >
              {[TodoPriority.Low, TodoPriority.Medium, TodoPriority.High].map(p => (
                <option key={p} value={p}>{TodoPriorityLabels[p]}</option>
              ))}
            </select>
          </div>
        </div>
        {error && <p className="text-sm text-red-600 dark:text-red-400" role="alert">{error}</p>}
      </div>
    </Modal>
  )
}
