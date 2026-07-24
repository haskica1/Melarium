import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { format } from 'date-fns'
import clsx from 'clsx'
import { Camera, Loader2, PencilLine, Plus, ReceiptText, Trash2 } from 'lucide-react'
import { useExpenses, useDeleteExpense } from '../../core/services/expenseQueries'
import { ExpenseSource, ExpenseSourceLabels } from '../../core/models'
import type { Expense } from '../../core/models'
import { VitalCard, VitalsSkeleton, ConfirmDialog } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'

type SourceFilter = 'all' | 'manual' | 'scan'

const FILTERS: { key: SourceFilter; label: string }[] = [
  { key: 'all',    label: 'Sve' },
  { key: 'manual', label: 'Ručno' },
  { key: 'scan',   label: 'Skenirani' },
]

export default function ExpensesPage() {
  const navigate = useNavigate()
  const { data: expenses = [], isLoading } = useExpenses()
  const deleteExpense = useDeleteExpense()
  const { toast } = useToast()
  const [confirmTarget, setConfirmTarget] = useState<Expense | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const [filter, setFilter] = useState<SourceFilter>('all')

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await deleteExpense.mutateAsync(confirmTarget.id)
      toast.success('Trošak obrisan.')
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? e?.message ?? 'Greška pri brisanju troška.')
    } finally {
      setIsDeleting(false)
    }
  }

  // ── Derived vitals ──
  const currency = expenses[0]?.currency ?? 'BAM'
  const totalSpent = expenses.reduce((sum, e) => sum + e.totalAmount, 0)
  const avg = expenses.length ? totalSpent / expenses.length : 0
  const thisMonthTotal = useMemo(() => {
    const now = new Date()
    const ym = `${now.getFullYear()}-${now.getMonth()}`
    return expenses
      .filter(e => { const d = new Date(e.purchaseDate); return `${d.getFullYear()}-${d.getMonth()}` === ym })
      .reduce((s, e) => s + e.totalAmount, 0)
  }, [expenses])

  const visible = useMemo(() => {
    if (filter === 'all') return expenses
    if (filter === 'scan') return expenses.filter(e => e.source === ExpenseSource.ReceiptScan)
    return expenses.filter(e => e.source !== ExpenseSource.ReceiptScan)
  }, [expenses, filter])

  return (
    <div className="animate-fade-in space-y-6">

      {/* ── Hero ──────────────────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center gap-4 min-w-0">
            <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
              🧾
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Troškovi</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">Pratite šta trošite na vaše pčelinjake.</p>
            </div>
          </div>

          <div className="flex items-center gap-2 shrink-0">
            <button
              onClick={() => navigate('/expenses/scan')}
              className="flex items-center gap-1.5 px-3 py-2 rounded-xl border border-honey-200 dark:border-slate-700 bg-white/70 dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-white dark:hover:bg-slate-700 transition-colors"
            >
              <Camera className="w-4 h-4" />
              <span className="hidden sm:inline">Skeniraj</span> račun
            </button>
            <button
              onClick={() => navigate('/expenses/new')}
              className="btn-primary text-sm"
            >
              <Plus className="w-4 h-4" />
              Dodaj trošak
            </button>
          </div>
        </div>
      </div>

      {/* Loading */}
      {isLoading && <VitalsSkeleton />}

      {/* Empty */}
      {!isLoading && expenses.length === 0 && (
        <div className="text-center py-20 bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none">
          <ReceiptText className="w-12 h-12 text-honey-300 dark:text-honey-500/40 mx-auto mb-3" />
          <p className="text-gray-500 dark:text-slate-300 font-medium">Nema zabilježenih troškova.</p>
          <p className="text-sm text-gray-400 dark:text-slate-500 mt-1">
            Dodajte vaš prvi trošak ručno ili skeniranjem računa.
          </p>
          <div className="flex items-center justify-center gap-3 mt-5">
            <button
              onClick={() => navigate('/expenses/scan')}
              className="flex items-center gap-1.5 px-4 py-2 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
            >
              <Camera className="w-4 h-4" />
              Skeniraj račun
            </button>
            <button
              onClick={() => navigate('/expenses/new')}
              className="flex items-center gap-1.5 px-4 py-2 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
            >
              <Plus className="w-4 h-4" />
              Dodaj ručno
            </button>
          </div>
        </div>
      )}

      {/* Content */}
      {!isLoading && expenses.length > 0 && (
        <>
          {/* Vitals */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
            <VitalCard icon="🧾" label="Zapisi"       value={String(expenses.length)}   sub="troškova"             gradient="from-honey-400 to-honey-600" />
            <VitalCard icon="💰" label="Ukupno"      value={totalSpent.toFixed(2)}     sub={currency}             gradient="from-emerald-400 to-teal-600" />
            <VitalCard icon="📅" label="Ovaj mjesec" value={thisMonthTotal.toFixed(2)} sub={format(new Date(), 'MMMM')} gradient="from-sky-400 to-blue-600" />
            <VitalCard icon="🧮" label="Prosjek"     value={avg.toFixed(2)}            sub="po zapisu"            gradient="from-violet-400 to-indigo-600" />
          </div>

          {/* Filter chips */}
          <div className="flex items-center gap-0.5 bg-gray-100 dark:bg-slate-800 rounded-xl p-1 w-fit">
            {FILTERS.map(f => (
              <button
                key={f.key}
                onClick={() => setFilter(f.key)}
                className={clsx(
                  'px-3 py-1.5 rounded-lg text-sm font-medium transition-all',
                  filter === f.key
                    ? 'bg-white dark:bg-slate-700 text-honey-800 dark:text-honey-300 shadow-sm'
                    : 'text-gray-600 dark:text-slate-300 hover:text-honey-700 dark:hover:text-honey-300',
                )}
              >
                {f.label}
              </button>
            ))}
          </div>

          {/* Expense list */}
          {visible.length === 0 ? (
            <div className="text-center py-12 bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800">
              <p className="text-sm text-gray-500 dark:text-slate-400">Nema troškova u ovoj kategoriji.</p>
            </div>
          ) : (
            <div className="space-y-3">
              {visible.map(expense => (
                <ExpenseCard
                  key={expense.id}
                  expense={expense}
                  isDeleting={confirmTarget?.id === expense.id && isDeleting}
                  onEdit={() => navigate(`/expenses/${expense.id}/edit`)}
                  onDelete={() => setConfirmTarget(expense)}
                />
              ))}
            </div>
          )}
        </>
      )}

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši trošak"
        message={confirmTarget
          ? `Obrisati trošak od ${confirmTarget.totalAmount.toFixed(2)} ${confirmTarget.currency} na dan ${format(new Date(confirmTarget.purchaseDate), 'dd.MM.yyyy')}? Ova radnja se ne može poništiti.`
          : ''}
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}

// ── Expense Card ──────────────────────────────────────────────────────────────

interface ExpenseCardProps {
  expense: Expense
  isDeleting: boolean
  onEdit: () => void
  onDelete: () => void
}

function ExpenseCard({ expense, isDeleting, onEdit, onDelete }: ExpenseCardProps) {
  const isReceiptScan = expense.source === ExpenseSource.ReceiptScan

  return (
    <div className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4 flex items-center gap-4 hover:border-honey-200 dark:hover:border-slate-700 transition-colors">
      {/* Icon */}
      <div className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 ${
        isReceiptScan ? 'bg-blue-50 text-blue-500 dark:bg-blue-500/15 dark:text-blue-300' : 'bg-honey-50 text-honey-600 dark:bg-honey-500/15 dark:text-honey-300'
      }`}>
        {isReceiptScan ? <Camera className="w-5 h-5" /> : <PencilLine className="w-5 h-5" />}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-semibold text-gray-900 dark:text-slate-100">
            {expense.totalAmount.toFixed(2)} {expense.currency}
          </span>
          <span className="text-xs text-gray-400 dark:text-slate-400 bg-gray-100 dark:bg-slate-800 rounded-full px-2 py-0.5">
            {ExpenseSourceLabels[expense.source]}
          </span>
        </div>
        <div className="flex items-center gap-3 mt-0.5 text-sm text-gray-500 dark:text-slate-400">
          <span>{format(new Date(expense.purchaseDate), 'dd.MM.yyyy')}</span>
          <span>·</span>
          <span>{expense.itemCount} {expense.itemCount === 1 ? 'stavka' : 'stavki'}</span>
          {expense.notes && (
            <>
              <span>·</span>
              <span className="truncate">{expense.notes}</span>
            </>
          )}
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-1 shrink-0">
        <button
          onClick={onEdit}
          className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
          aria-label="Uredi trošak"
        >
          <PencilLine className="w-4 h-4" />
        </button>
        <button
          onClick={onDelete}
          disabled={isDeleting}
          className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-50"
          aria-label="Obriši trošak"
        >
          {isDeleting
            ? <Loader2 className="w-4 h-4 animate-spin" />
            : <Trash2 className="w-4 h-4" />
          }
        </button>
      </div>
    </div>
  )
}

// ── Vitals KPI tile ────────────────────────────────────────────────────────────

/* VitalCard now lives in shared/components (with count-up animation). */
