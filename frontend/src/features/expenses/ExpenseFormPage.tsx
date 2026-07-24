import { useEffect } from 'react'
import { useNavigate, useParams, useLocation } from 'react-router-dom'
import { useFieldArray, useForm, useWatch } from 'react-hook-form'
import { AlertCircle, Loader2, Plus, Trash2 } from 'lucide-react'
import { useExpense, useCreateExpense, useUpdateExpense } from '../../core/services/expenseQueries'
import { ExpenseSource } from '../../core/models'
import type { CreateExpenseItemPayload } from '../../core/models'
import { FormHeader } from '../../shared/components'

interface FormValues {
  purchaseDate: string
  totalAmount: string
  currency: string
  notes: string
  items: Array<{
    name: string
    quantity: string
    unit: string
    unitPrice: string
    totalPrice: string
  }>
}

const DEFAULT_CURRENCIES = ['BAM', 'EUR', 'USD', 'HRK']

const itemInputCls =
  'w-full px-3 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all'
// Field labels shown only on mobile (the desktop layout has a shared header row instead).
const itemLabelCls = 'sm:hidden block text-[11px] font-medium text-gray-400 dark:text-slate-500 mb-1'

export default function ExpenseFormPage() {
  const { id } = useParams<{ id: string }>()
  const expenseId = id ? parseInt(id) : undefined
  const isEdit = expenseId !== undefined

  const navigate = useNavigate()
  const location = useLocation()
  const prefilled = location.state as { items?: CreateExpenseItemPayload[]; source?: ExpenseSource } | null

  const { data: existing, isLoading: loadingExisting } = useExpense(expenseId ?? 0)
  const createExpense = useCreateExpense()
  const updateExpense = useUpdateExpense(expenseId ?? 0)

  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    setValue,
    getValues,
  } = useForm<FormValues>({
    defaultValues: {
      purchaseDate: new Date().toISOString().split('T')[0],
      totalAmount: '',
      currency: 'BAM',
      notes: '',
      items: prefilled?.items?.map(i => ({
        name: i.name,
        quantity: String(i.quantity),
        unit: i.unit ?? '',
        unitPrice: String(i.unitPrice),
        totalPrice: String(i.totalPrice),
      })) ?? [emptyItem()],
    },
  })

  const { fields, append, remove } = useFieldArray({ control, name: 'items' })

  // Populate form when editing
  useEffect(() => {
    if (existing && isEdit) {
      reset({
        purchaseDate: existing.purchaseDate.split('T')[0],
        totalAmount: String(existing.totalAmount),
        currency: existing.currency,
        notes: existing.notes ?? '',
        items: existing.items.map(i => ({
          name: i.name,
          quantity: String(i.quantity),
          unit: i.unit ?? '',
          unitPrice: String(i.unitPrice),
          totalPrice: String(i.totalPrice),
        })),
      })
    }
  }, [existing, isEdit, reset])

  // Live grand total, summed from each row's line total. useWatch stays reactive on every
  // keystroke — the previous watch()+useEffect combo missed nested edits, so the total went
  // stale until a new row was added or the page reloaded.
  const watchedItems = useWatch({ control, name: 'items' })
  const grandTotal = (watchedItems ?? []).reduce(
    (sum, it) => sum + (parseFloat(it?.totalPrice) || 0),
    0,
  )

  // When both quantity and unit price are known, fill the row's line total automatically.
  const recomputeRow = (index: number) => {
    const q = parseFloat(getValues(`items.${index}.quantity`)) || 0
    const u = parseFloat(getValues(`items.${index}.unitPrice`)) || 0
    if (q > 0 && u > 0) setValue(`items.${index}.totalPrice`, (q * u).toFixed(2))
  }

  const isSaving = createExpense.isPending || updateExpense.isPending
  const error = createExpense.error || updateExpense.error

  async function onSubmit(values: FormValues) {
    const items: CreateExpenseItemPayload[] = values.items.map((item, i) => ({
      name: item.name.trim(),
      quantity: parseFloat(item.quantity) || 0,
      unit: item.unit.trim() || undefined,
      unitPrice: parseFloat(item.unitPrice) || 0,
      totalPrice: parseFloat(item.totalPrice) || 0,
      sortOrder: i,
    }))

    // Total is always the sum of the line totals — never a stale form field.
    const totalAmount = items.reduce((sum, it) => sum + it.totalPrice, 0)

    if (isEdit && expenseId) {
      await updateExpense.mutateAsync({
        purchaseDate: values.purchaseDate,
        totalAmount,
        currency: values.currency,
        notes: values.notes.trim() || undefined,
        items,
      })
    } else {
      await createExpense.mutateAsync({
        source: prefilled?.source ?? ExpenseSource.Manual,
        purchaseDate: values.purchaseDate,
        totalAmount,
        currency: values.currency,
        notes: values.notes.trim() || undefined,
        items,
      })
    }

    navigate('/expenses')
  }

  if (isEdit && loadingExisting) {
    return (
      <div className="flex justify-center py-20">
        <Loader2 className="w-6 h-6 animate-spin text-honey-500" />
      </div>
    )
  }

  return (
    <div className="max-w-2xl mx-auto">
      <FormHeader
        icon="🧾"
        title={isEdit ? 'Uredi trošak' : 'Novi trošak'}
        onBack={() => navigate('/expenses')}
        backLabel="Nazad na troškove"
      />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-8 py-8">
        {error && (
          <div className="flex items-start gap-2 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm mb-5">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            Greška pri čuvanju troška. Provjerite vaše podatke i pokušajte ponovo.
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
          {/* Date + Currency row */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                Datum kupovine <span className="text-red-500">*</span>
              </label>
              <input
                type="date"
                {...register('purchaseDate', { required: true })}
                className="w-full px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">Valuta</label>
              <select
                {...register('currency')}
                className="w-full px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
              >
                {DEFAULT_CURRENCIES.map(c => (
                  <option key={c} value={c}>{c}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Notes */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">Napomene</label>
            <input
              type="text"
              placeholder="npr. Proljetne zalihe, lokalna prodavnica"
              {...register('notes')}
              className="w-full px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
            />
          </div>

          {/* Items */}
          <div>
            <div className="flex items-center justify-between mb-2">
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                Stavke <span className="text-red-500">*</span>
              </label>
              <button
                type="button"
                onClick={() => append(emptyItem())}
                className="flex items-center gap-1 text-xs text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300 font-medium transition-colors"
              >
                <Plus className="w-3.5 h-3.5" />
                Dodaj stavku
              </button>
            </div>

            {/* Column headers (desktop only) */}
            <div className="hidden sm:grid grid-cols-[2fr_1fr_0.8fr_1fr_1fr_auto] gap-2 px-1 mb-1">
              {['Proizvod', 'Kol.', 'Jed.', 'Jed. cijena', 'Ukupno', ''].map(h => (
                <span key={h} className="text-xs font-medium text-gray-400 dark:text-slate-500">{h}</span>
              ))}
            </div>

            <div className="space-y-3 sm:space-y-2">
              {fields.map((field, index) => {
                const qtyReg   = register(`items.${index}.quantity`, { required: true })
                const priceReg = register(`items.${index}.unitPrice`)
                return (
                  <div
                    key={field.id}
                    className="grid grid-cols-2 sm:grid-cols-[2fr_1fr_0.8fr_1fr_1fr_auto] gap-2 sm:items-center
                               rounded-xl border border-gray-100 dark:border-slate-800 p-3 sm:p-0 sm:border-0 sm:rounded-none"
                  >
                    <div className="col-span-2 sm:col-span-1">
                      <label className={itemLabelCls}>Proizvod</label>
                      <input type="text" placeholder="Šećer" {...register(`items.${index}.name`, { required: true })} className={itemInputCls} />
                    </div>
                    <div>
                      <label className={itemLabelCls}>Količina</label>
                      <input
                        type="number" step="0.01" min="0" inputMode="decimal" placeholder="25"
                        {...qtyReg}
                        onChange={e => { qtyReg.onChange(e); recomputeRow(index) }}
                        className={itemInputCls}
                      />
                    </div>
                    <div>
                      <label className={itemLabelCls}>Jedinica</label>
                      <input type="text" placeholder="kg" {...register(`items.${index}.unit`)} className={itemInputCls} />
                    </div>
                    <div>
                      <label className={itemLabelCls}>Jed. cijena</label>
                      <input
                        type="number" step="0.01" min="0" inputMode="decimal" placeholder="1.60"
                        {...priceReg}
                        onChange={e => { priceReg.onChange(e); recomputeRow(index) }}
                        className={itemInputCls}
                      />
                    </div>
                    <div>
                      <label className={itemLabelCls}>Ukupno</label>
                      <input type="number" step="0.01" min="0" inputMode="decimal" placeholder="40.00" {...register(`items.${index}.totalPrice`)} className={itemInputCls} />
                    </div>
                    <div className="col-span-2 sm:col-span-1 flex justify-end sm:block">
                      <button
                        type="button"
                        onClick={() => remove(index)}
                        disabled={fields.length === 1}
                        className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-30 disabled:cursor-not-allowed"
                        aria-label="Ukloni stavku"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                )
              })}
            </div>
          </div>

          {/* Total — always the live sum of the line totals */}
          <div className="flex items-center justify-between gap-3 pt-3 border-t border-gray-100 dark:border-slate-800">
            <span className="text-sm font-medium text-gray-600 dark:text-slate-300">Ukupan iznos</span>
            <span className="text-lg font-bold text-gray-900 dark:text-slate-50 tabular-nums">
              {grandTotal.toFixed(2)}{' '}
              <span className="text-sm font-medium text-gray-500 dark:text-slate-400">{watch('currency')}</span>
            </span>
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={() => navigate('/expenses')}
              className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
            >
              Otkaži
            </button>
            <button
              type="submit"
              disabled={isSaving}
              className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors"
            >
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isEdit ? 'Spremi promjene' : 'Sačuvaj trošak'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function emptyItem() {
  return { name: '', quantity: '', unit: '', unitPrice: '', totalPrice: '' }
}
