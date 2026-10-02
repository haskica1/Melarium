import { useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'
import clsx from 'clsx'
import { AlertCircle, AlertTriangle, Loader2 } from 'lucide-react'
import { useApiaries, useBeehivesByApiary } from '../../core/services/queries'
import { useHarvest, useCreateHarvest, useUpdateHarvest } from '../../core/services/harvestQueries'
import { useTreatments } from '../../core/services/treatmentQueries'
import { useMyPlan, isFeatureLocked } from '../../core/services/planService'
import { errorMessage, isPlanLimit } from '../../core/services/apiClient'
import { HiveProductType, HiveProductTypeLabels, HoneyType, HoneyTypeLabels } from '../../core/models'
import type { CreateHarvestEntryPayload } from '../../core/models'
import { FormHeader, ErrorMessage } from '../../shared/components'
import { useFormNavigation } from '../../shared/hooks/useFormNavigation'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useToast } from '../../core/context/ToastContext'
import { decimalInputProps, parseDecimal, sanitizeDecimal } from '../../shared/utils/decimalInput'
import {
  HIVE_PRODUCT_TYPES, PRODUCT_ICON, displayToKg, displayToPricePerKg, fmtProductQty,
  priceForInput, priceUnitLabel, qtyForInput, qtyUnit,
} from '../../shared/utils/hiveProductUnits'
import { ProductsReadOnlyNotice } from './ProductsReadOnlyNotice'

/** Where the quantity was recorded — exactly one level per record (SPEC-30). */
type Level = 'hive' | 'apiary' | 'org'

const LEVELS: Array<{ value: Level; label: string }> = [
  { value: 'hive', label: 'Po košnicama' },
  { value: 'apiary', label: 'Ukupno za pčelinjak' },
  { value: 'org', label: 'Cijela organizacija' },
]

const HONEY_TYPES = Object.values(HoneyType).filter(v => typeof v === 'number') as HoneyType[]

const PRICE_HINT: Record<HiveProductType, string> = {
  [HiveProductType.Honey]:      'npr. 12,00',
  [HiveProductType.CombHoney]:  'npr. 25',
  [HiveProductType.Wax]:        'npr. 20',
  [HiveProductType.Propolis]:   'npr. 150',
  [HiveProductType.Pollen]:     'npr. 40',
  [HiveProductType.RoyalJelly]: 'npr. 4,50',
  [HiveProductType.BeeBread]:   'npr. 80',
  [HiveProductType.BeeVenom]:   'npr. 300',
  [HiveProductType.Other]:      'npr. 10',
}

const today = () => new Date().toISOString().split('T')[0]

export default function HarvestFormPage() {
  const { id } = useParams<{ id: string }>()
  const harvestId = id ? parseInt(id) : undefined
  const isEdit = harvestId !== undefined

  const { goBack, goAfterSave } = useFormNavigation('/harvests')
  const { toast } = useToast()
  const { isOrgAdmin } = usePermissions()
  const { data: plan } = useMyPlan()
  // Honey is on every plan; the other products need Standard or above to write (SPEC-30).
  const otherProductsLocked = isFeatureLocked(plan, 'hiveProducts')

  const { data: apiaries = [], isError: apiariesError } = useApiaries()
  const { data: existing, isLoading: loadingExisting } = useHarvest(harvestId ?? 0)
  const createHarvest = useCreateHarvest()
  const updateHarvest = useUpdateHarvest(harvestId ?? 0)

  // Per hive is the default: it is how honey has always been recorded here.
  const [level, setLevel] = useState<Level>('hive')
  const [apiaryId, setApiaryId] = useState<number>(0)
  const [productType, setProductType] = useState<HiveProductType>(HiveProductType.Honey)
  const [honeyType, setHoneyType] = useState<HoneyType>(HoneyType.Acacia)
  const [date, setDate] = useState<string>(today())
  const [price, setPrice] = useState<string>('')
  const [notes, setNotes] = useState<string>('')
  const [bulk, setBulk] = useState<string>('')
  const [qty, setQty] = useState<Record<number, string>>({})
  const [frames, setFrames] = useState<Record<number, string>>({})
  const [formError, setFormError] = useState<string | null>(null)

  const isHoney = productType === HiveProductType.Honey
  const isOrgLevel = level === 'org'
  const { data: hives = [], isLoading: loadingHives, isError: hivesError } = useBeehivesByApiary(isOrgLevel ? 0 : apiaryId)

  // A locked apiary (SPEC-24) would only answer 402 — it is not a choice.
  const selectableApiaries = apiaries.filter(a => !a.isLocked || a.id === apiaryId)

  useEffect(() => {
    if (!existing || !isEdit) return
    const type = existing.productType
    setProductType(type)
    if (existing.honeyType) setHoneyType(existing.honeyType)
    setApiaryId(existing.apiaryId ?? 0)
    setDate(existing.date.split('T')[0])
    setPrice(existing.pricePerKg != null ? priceForInput(existing.pricePerKg, type) : '')
    setNotes(existing.notes ?? '')
    if (existing.apiaryId == null) {
      setLevel('org')
      setBulk(existing.bulkKg != null ? qtyForInput(existing.bulkKg, type) : '')
    } else if (existing.entries.length > 0) {
      setLevel('hive')
      setQty(Object.fromEntries(existing.entries.map(e => [e.beehiveId, qtyForInput(e.quantityKg, type)])))
      setFrames(Object.fromEntries(existing.entries
        .filter(e => e.framesExtracted != null)
        .map(e => [e.beehiveId, String(e.framesExtracted)])))
    } else {
      setLevel('apiary')
      setBulk(existing.bulkKg != null ? qtyForInput(existing.bulkKg, type) : '')
    }
  }, [existing, isEdit])

  /**
   * Changing the product changes the unit (propolis is typed in g, wax in kg), so whatever was typed
   * is converted rather than kept: "350" g of propolis must not become 350 kg of wax.
   */
  function changeType(next: HiveProductType) {
    const convertQty = (v: string) => {
      const n = parseDecimal(v)
      return isNaN(n) ? v : qtyForInput(displayToKg(n, productType), next)
    }
    setBulk(prev => convertQty(prev))
    setQty(prev => Object.fromEntries(Object.entries(prev).map(([k, v]) => [k, convertQty(v)])))
    setPrice(prev => {
      const n = parseDecimal(prev)
      return isNaN(n) ? prev : priceForInput(displayToPricePerKg(n, productType), next)
    })
    setProductType(next)
  }

  function changeLevel(next: Level) {
    setLevel(next)
    setFormError(null)
    if (next === 'org') { setApiaryId(0); setQty({}); setFrames({}) }
  }

  const unit = qtyUnit(productType)
  const bulkValue = parseDecimal(bulk)
  const splitTotal = useMemo(
    () => Object.values(qty).reduce((sum, v) => sum + (parseDecimal(v) || 0), 0),
    [qty],
  )
  const totalDisplay = level === 'hive' ? splitTotal : isNaN(bulkValue) ? 0 : bulkValue

  // SPEC-08 soft integration: a date inside a treatment or its withdrawal period is worth a warning —
  // never a block. One figure for the apiary came from all of it, so every treatment there counts; a
  // record of the whole organization belongs to no one apiary and gets no warning.
  const { data: apiaryTreatments = [] } = useTreatments({ apiaryId }, { enabled: apiaryId > 0 && !isOrgLevel })
  const karencaWarnings = useMemo(() => {
    if (!apiaryId || isOrgLevel || !date) return []
    const selectedNames = level === 'hive'
      ? new Set(hives.filter(h => (parseDecimal(qty[h.id] ?? '') || 0) > 0).map(h => h.name))
      : null
    if (selectedNames && selectedNames.size === 0) return []

    const fmt = (iso: string) => {
      const [y, m, d] = iso.split('T')[0].split('-')
      return `${d}.${m}.${y}.`
    }
    const warnings: string[] = []
    for (const t of apiaryTreatments) {
      if (date < t.startDate.split('T')[0]) continue
      const inWindow = !t.endDate || date <= t.karencaUntil.split('T')[0]
      if (!inWindow) continue
      if (selectedNames && !t.hiveNames.some(n => selectedNames.has(n))) continue
      warnings.push(!t.endDate
        ? `${t.productName} — tretman u toku od ${fmt(t.startDate)}`
        : `${t.productName} — karenca do ${fmt(t.karencaUntil)}`)
    }
    return warnings
  }, [apiaryTreatments, hives, qty, date, apiaryId, level, isOrgLevel])

  const isSaving = createHarvest.isPending || updateHarvest.isPending

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    if (!isOrgLevel && !apiaryId) { setFormError('Odaberite pčelinjak.'); return }

    let entries: CreateHarvestEntryPayload[] = []
    let bulkKg: number | null = null
    if (level === 'hive') {
      entries = hives
        .map(h => ({ hive: h, value: parseDecimal(qty[h.id] ?? '') }))
        .filter(r => !isNaN(r.value) && r.value > 0)
        .map(r => ({
          beehiveId: r.hive.id,
          quantityKg: displayToKg(r.value, productType),
          framesExtracted: isHoney && frames[r.hive.id] ? parseInt(frames[r.hive.id]) : null,
        }))
      if (entries.length === 0) { setFormError('Unesite količinu za barem jednu košnicu.'); return }
    } else {
      if (isNaN(bulkValue) || bulkValue <= 0) { setFormError('Unesite količinu.'); return }
      bulkKg = displayToKg(bulkValue, productType)
    }

    // `|| null` would turn a deliberate 0 into "not specified" — only an unparseable value is null.
    const parsedPrice = parseDecimal(price)
    const pricePerKg = isNaN(parsedPrice) ? null : displayToPricePerKg(parsedPrice, productType)
    const payload = {
      date, productType, honeyType: isHoney ? honeyType : null, pricePerKg, bulkKg,
      notes: notes.trim() || undefined, entries,
    }

    try {
      if (isEdit && harvestId) {
        await updateHarvest.mutateAsync(payload)
        toast.success('Zapis ažuriran.')
      } else {
        await createHarvest.mutateAsync({ ...payload, apiaryId: isOrgLevel ? null : apiaryId })
        toast.success('Zapis sačuvan.')
      }
      goAfterSave('/harvests')
    } catch (err) {
      // A 402 has already opened the upsell modal; an inline copy of it would be a second dialog.
      if (!isPlanLimit(err)) setFormError(errorMessage(err))
    }
  }

  if (isEdit && loadingExisting) {
    return (
      <div className="flex justify-center py-20">
        <Loader2 className="w-6 h-6 animate-spin text-honey-500" />
      </div>
    )
  }

  const title = isEdit ? 'Uredi prinos' : 'Novi prinos'

  // Editing another product below Standard: the record stays readable and deletable, not editable.
  if (isEdit && existing && existing.productType !== HiveProductType.Honey && otherProductsLocked) {
    return (
      <div className="max-w-2xl mx-auto space-y-4">
        <FormHeader icon={PRODUCT_ICON[existing.productType]} title={title} />
        <ProductsReadOnlyNotice />
        <button type="button" onClick={goBack} className="w-full px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors">
          Nazad
        </button>
      </div>
    )
  }

  // The level a record was saved at is fixed for the organization's own records (no apiary to
  // split over) and, the other way round, for apiary records (the apiary is immutable).
  const levelOptions = LEVELS.filter(l =>
    isEdit
      ? (existing?.apiaryId == null ? l.value === 'org' : l.value !== 'org')
      : l.value !== 'org' || isOrgAdmin)

  // text-base below `sm` so iOS doesn't zoom the page on focus — see the .form-input note in index.css.
  const inputClass =
    'w-full min-w-0 px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-base sm:text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all'
  const rowInputClass =
    'w-full min-w-0 pl-3 pr-9 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-base sm:text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all'
  const labelClass = 'block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5'
  const unitWord = unit === 'g' ? 'gramima' : 'kilogramima'

  return (
    <div className="max-w-2xl mx-auto">
      <FormHeader icon={PRODUCT_ICON[productType]} title={title} />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-4 py-6 sm:px-8 sm:py-8">
        {formError && (
          <div className="flex items-start gap-2 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm mb-5">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            {formError}
          </div>
        )}

        {apiariesError && (
          <div className="mb-4">
            <ErrorMessage message="Greška pri učitavanju pčelinjaka. Osvježite stranicu." />
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-6">
          {/* Level */}
          <div>
            <span className={labelClass}>Gdje je prikupljeno</span>
            <div className="flex flex-wrap rounded-xl border border-gray-200 dark:border-slate-700 p-0.5 text-sm font-medium w-fit max-w-full" role="group" aria-label="Nivo unosa">
              {levelOptions.map(l => (
                <button
                  key={l.value}
                  type="button"
                  onClick={() => changeLevel(l.value)}
                  aria-pressed={level === l.value}
                  className={clsx(
                    'px-3 py-1.5 rounded-lg transition-colors',
                    level === l.value
                      ? 'bg-honey-500 text-white'
                      : 'text-gray-600 dark:text-slate-300 hover:bg-gray-50 dark:hover:bg-slate-800',
                  )}
                >
                  {l.label}
                </button>
              ))}
            </div>
            {isOrgLevel && (
              <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                Za cijelo gazdinstvo — npr. vosak istopljen sa svih pčelinjaka ili nastavci s više pčelinjaka vrcani
                zajedno. U izvještaju ulazi u ukupnu bilansu, a ne u bilansu pojedinog pčelinjaka.
              </p>
            )}
          </div>

          {/* Apiary + product */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {!isOrgLevel && (
              <div>
                <label className={labelClass}>
                  Pčelinjak <span className="text-red-500">*</span>
                </label>
                <select
                  value={apiaryId}
                  onChange={e => { setApiaryId(Number(e.target.value)); setQty({}); setFrames({}) }}
                  disabled={isEdit}
                  className={`${inputClass} disabled:opacity-60 disabled:cursor-not-allowed`}
                >
                  <option value={0} disabled>Odaberite pčelinjak…</option>
                  {selectableApiaries.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
                </select>
              </div>
            )}
            <div>
              <label className={labelClass}>Proizvod</label>
              <select value={productType} onChange={e => changeType(Number(e.target.value))} className={inputClass}>
                {HIVE_PRODUCT_TYPES.map(t => {
                  const locked = otherProductsLocked && t !== HiveProductType.Honey
                  return (
                    <option key={t} value={t} disabled={locked}>
                      {HiveProductTypeLabels[t]}{locked ? ' — Standard, Pro, Max' : ''}
                    </option>
                  )
                })}
              </select>
            </div>
            {isHoney && (
              <div>
                <label className={labelClass}>Vrsta meda</label>
                <select value={honeyType} onChange={e => setHoneyType(Number(e.target.value))} className={inputClass}>
                  {HONEY_TYPES.map(t => <option key={t} value={t}>{HoneyTypeLabels[t]}</option>)}
                </select>
              </div>
            )}
          </div>

          {/* Date + price */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className={labelClass}>
                Datum <span className="text-red-500">*</span>
              </label>
              <input type="date" value={date} max={today()} onChange={e => setDate(e.target.value)} className={inputClass} />
            </div>
            <div>
              <label className={labelClass}>Cijena ({priceUnitLabel(productType)})</label>
              <input
                {...decimalInputProps}
                placeholder={PRICE_HINT[productType]}
                value={price}
                onChange={e => setPrice(sanitizeDecimal(e.target.value))}
                className={inputClass}
              />
            </div>
          </div>

          <div>
            <label className={labelClass}>Napomena</label>
            <input
              type="text"
              placeholder={isHoney ? 'npr. Prvo vrcanje sezone' : 'npr. Poklopci s vrcanja, zamjena za satne osnove'}
              value={notes}
              onChange={e => setNotes(e.target.value)}
              className={inputClass}
            />
          </div>

          {karencaWarnings.length > 0 && (
            <div className="flex items-start gap-2 bg-amber-50 dark:bg-amber-500/10 border border-amber-200 dark:border-amber-500/30 text-amber-800 dark:text-amber-300 rounded-xl px-4 py-3 text-sm">
              <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
              <div>
                <p className="font-medium">Datum pada u period tretmana ili karence:</p>
                <ul className="mt-1 space-y-0.5 list-disc list-inside">
                  {karencaWarnings.map(w => <li key={w}>{w}</li>)}
                </ul>
                <p className="text-xs mt-1.5 opacity-80">Upozorenje ne blokira unos — provjerite etiketu preparata.</p>
              </div>
            </div>
          )}

          {/* Quantity */}
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
              Količina ({unit}) <span className="text-red-500">*</span>
            </label>

            {level !== 'hive' ? (
              <label className="relative block">
                <input
                  {...decimalInputProps}
                  placeholder="—"
                  aria-label={`Ukupna količina u ${unitWord}`}
                  value={bulk}
                  onChange={e => setBulk(sanitizeDecimal(e.target.value))}
                  className={`${inputClass} pr-12`}
                />
                <span className="absolute right-4 top-1/2 -translate-y-1/2 text-sm text-gray-400 dark:text-slate-500 pointer-events-none">{unit}</span>
              </label>
            ) : !apiaryId ? (
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
                    <div key={hive.id} className="flex items-center gap-2 sm:gap-3">
                      <span className="flex-1 min-w-0 text-sm text-gray-700 dark:text-slate-200 truncate">{hive.name}</span>
                      <label className="relative block w-28 sm:w-32 shrink-0">
                        <input
                          {...decimalInputProps}
                          placeholder="—"
                          aria-label={`Količina u ${unitWord} — ${hive.name}`}
                          value={qty[hive.id] ?? ''}
                          onChange={e => setQty(prev => ({ ...prev, [hive.id]: sanitizeDecimal(e.target.value) }))}
                          className={rowInputClass}
                        />
                        <span className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-gray-400 dark:text-slate-500 pointer-events-none">{unit}</span>
                      </label>
                      {isHoney && (
                        <label className="relative block w-20 sm:w-24 shrink-0">
                          <input
                            type="number" step="1" min="0" inputMode="numeric" placeholder="—"
                            aria-label={`Broj izvrcanih okvira — ${hive.name}`}
                            value={frames[hive.id] ?? ''}
                            onChange={e => setFrames(prev => ({ ...prev, [hive.id]: e.target.value }))}
                            className={rowInputClass}
                          />
                          <span className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-gray-400 dark:text-slate-500 pointer-events-none">kom</span>
                        </label>
                      )}
                    </div>
                  ))}
                </div>
                <p className="text-xs text-gray-400 dark:text-slate-500 mt-2">
                  Ostavite prazno za košnice s kojih ništa nije uzeto.{isHoney ? ' Okviri su opcioni.' : ''}
                </p>
              </>
            )}
          </div>

          {/* Total */}
          <div className="flex items-center justify-end gap-3 pt-2 border-t border-gray-100 dark:border-slate-800">
            <span className="text-sm text-gray-500 dark:text-slate-400">Ukupno</span>
            <span className="text-lg font-semibold text-honey-700 dark:text-honey-300">
              {fmtProductQty(displayToKg(totalDisplay, productType), productType)}
            </span>
          </div>

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={goBack} className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors">
              Otkaži
            </button>
            <button type="submit" disabled={isSaving || totalDisplay <= 0} className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors">
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isEdit ? 'Spremi' : 'Sačuvaj zapis'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
