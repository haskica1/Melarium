import { useState, useEffect } from 'react'
import { useNavigate, useParams, useSearchParams, Link } from 'react-router-dom'
import { Info } from 'lucide-react'
import { format, addDays } from 'date-fns'
import { useCreateDiet, useUpdateDiet, useDiet } from '../../core/services/queries'
import { LoadingSpinner, ErrorMessage, FormHeader } from '../../shared/components'
import {
  DietReason, DietReasonLabels,
  FoodType, FoodTypeLabels,
} from '../../core/models'
import type { CreateDietPayload, UpdateDietPayload } from '../../core/models'

const REASON_OPTIONS = Object.entries(DietReasonLabels).map(([k, v]) => ({
  value: Number(k) as DietReason, label: v,
}))

const FOOD_OPTIONS = Object.entries(FoodTypeLabels).map(([k, v]) => ({
  value: Number(k) as FoodType, label: v,
}))

function calcEntryCount(duration: number, frequency: number): number {
  if (!duration || !frequency || frequency <= 0) return 0
  return Math.max(1, Math.floor(duration / frequency))
}

export default function DietFormPage() {
  const { id } = useParams<{ id: string }>()
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()

  const isEdit = !!id
  const dietId = Number(id)
  const beehiveId = Number(searchParams.get('beehiveId'))

  const { data: existing, isLoading: loadingExisting } = useDiet(isEdit ? dietId : 0)
  const createMutation = useCreateDiet(isEdit ? existing?.beehiveId ?? beehiveId : beehiveId)
  const updateMutation = useUpdateDiet(dietId, existing?.beehiveId ?? beehiveId)

  // ── Form state ──────────────────────────────────────────────────────────────

  const [name, setName]               = useState('')
  const [startDate, setStartDate]     = useState(format(new Date(), 'yyyy-MM-dd'))
  const [reason, setReason]           = useState<DietReason>(DietReason.LackOfFood)
  const [customReason, setCustomReason] = useState('')
  const [duration, setDuration]       = useState(10)
  const [frequency, setFrequency]     = useState(2)
  const [foodType, setFoodType]       = useState<FoodType>(FoodType.SugarSyrup)
  const [customFood, setCustomFood]   = useState('')
  const [errors, setErrors]           = useState<Record<string, string>>({})

  // Populate form when editing
  useEffect(() => {
    if (existing) {
      setName(existing.name)
      setStartDate(format(new Date(existing.startDate), 'yyyy-MM-dd'))
      setReason(existing.reason)
      setCustomReason(existing.customReason ?? '')
      setDuration(existing.durationDays)
      setFrequency(existing.frequencyDays)
      setFoodType(existing.foodType)
      setCustomFood(existing.customFoodType ?? '')
    }
  }, [existing])

  // ── Derived preview ─────────────────────────────────────────────────────────

  const entryCount = calcEntryCount(duration, frequency)
  const endDate = startDate
    ? format(addDays(new Date(startDate), duration - 1), 'dd MMM yyyy')
    : '—'

  // ── Validation ──────────────────────────────────────────────────────────────

  function validate(): boolean {
    const e: Record<string, string> = {}
    if (!name.trim()) e.name = 'Naziv je obavezan.'
    if (!startDate) e.startDate = 'Datum početka je obavezan.'
    else if (startDate > format(new Date(), 'yyyy-MM-dd')) e.startDate = 'Datum ne može biti u budućnosti.'
    if (!duration || duration < 1) e.duration = 'Trajanje mora biti najmanje 1 dan.'
    if (!frequency || frequency < 1) e.frequency = 'Frekvencija mora biti najmanje 1 dan.'
    if (frequency > duration) e.frequency = 'Frekvencija ne može biti veća od trajanja.'
    if (reason === DietReason.Custom && !customReason.trim())
      e.customReason = 'Molimo opišite razlog.'
    if (foodType === FoodType.Custom && !customFood.trim())
      e.customFood = 'Molimo opišite vrstu hrane.'
    setErrors(e)
    return Object.keys(e).length === 0
  }

  // ── Submit ──────────────────────────────────────────────────────────────────

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!validate()) return

    if (isEdit) {
      const payload: UpdateDietPayload = {
        name: name.trim(),
        startDate,
        reason,
        customReason: reason === DietReason.Custom ? customReason.trim() : undefined,
        durationDays: duration,
        frequencyDays: frequency,
        foodType,
        customFoodType: foodType === FoodType.Custom ? customFood.trim() : undefined,
      }
      await updateMutation.mutateAsync(payload)
      navigate(`/feedings/${dietId}`)
    } else {
      const payload: CreateDietPayload = {
        name: name.trim(),
        startDate,
        reason,
        customReason: reason === DietReason.Custom ? customReason.trim() : undefined,
        durationDays: duration,
        frequencyDays: frequency,
        foodType,
        customFoodType: foodType === FoodType.Custom ? customFood.trim() : undefined,
        beehiveId,
      }
      const created = await createMutation.mutateAsync(payload)
      navigate(`/feedings/${created.id}`)
    }
  }

  const isSaving = createMutation.isPending || updateMutation.isPending

  if (isEdit && loadingExisting) return <LoadingSpinner message="Učitavanje prehrane…" />
  if (isEdit && !existing && !loadingExisting) return <ErrorMessage message="Prehrana nije pronađena." />

  const backHref = isEdit
    ? `/feedings/${dietId}`
    : `/beehives/${beehiveId}`

  return (
    <div className="animate-fade-in max-w-2xl mx-auto">
      <FormHeader
        icon="🌿"
        title={isEdit ? 'Uredi prehranu' : 'Nova prehrana'}
        subtitle={isEdit ? 'Ažuriraj program prehrane' : 'Kreiraj program prehrane'}
        onBack={() => navigate(backHref)}
      />

      <form onSubmit={handleSubmit} className="space-y-6">

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

          <div className="grid grid-cols-2 gap-4">
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
                <span className="font-semibold">{entryCount} {entryCount === 1 ? 'unos' : 'unosa'} hranjenja</span>
                {' '}bit će generisano, svakih {frequency} {frequency === 1 ? 'dan' : 'dana'} tokom {duration} dana
                {startDate && <span> (završava {endDate})</span>}.
              </div>
            </div>
          )}
        </div>

        {/* Food */}
        <div className="card space-y-4">
          <h3 className="font-semibold text-gray-700 dark:text-slate-200 text-sm uppercase tracking-wide">Vrsta hrane</h3>

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
        </div>

        {/* Actions */}
        <div className="flex gap-3 justify-end pb-8">
          <Link to={backHref} className="btn-secondary">Otkaži</Link>
          <button type="submit" className="btn-primary" disabled={isSaving}>
            {isSaving ? 'Čuvanje…' : isEdit ? 'Spremi promjene' : 'Kreiraj prehranu'}
          </button>
        </div>
      </form>
    </div>
  )
}
