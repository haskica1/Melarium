import { useState } from 'react'
import { Check, Loader2, Pencil, Plus, Trash2, ChevronDown, ChevronUp, Calendar, User } from 'lucide-react'
import { CollapsibleSection } from './CollapsibleSection'
import { format, parseISO, isPast, isToday } from 'date-fns'
import type { Todo, CreateTodoPayload, UpdateTodoPayload, AssignableUser } from '../../core/models'
import { TodoPriority } from '../../core/models'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useAssignableUsersForBeehive } from '../../core/services/queries'

// ── Helpers ───────────────────────────────────────────────────────────────────

const PRIORITY_STYLES: Record<TodoPriority, string> = {
  [TodoPriority.Low]:    'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
  [TodoPriority.Medium]: 'bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300',
  [TodoPriority.High]:   'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300',
}

const PRIORITY_LABELS: Record<TodoPriority, string> = {
  [TodoPriority.Low]:    'Nizak',
  [TodoPriority.Medium]: 'Srednji',
  [TodoPriority.High]:   'Visok',
}

function dueDateStyle(dateStr?: string, completed?: boolean): string {
  if (!dateStr || completed) return 'text-gray-400 dark:text-slate-500'
  const d = parseISO(dateStr)
  if (isPast(d) && !isToday(d)) return 'text-red-500 dark:text-red-400 font-semibold'
  if (isToday(d)) return 'text-orange-500 dark:text-orange-400 font-semibold'
  return 'text-gray-400 dark:text-slate-500'
}

// ── Inline add / edit form ────────────────────────────────────────────────────

interface TodoFormProps {
  initial?: { title: string; notes: string; dueDate: string; priority: TodoPriority; isCompleted?: boolean; assignedToId?: number | null }
  assignableUsers?: AssignableUser[]
  onSave: (data: { title: string; notes: string; dueDate: string; priority: TodoPriority; isCompleted: boolean; assignedToId: number | null }) => Promise<void>
  onCancel: () => void
  isSaving: boolean
  showCompleted?: boolean
}

function TodoForm({ initial, assignableUsers, onSave, onCancel, isSaving, showCompleted }: TodoFormProps) {
  const [title, setTitle]             = useState(initial?.title ?? '')
  const [notes, setNotes]             = useState(initial?.notes ?? '')
  const [dueDate, setDueDate]         = useState(initial?.dueDate ?? '')
  const [priority, setPriority]       = useState<TodoPriority>(initial?.priority ?? TodoPriority.Medium)
  const [isCompleted, setIsCompleted] = useState(initial?.isCompleted ?? false)
  const [assignedToId, setAssignedToId] = useState<number | null>(initial?.assignedToId ?? null)
  const [titleError, setTitleError]   = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!title.trim()) {
      setTitleError('Naziv je obavezan.')
      return
    }
    setTitleError('')
    await onSave({ title: title.trim(), notes: notes.trim(), dueDate, priority, isCompleted, assignedToId })
  }

  return (
    <div className="card animate-fade-in">
      <form onSubmit={handleSubmit} noValidate className="space-y-5">

        {/* Title */}
        <div>
          <label className="form-label" htmlFor="todo-title">
            Naziv <span className="text-red-500">*</span>
          </label>
          <input
            id="todo-title"
            autoFocus
            type="text"
            placeholder="Šta treba uraditi?"
            value={title}
            onChange={e => { setTitle(e.target.value); if (titleError) setTitleError('') }}
            className="form-input"
            maxLength={200}
          />
          {titleError && <p className="form-error">{titleError}</p>}
        </div>

        {/* Priority + Due date */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="form-label" htmlFor="todo-priority">Prioritet</label>
            <select
              id="todo-priority"
              value={priority}
              onChange={e => setPriority(Number(e.target.value) as TodoPriority)}
              className="form-input"
            >
              <option value={TodoPriority.Low}>Nizak</option>
              <option value={TodoPriority.Medium}>Srednji</option>
              <option value={TodoPriority.High}>Visok</option>
            </select>
          </div>
          <div>
            <label className="form-label" htmlFor="todo-due">Rok izvršenja</label>
            <input
              id="todo-due"
              type="date"
              value={dueDate}
              onChange={e => setDueDate(e.target.value)}
              className="form-input"
            />
          </div>
        </div>

        {/* Notes */}
        <div>
          <label className="form-label" htmlFor="todo-notes">Napomene</label>
          <textarea
            id="todo-notes"
            placeholder="Dodatni detalji (opcionalno)"
            value={notes}
            onChange={e => setNotes(e.target.value)}
            rows={3}
            className="form-input resize-none"
            maxLength={1000}
          />
        </div>

        {/* Assignee */}
        {assignableUsers && assignableUsers.length > 0 && (
          <div>
            <label className="form-label" htmlFor="todo-assignee">Dodijeli</label>
            <select
              id="todo-assignee"
              value={assignedToId ?? ''}
              onChange={e => setAssignedToId(e.target.value ? Number(e.target.value) : null)}
              className="form-input"
            >
              <option value="">— Nije dodijeljeno —</option>
              {assignableUsers.map(u => (
                <option key={u.id} value={u.id}>{u.fullName}</option>
              ))}
            </select>
          </div>
        )}

        {/* Completed toggle (edit mode only) */}
        {showCompleted && (
          <label className="flex items-center gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={isCompleted}
              onChange={e => setIsCompleted(e.target.checked)}
              className="w-4 h-4 accent-honey-500"
            />
            <span className="form-label mb-0">Označi kao završeno</span>
          </label>
        )}

        {/* Actions */}
        <div className="flex gap-3 pt-1">
          <button type="button" onClick={onCancel} className="btn-secondary flex-1">
            Otkaži
          </button>
          <button type="submit" disabled={isSaving} className="btn-primary flex-1">
            {isSaving ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <><Check className="w-4 h-4" /> {initial ? 'Spremi promjene' : 'Dodaj zadatak'}</>
            )}
          </button>
        </div>

      </form>
    </div>
  )
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface TodoSectionProps {
  todos: Todo[]
  isLoading: boolean
  apiaryId?: number
  beehiveId?: number
  assignableUsers?: AssignableUser[]
  /** Whether the current user can create new todos in this context */
  canCreate?: boolean
  /** Whether the current user can edit and delete todos in this context */
  canManage?: boolean
  onCreate: (payload: CreateTodoPayload) => Promise<unknown>
  onUpdate: (id: number, payload: UpdateTodoPayload) => Promise<unknown>
  onDelete: (id: number) => Promise<unknown>
  isMutating: boolean
}

// ── Component ─────────────────────────────────────────────────────────────────

export function TodoSection({
  todos,
  isLoading,
  apiaryId,
  beehiveId,
  assignableUsers,
  canCreate,
  canManage,
  onCreate,
  onUpdate,
  onDelete,
  isMutating,
}: TodoSectionProps) {
  // Fetch beehive-specific assignable users if beehiveId is provided
  const { data: beehiveAssignableUsers } = useAssignableUsersForBeehive(beehiveId ?? 0)

  // Fall back to the hook's canEditDelete only if props are not explicitly provided
  const { canEditDelete } = usePermissions()
  const effectiveCanCreate = canCreate ?? canEditDelete
  const effectiveCanManage = canManage ?? canEditDelete
  const [showAddForm, setShowAddForm]   = useState(false)
  const [editingId, setEditingId]       = useState<number | null>(null)
  const [showCompleted, setShowCompleted] = useState(false)

  // Use beehive-specific assignable users for beehive-scoped todos
  const effectiveAssignableUsers = beehiveId ? beehiveAssignableUsers : assignableUsers

  const open   = todos.filter(t => !t.isCompleted)
  const done   = todos.filter(t => t.isCompleted)

  const handleAdd = async (data: { title: string; notes: string; dueDate: string; priority: TodoPriority; isCompleted: boolean; assignedToId: number | null }) => {
    await onCreate({
      title:        data.title,
      notes:        data.notes || undefined,
      dueDate:      data.dueDate || null,
      priority:     data.priority,
      assignedToId: data.assignedToId,
      apiaryId,
      beehiveId,
    })
    setShowAddForm(false)
  }

  const handleUpdate = async (id: number, _todo: Todo, data: { title: string; notes: string; dueDate: string; priority: TodoPriority; isCompleted: boolean; assignedToId: number | null }) => {
    await onUpdate(id, {
      title:        data.title,
      notes:        data.notes || undefined,
      dueDate:      data.dueDate || null,
      priority:     data.priority,
      isCompleted:  data.isCompleted,
      assignedToId: data.assignedToId,
    })
    setEditingId(null)
  }

  const handleToggle = async (todo: Todo) => {
    await onUpdate(todo.id, {
      title:        todo.title,
      notes:        todo.notes,
      dueDate:      todo.dueDate ?? null,
      priority:     todo.priority,
      isCompleted:  !todo.isCompleted,
      assignedToId: todo.assignedToId ?? null,
    })
  }

  const addAction = effectiveCanCreate && !showAddForm
    ? (
      <button onClick={() => setShowAddForm(true)} className="btn-primary text-sm">
        <Plus className="w-4 h-4" /> Dodaj zadatak
      </button>
    ) : null

  return (
    <CollapsibleSection title="Spisak zadataka" icon="✅" count={open.length} action={addAction}>
      {/* Add form */}
      {showAddForm && (
        <div className="mb-3">
          <TodoForm
            assignableUsers={effectiveAssignableUsers}
            onSave={handleAdd}
            onCancel={() => setShowAddForm(false)}
            isSaving={isMutating}
          />
        </div>
      )}

      {/* Loading */}
      {isLoading && (
        <p className="text-sm text-gray-400 dark:text-slate-500 py-4 text-center">Učitavanje zadataka…</p>
      )}

      {/* Empty state */}
      {!isLoading && todos.length === 0 && !showAddForm && (
        <div className="text-center py-8 border-dashed border-2 border-gray-200 dark:border-slate-700 rounded-xl">
          <p className="text-2xl mb-2">📋</p>
          <p className="text-gray-500 dark:text-slate-400 text-sm">Nema zadataka. Dodajte prvi!</p>
        </div>
      )}

      {/* Open tasks */}
      {open.length > 0 && (
        <div className="space-y-2">
          {open.map(todo => (
            <TodoItem
              key={todo.id}
              todo={todo}
              isEditing={editingId === todo.id}
              isMutating={isMutating}
              canEditDelete={effectiveCanManage}
              assignableUsers={effectiveAssignableUsers}
              onToggle={() => handleToggle(todo)}
              onEdit={() => setEditingId(todo.id)}
              onCancelEdit={() => setEditingId(null)}
              onSaveEdit={(data) => handleUpdate(todo.id, todo, data)}
              onDelete={() => onDelete(todo.id)}
            />
          ))}
        </div>
      )}

      {/* Completed tasks (collapsible) */}
      {done.length > 0 && (
        <div className="mt-4">
          <button
            onClick={() => setShowCompleted(v => !v)}
            className="flex items-center gap-1.5 text-sm text-gray-400 dark:text-slate-500 hover:text-gray-600 dark:hover:text-slate-300 transition-colors mb-2"
          >
            {showCompleted ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
            {done.length} {done.length === 1 ? 'završen zadatak' : 'završenih zadataka'}
          </button>
          {showCompleted && (
            <div className="space-y-2 opacity-70">
              {done.map(todo => (
                <TodoItem
                  key={todo.id}
                  todo={todo}
                  isEditing={editingId === todo.id}
                  isMutating={isMutating}
                  canEditDelete={effectiveCanManage}
                  assignableUsers={effectiveAssignableUsers}
                  onToggle={() => handleToggle(todo)}
                  onEdit={() => setEditingId(todo.id)}
                  onCancelEdit={() => setEditingId(null)}
                  onSaveEdit={(data) => handleUpdate(todo.id, todo, data)}
                  onDelete={() => onDelete(todo.id)}
                />
              ))}
            </div>
          )}
        </div>
      )}
    </CollapsibleSection>
  )
}

// ── Single todo item ──────────────────────────────────────────────────────────

interface TodoItemProps {
  todo: Todo
  isEditing: boolean
  isMutating: boolean
  canEditDelete?: boolean
  assignableUsers?: AssignableUser[]
  onToggle: () => void
  onEdit: () => void
  onCancelEdit: () => void
  onSaveEdit: (data: { title: string; notes: string; dueDate: string; priority: TodoPriority; isCompleted: boolean; assignedToId: number | null }) => Promise<void>
  onDelete: () => void
}

function TodoItem({
  todo,
  isEditing,
  isMutating,
  canEditDelete,
  assignableUsers,
  onToggle,
  onEdit,
  onCancelEdit,
  onSaveEdit,
  onDelete,
}: TodoItemProps) {
  if (isEditing) {
    return (
      <TodoForm
        initial={{
          title:        todo.title,
          notes:        todo.notes ?? '',
          dueDate:      todo.dueDate ? todo.dueDate.slice(0, 10) : '',
          priority:     todo.priority,
          isCompleted:  todo.isCompleted,
          assignedToId: todo.assignedToId ?? null,
        }}
        assignableUsers={assignableUsers}
        showCompleted
        onSave={onSaveEdit}
        onCancel={onCancelEdit}
        isSaving={isMutating}
      />
    )
  }

  return (
    <div className={`card flex items-start gap-3 py-3 animate-slide-up ${todo.isCompleted ? 'bg-gray-50 dark:bg-slate-800/50' : ''}`}>
      {/* Checkbox */}
      <button
        onClick={onToggle}
        className={`mt-0.5 shrink-0 w-5 h-5 rounded-full border-2 flex items-center justify-center transition-colors ${
          todo.isCompleted
            ? 'bg-green-500 border-green-500 text-white'
            : 'border-gray-300 dark:border-slate-600 hover:border-honey-500 dark:hover:border-honey-400'
        }`}
        title={todo.isCompleted ? 'Označi kao otvoreno' : 'Označi kao završeno'}
      >
        {todo.isCompleted && <Check className="w-3 h-3" />}
      </button>

      {/* Content */}
      <div className="flex-1 min-w-0">
        <p className={`text-sm font-medium leading-snug ${todo.isCompleted ? 'line-through text-gray-400 dark:text-slate-500' : 'text-gray-800 dark:text-slate-100'}`}>
          {todo.title}
        </p>

        {todo.notes && (
          <p className="text-xs text-gray-500 dark:text-slate-400 mt-0.5 italic">{todo.notes}</p>
        )}

        <div className="flex flex-wrap items-center gap-2 mt-1.5">
          <span className={`badge text-xs ${PRIORITY_STYLES[todo.priority as TodoPriority]}`}>
            {PRIORITY_LABELS[todo.priority as TodoPriority]}
          </span>

          {todo.dueDate && (
            <span className={`flex items-center gap-0.5 text-xs ${dueDateStyle(todo.dueDate, todo.isCompleted)}`}>
              <Calendar className="w-3 h-3" />
              {format(parseISO(todo.dueDate), 'dd MMM yyyy')}
            </span>
          )}

          {todo.assignedToName && (
            <span className="flex items-center gap-0.5 text-xs text-gray-500 dark:text-slate-400">
              <User className="w-3 h-3" />
              {todo.assignedToName}
            </span>
          )}

          {todo.isCompleted && todo.completedAt && (
            <span className="text-xs text-green-500 dark:text-green-400">
              ✓ Završeno {format(parseISO(todo.completedAt), 'dd MMM')}
            </span>
          )}
        </div>
      </div>

      {/* Actions */}
      {canEditDelete && (
        <div className="flex gap-1 shrink-0">
          <button
            onClick={onEdit}
            className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
            title="Uredi"
          >
            <Pencil className="w-3.5 h-3.5" />
          </button>
          <button
            onClick={onDelete}
            className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
            title="Obriši"
          >
            <Trash2 className="w-3.5 h-3.5" />
          </button>
        </div>
      )}
    </div>
  )
}
