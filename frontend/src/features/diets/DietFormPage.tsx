import { useState, useEffect, useRef } from 'react'
import { useParams, useSearchParams } from 'react-router-dom'
import { Info, Loader2 } from 'lucide-react'
import { format, addDays } from 'date-fns'
import { useApiaries, useBeehivesByApiary, useBeehive } from '../../core/services/queries'
import { useCreateDiet, useUpdateDiet, useDiet } from '../../core/services/dietQueries'
import { LoadingSpinner, ErrorMessage, FormHeader } from '../../shared/components'
import { useFormNavigation } from '../../shared/hooks/useFormNavigation'
import { decimalInputProps, sanitizeDecimal, parseDecimal } from '../../shared/utils/decimalInput'
import {
  DietReason, DietReasonLabels,
  FoodType, FoodTypeLabels,
  FeedingAmountUnit, FeedingAmountUnitLabels,
} from '../../core/models'
import type { CreateDietPayload, UpdateDietPayload } from '../../core/models'

const REASON_OPTIONS = Object.entries(DietReasonLabels).map(([k, v]) => ({
  value: Number(k) as DietReason, label: v,
}))

const FOOD_OPTIONS = Object.entries(FoodTypeLabels).map(([k, v]) => ({
  value: Number(k) as FoodType, label: v,
}))

const UNIT_OPTIONS = Object.entries(FeedingAmountUnitLabels).map(([k, v]) => ({
  value: Number(k) as FeedingAmountUnit, label: v,
}))

function calcEntryCount(duration: number, frequency: number): number {
  if (!duration || !frequency || frequency <= 0) return 0
  return Math.max(1, Math.floor(duration / frequency))
}

export default function DietFormPage() {
  const { id } = useParams<{ id: string }>()
  const [searchParams] = useSearchParams()

  const isEdit = !!id
  const dietId = Number(id)
  const deepLinkHiveId = Number(searchParams.get('beehiveId')) || 0
  const deepLinkApiaryId = Number(searchParams.get('apiaryId')) || 0

  // Where Otkaži/back belongs when there is no history to pop (deep link into the form).
  const backHref = isEdit ? `/feedings/${dietId}` : deepLinkHiveId ? `/beehives/${deepLinkHiveId}` : '/feedings'
  const { goBack, goAfterSave } = useFormNavigation(backHref)

  const { data: apiaries = [], isError: apiariesError } = useApiaries()
  const { data: existing, isLoading: loadingExisting } = useDiet(isEdit ? dietId : 0)
  // "Feed this one hive" entry point: the hive knows its apiary, the user shouldn't have to.
  const { data: deepLinkHive } = useBeehive(!isEdit ? deepLinkHiveId : 0)

  const createMutation = useCreateDiet()
  const updateMutation = useUpdateDiet(dietId)

  // ── Form state ──────────────────────────────────────────────────────────────

  const [apiaryId, setApiaryId]         = useState(deepLinkApiaryId)
  const [checked, setChecked]           = useState<Record<number, boolean>>({})
  const [name, setName]                 = useState('')
  const [startDate, setStartDate]       = useState(format(new Date(), 'yyyy-MM-dd'))
  const [reason, setReason]             = useState<DietReason>(DietReason.LackOfFood)
  const [customReason, setCustomReason] = useState('')
  const [duration, setDuration]         = useState(10)
  const [frequency, setFrequency]       = useState(2)
  const [foodType, setFoodType]         = useState<FoodType>(FoodType.SugarSyrup)
  const [customFood, setCustomFood]     = useState('')
  const [amount, setAmount]             = useState('')
  const [amountUnit, setAmountUnit]     = useState<FeedingAmountUnit | ''>('')
  const [amountNote, setAmountNote]     = useState('')
  const [errors, setErrors]             = useState<Record<string, string>>({})

  const { data: hives = [], isLoading: loadingHives, isError: hivesError } = useBeehivesByApiary(apiaryId)

  // Guards the "pre-check all hives" init so it runs once per apiary pick, not on every refetch.
  const initializedApiary = useRef<number>(0)

  // Populate form when editing. The hive set is deliberately absent — it is managed on the detail
  // page, where removals keep their history instead of being overwritten by a save.
  useEffect(() => {
    if (!existing) return
    setApiaryId(existing.apiaryId)
    setName(existing.name)
    setStartDate(format(new Date(existing.startDate), 'yyyy-MM-dd'))
    setReason(existing.reason)
    setCustomReason(existing.customReason ?? '')
    setDuration(existing.durationDays)
    setFrequency(existing.frequencyDays)
    setFoodType(existing.foodType)
    setCustomFood(existing.customFoodType ?? '')
    setAmount(existing.amountPerHive != null ? String(existing.amountPerHive).replace('.', ',') : '')
    setAmountUnit(existing.amountUnit ?? '')
    setAmountNote(existing.amountNote ?? '')
    initializedApiary.current = existing.apiaryId
  }, [existing])

  // Deep link from a hive page: select its apiary so the hive list can load.
  useEffect(() => {
    if (isEdit || !deepLinkHive) return
    setApiaryId(prev => prev || deepLinkHive.apiaryId)
  }, [deepLinkHive, isEdit])

  // A programme normally covers the whole apiary, so pre-check everything — except when the user
  // came from one hive, where pre-checking the other nineteen would be the opposite of helpful.
  useEffect(() => {
    if (isEdit || !apiaryId || hives.length === 0) return
    if (initializedApiary.current === apiaryId) return
    initializedApiary.current = apiaryId
    setChecked(
      deepLinkHiveId && hives.some(h => h.id === deepLinkHiveId)
        ? { [deepLinkHiveId]: true }
        : Object.fromEntries(hives.map(h => [h.id, true])),
    )
  }, [apiaryId, hives, isEdit, deepLinkHiveId])

  // ── Derived preview ─────────────────────────────────────────────────────────

  const entryCount = calcEntryCount(duration, frequency)
  const endDate = startDate
    ? format(addDays(new Date(startDate), duration - 1), 'dd MMM yyyy')
    : '—'
  const selectedCount = hives.filter(h => checked[h.id]).length
  const allChecked = hives.length > 0 && selectedCount === hives.length

  // ── Validation ──────────────────────────────────────────────────────────────

  function validate(): boolean {
    const e: Record<string, string> = {}
    if (!isEdit && !apiaryId) e.apiary = 'Odaberite pčelinjak.'
    if (!isEdit && selectedCount === 0) e.hives = 'Odaberite barem jednu košnicu.'
    if (!name.trim()) e.name = 'Naziv je obavezan.'
    if (!startDate) e.startDate = 'Datum početka je obavezan.'
    else if (startDate > format(new Date(), 'yyyy-MM-dd')) e.startDate = 'Datum ne može biti u budućnosti.'
    if (!duration || duration < 1) e.duration = 'Trajanje mora biti najmanje 1 dan.'
    if (duration > 365) e.duration = 'Trajanje ne može biti duže od 365 dana.'
    if (!frequency || frequency < 1) e.frequency = 'Frekvencija mora biti najmanje 1 dan.'
    if (frequency > duration) e.frequency = 'Frekvencija ne može biti veća od trajanja.'
    if (reason === DietReason.Custom && !customReason.trim())
      e.customReason = 'Molimo opišite razlog.'
    if (foodType === FoodType.Custom && !customFood.trim())
      e.customFood = 'Molimo opišite vrstu hrane.'

    if (amount.trim()) {
      const parsed = parseDecimal(amount)
      if (Number.isNaN(parsed) || parsed <= 0) e.amount = 'Količina mora biti veća od 0.'
      else if (parsed > 100) e.amount = 'Količina po košnici ne može biti veća od 100.'
      else if (amountUnit === '') e.amountUnit = 'Odaberite jedinicu.'
    }
    setErrors(e)
    return Object.keys(e).length === 0
  }

  // ── Submit ──────────────────────────────────────────────────────────────────

  function amountFields() {
    const parsed = amount.trim() ? parseDecimal(amount) : NaN
    const hasAmount = !Number.isNaN(parsed)
    return {
      amountPerHive: hasAmount ? parsed : null,
      // Unit without a number carries no meaning, so it goes out together with it.
      amountUnit: hasAmount && amountUnit !== '' ? amountUnit : null,
      // The note stands on its own: "pola pogače" is a valid answer with no number at all.
      amountNote: amountNote.trim() || null,
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!validate()) return

    const common = {
      name: name.trim(),
      startDate,
      reason,
      customReason: reason === DietReason.Custom ? customReason.trim() : undefined,
      durationDays: duration,
      frequencyDays: frequency,
      foodType,
      customFoodType: foodType === FoodType.Custom ? customFood.trim() : undefined,
      ...amountFields(),
    }

    if (isEdit) {
      await updateMutation.mutateAsync(common as UpdateDietPayload)
      goAfterSave(`/feedings/${dietId}`)
    } else {
      const payload: CreateDietPayload = {
        apiaryId,
        beehiveIds: hives.filter(h => checked[h.id]).map(h => h.id),
        ...common,
      }
      const created = await createMutation.mutateAsync(payload)
      goAfterSave(`/feedings/${created.id}`)
    }
  }

  const isSaving = createMutation.isPending || updateMutation.isPending

  if (isEdit && loadingExisting) return <LoadingSpinner message="Učitavanje prehrane…" />
  if (isEdit && !existing && !loadingExisting) return <ErrorMessage message="Prehrana nije pronađena." />

  return (
    <div className="animate-fade-in max-w-2xl mx-auto">
      <FormHeader
        icon="🌿"
        title={isEdit ? 'Uredi prehranu' : 'Nova prehrana'}
        subtitle={isEdit ? 'Ažuriraj program prehrane' : 'Kreiraj program prehrane za pčelinjak'}
      />

      <form onSubmit={handleSubmit} className="space-y-6">

        {/* Apiary + hives */}
        <div className="card space-y-4">
          <h3 className="font-semibold text-gray-700 dark:text-slate-200 text-sm uppercase tracking-wide">Pčelinjak i košnice</h3>

          {apiariesError && <ErrorMessage message="Greška pri učitavanju pčelinjaka. Osvježite stranicu." />}

          <div>
            <label className="form-label">Pčelinjak *</label>
            <select
              className={`form-input disabled:opacity-60 disabled:cursor-not-allowed ${errors.apiary ? 'border-red-400' : ''}`}
              value={apiaryId}
              disabled={isEdit}
              onChange={e => { setApiaryId(Number(e.target.value)); setChecked({}) }}
            >
              <option value={0} disabled>Odaberite pčelinjak…</option>
              {apiaries.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
            </select>
            {errors.apiary && <p className="form-error">{errors.apiary}</p>}
            {isEdit && (
              <p className="text-xs text-gray-400 dark:text-slate-500 mt-1">
                Pčelinjak se ne mijenja nakon kreiranja.
              </p>
            )}
          </div>

          {isEdit ? (
            <p className="text-sm text-gray-500 dark:text-slate-400">
              Košnice se dodaju i izbacuju na stranici programa — tamo ostaje zapisano kada je koja
              košnica bila na prehrani.
            </p>
          ) : (
            <div>
              <div className="flex items-center justify-between mb-2">
                <label className="text-sm font-medium text-gray-700 dark:text-slate-300">Košnice na prehrani *</label>
                {hives.length > 0 && (
                  <button
                    type="button"
                    onClick={() => setChecked(allChecked ? {} : Object.fromEntries(hives.map(h => [h.id, true])))}
                    className="text-xs font-medium text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300 transition-colors"
                  >
                    {allChecked ? 'Poništi sve' : 'Označi sve'}
                  </button>
                )}
              </div>

              {!apiaryId ? (
                <p className="text-sm text-gray-400 dark:text-slate-500 py-4">Prvo odaberite pčelinjak.</p>
              ) : loadingHives ? (
                <div className="flex justify-center py-6"><Loader2 className="w-5 h-5 animate-spin text-honey-500" /></div>
              ) : hivesError ? (
                <ErrorMessage message="Greška pri učitavanju košnica. Osvježite stranicu." />
              ) : hives.length === 0 ? (
                <p className="text-sm text-gray-400 dark:text-slate-500 py-4">Ovaj pčelinjak nema košnica.</p>
              ) : (
                <>
                  <div className="space-y-2">
                    {hives.map(hive => (
                      <label key={hive.id} className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={!!checked[hive.id]}
                          onChange={e => setChecked(prev => ({ ...prev, [hive.id]: e.target.checked }))}
                          className="w-4 h-4 rounded border-gray-300 dark:border-slate-600 text-honey-500 focus:ring-honey-400 accent-honey-500"
                        />
                        <span className="text-sm text-gray-700 dark:text-slate-200 truncate">{hive.name}</span>
                      </label>
                    ))}
                  </div>
                  <p className="text-xs text-gray-400 dark:text-slate-500 mt-2">
                    Odabrano: {selectedCount} od {hives.length}. Jedan program pokriva sve označene košnice.
                  </p>
                  {errors.hives && <p className="form-error">{errors.hives}</p>}
                </>
              )}
            </div>
          )}
        </div>

        {/* Basic info */}
        <div className="card space-y-4">
          <h3 className="font-semibold text-gray-700 dark:text-slate-200 text-sm uppercase tracking-wide">Osnovne informacije</h3>

          <div>
            <label className="form-label">Naziv prehrane *</label>
            <input
              className={`form-input ${errors.name ? 'border-red-400' : ''}`}
              placeholder="npr. Proljetna stimulativna prehrana"
              value={name}
              onChange={e => setName(e.target.value)}
            />
            {errors.name && <p className="form-error">{errors.name}</p>}
          </div>

          <div>
            <label className="form-label">Datum početka *</label>
            <input
              type="date"
              className={`form-input ${errors.startDate ? 'border-red-400' : ''}`}
              max={format(new Date(), 'yyyy-MM-dd')}
              value={startDate}
              onChange={e => setStartDate(e.target.value)}
            />
            {errors.startDate && <p className="form-error">{errors.startDate}</p>}
          </div>

          <div>
            <label className="form-label">Razlog *</label>
            <select
              className="form-input"
              value={reason}
              onChange={e => setReason(Number(e.target.value) as DietReason)}
            >
              {REASON_OPTIONS.map(o => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>

          {reason === DietReason.Custom && (
            <div>
              <label className="form-label">Vlastiti razlog *</label>
              <input
                className={`form-input ${errors.customReason ? 'border-red-400' : ''}`}
                placeholder="Opišite razlog…"
                value={customReason}
                onChange={e => setCustomReason(e.target.value)}
              />
              {errors.customReason && <p className="form-error">{errors.customReason}</p>}
            </div>
          )}
        </div>

        {/* Schedule */}
        <div className="card space-y-4">
          <h3 className="font-semibold text-gray-700 dark:text-slate-200 text-sm uppercase tracking-wide">Raspored</h3>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="form-label">Trajanje (dani) *</label>
              <input
                type="number"
                min={1}
                className={`form-input ${errors.duration ? 'border-red-400' : ''}`}
                value={duration}
                onChange={e => setDuration(Number(e.target.value))}
              />
              {errors.duration && <p className="form-error">{errors.duration}</p>}
            </div>
            <div>
              <label className="form-label">Hranjenje svakih (dana) *</label>
              <input
                type="number"
                min={1}
                className={`form-input ${errors.frequency ? 'border-red-400' : ''}`}
                value={frequency}
                onChange={e => setFrequency(Number(e.target.value))}
              />
              {errors.frequency && <p className="form-error">{errors.frequency}</p>}
            </div>
          </div>

          {/* Preview panel */}
          {entryCount > 0 && (
            <div className="flex items-start gap-3 bg-honey-50 dark:bg-honey-500/10 border border-honey-200 dark:border-honey-500/30 rounded-xl p-4 text-sm">
              <Info className="w-4 h-4 text-honey-600 dark:text-honey-400 shrink-0 mt-0.5" />
              <div className="text-honey-800 dark:text-honey-200">
                <span className="font-semibold">{entryCount} {entryCount === 1 ? 'runda' : entryCount < 5 ? 'runde' : 'rundi'} hranjenja</span>
                {' '}bit će generisano, svakih {frequency} {frequency === 1 ? 'dan' : 'dana'} tokom {duration} dana
                {startDate && <span> (završava {endDate})</span>}.
                {' '}Jedna runda vrijedi za sve košnice na programu.
              </div>
            </div>
          )}
        </div>

        {/* Food */}
        <div className="card space-y-4">
          <h3 className="font-semibold text-gray-700 dark:text-slate-200 text-sm uppercase tracking-wide">Hrana</h3>

          <div>
            <label className="form-label">Vrsta hrane *</label>
            <select
              className="form-input"
              value={foodType}
              onChange={e => setFoodType(Number(e.target.value) as FoodType)}
            >
              {FOOD_OPTIONS.map(o => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>

          {foodType === FoodType.Custom && (
            <div>
              <label className="form-label">Vlastita hrana *</label>
              <input
                className={`form-input ${errors.customFood ? 'border-red-400' : ''}`}
                placeholder="Opišite hranu…"
                value={customFood}
                onChange={e => setCustomFood(e.target.value)}
              />
              {errors.customFood && <p className="form-error">{errors.customFood}</p>}
            </div>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-[1fr_auto] gap-4">
            <div>
              <label className="form-label">Količina po košnici</label>
              {/* type="text" + inputMode, never type="number": on an iPhone with a Bosnian keyboard
                  the comma key silently empties a number input. See decimalInput.ts. */}
              <input
                {...decimalInputProps}
                className={`form-input ${errors.amount ? 'border-red-400' : ''}`}
                placeholder="npr. 1,5"
                value={amount}
                onChange={e => {
                  const v = sanitizeDecimal(e.target.value)
                  setAmount(v)
                  if (!v.trim()) setAmountUnit('')
                }}
              />
              {errors.amount && <p className="form-error">{errors.amount}</p>}
            </div>
            <div>
              <label className="form-label">Jedinica</label>
              <select
                className={`form-input sm:w-28 disabled:opacity-40 ${errors.amountUnit ? 'border-red-400' : ''}`}
                value={amountUnit}
                disabled={!amount.trim()}
                onChange={e => setAmountUnit(e.target.value === '' ? '' : Number(e.target.value) as FeedingAmountUnit)}
              >
                <option value="">—</option>
                {UNIT_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
              {errors.amountUnit && <p className="form-error">{errors.amountUnit}</p>}
            </div>
          </div>

          <div>
            <label className="form-label">Napomena o količini</label>
            <input
              className="form-input"
              placeholder="npr. 1:1, pola pogače"
              maxLength={100}
              value={amountNote}
              onChange={e => setAmountNote(e.target.value)}
            />
            <p className="text-xs text-gray-400 dark:text-slate-500 mt-1">
              Ono što broj ne može nositi — 1 L sirupa 1:1 i 1 L sirupa 2:1 nisu isto.
            </p>
          </div>
        </div>

        {/* Actions */}
        <div className="flex gap-3 pb-8">
          {/* A button, not a Link — a Link would push the parent on top of the abandoned form. */}
          <button type="button" onClick={goBack} className="btn-secondary flex-1">Otkaži</button>
          <button type="submit" className="btn-primary flex-1" disabled={isSaving}>
            {isSaving ? 'Čuvanje…' : isEdit ? 'Spremi' : 'Kreiraj prehranu'}
          </button>
        </div>
      </form>
    </div>
  )
}
