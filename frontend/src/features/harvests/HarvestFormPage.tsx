import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { AlertCircle, AlertTriangle, Loader2 } from 'lucide-react'
import { useApiaries, useBeehivesByApiary } from '../../core/services/queries'
import { useHarvest, useCreateHarvest, useUpdateHarvest } from '../../core/services/harvestQueries'
import { useTreatments } from '../../core/services/treatmentQueries'
import { HoneyType, HoneyTypeLabels } from '../../core/models'
import type { CreateHarvestEntryPayload } from '../../core/models'
import { FormHeader } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'

const HONEY_TYPES = Object.values(HoneyType).filter(v => typeof v === 'number') as HoneyType[]
const today = () => new Date().toISOString().split('T')[0]

export default function HarvestFormPage() {
  const { id } = useParams<{ id: string }>()
  const harvestId = id ? parseInt(id) : undefined
  const isEdit = harvestId !== undefined

  const navigate = useNavigate()
  const { toast } = useToast()

  const { data: apiaries = [] } = useApiaries()
  const { data: existing, isLoading: loadingExisting } = useHarvest(harvestId ?? 0)
  const createHarvest = useCreateHarvest()
  const updateHarvest = useUpdateHarvest(harvestId ?? 0)

  const [apiaryId, setApiaryId] = useState<number>(0)
  const [honeyType, setHoneyType] = useState<HoneyType>(HoneyType.Acacia)
  const [date, setDate] = useState<string>(today())
  const [pricePerKg, setPricePerKg] = useState<string>('')
  const [notes, setNotes] = useState<string>('')
  const [qty, setQty] = useState<Record<number, string>>({})
  const [frames, setFrames] = useState<Record<number, string>>({})
  const [formError, setFormError] = useState<string | null>(null)

  const { data: hives = [], isLoading: loadingHives } = useBeehivesByApiary(apiaryId)

  // Populate when editing
  useEffect(() => {
    if (existing && isEdit) {
      setApiaryId(existing.apiaryId)
      setHoneyType(existing.honeyType)
      setDate(existing.date.split('T')[0])
      setPricePerKg(existing.pricePerKg != null ? String(existing.pricePerKg) : '')
      setNotes(existing.notes ?? '')
      const q: Record<number, string> = {}
      const f: Record<number, string> = {}
      for (const e of existing.entries) {
        q[e.beehiveId] = String(e.quantityKg)
        if (e.framesExtracted != null) f[e.beehiveId] = String(e.framesExtracted)
      }
      setQty(q)
      setFrames(f)
    }
  }, [existing, isEdit])

  const isSaving = createHarvest.isPending || updateHarvest.isPending

  const totalKg = useMemo(
    () => Object.values(qty).reduce((sum, v) => sum + (parseFloat(v) || 0), 0),
    [qty],
  )

  // SPEC-08 soft integration: warn (don't block) when the harvest date falls inside a
  // treatment/karenca window for any hive with a quantity entered.
  const { data: apiaryTreatments = [] } = useTreatments({ apiaryId }, { enabled: apiaryId > 0 })
  const karencaWarnings = useMemo(() => {
    if (!apiaryId || !date) return []
    const selectedNames = new Set(
      hives.filter(h => (parseFloat(qty[h.id] ?? '') || 0) > 0).map(h => h.name),
    )
    if (selectedNames.size === 0) return []

    const fmt = (iso: string) => {
      const [y, m, d] = iso.split('T')[0].split('-')
      return `${d}.${m}.${y}.`
    }
    const warnings: string[] = []
    for (const t of apiaryTreatments) {
      if (date < t.startDate.split('T')[0]) continue
      const inWindow = !t.endDate || date <= t.karencaUntil.split('T')[0]
      if (!inWindow) continue
      if (!t.hiveNames.some(n => selectedNames.has(n))) continue
      warnings.push(!t.endDate
        ? `${t.productName} — tretman u toku od ${fmt(t.startDate)}`
        : `${t.productName} — karenca do ${fmt(t.karencaUntil)}`)
    }
    return warnings
  }, [apiaryTreatments, hives, qty, date, apiaryId])

  function buildEntries(): CreateHarvestEntryPayload[] {
    return hives
      .map(h => ({ hive: h, kg: parseFloat(qty[h.id] ?? '') }))
      .filter(r => !isNaN(r.kg) && r.kg > 0)
      .map(r => ({
        beehiveId: r.hive.id,
        quantityKg: r.kg,
        framesExtracted: frames[r.hive.id] ? parseInt(frames[r.hive.id]) : null,
      }))
  }

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    if (!apiaryId) { setFormError('Odaberite pčelinjak.'); return }
    const entries = buildEntries()
    if (entries.length === 0) { setFormError('Unesite količinu meda za barem jednu košnicu.'); return }

    const price = pricePerKg.trim() ? parseFloat(pricePerKg) : null
    try {
      if (isEdit && harvestId) {
        await updateHarvest.mutateAsync({ date, honeyType, pricePerKg: price, notes: notes.trim() || undefined, entries })
        toast.success('Vrcanje ažurirano.')
      } else {
        await createHarvest.mutateAsync({ apiaryId, date, honeyType, pricePerKg: price, notes: notes.trim() || undefined, entries })
        toast.success('Vrcanje zabilježeno.')
      }
      navigate('/harvests')
    } catch (err: any) {
      const detail = err?.response?.data?.errors?.entries?.[0]
        ?? err?.response?.data?.detail
        ?? 'Greška pri čuvanju vrcanja. Provjerite podatke i pokušajte ponovo.'
      setFormError(detail)
    }
  }

  if (isEdit && loadingExisting) {
    return (
      <div className="flex justify-center py-20">
        <Loader2 className="w-6 h-6 animate-spin text-honey-500" />
      </div>
    )
  }

  const inputClass =
    'w-full px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all'

  return (
    <div className="max-w-2xl mx-auto">
      <FormHeader
        icon="🍯"
        title={isEdit ? 'Uredi vrcanje' : 'Novo vrcanje'}
        onBack={() => navigate('/harvests')}
        backLabel="Nazad na vrcanja"
      />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-8 py-8">
        {formError && (
          <div className="flex items-start gap-2 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm mb-5">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            {formError}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-6">
          {/* Apiary + honey type */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                Pčelinjak <span className="text-red-500">*</span>
              </label>
              <select
                value={apiaryId}
                onChange={e => { setApiaryId(Number(e.target.value)); setQty({}); setFrames({}) }}
                disabled={isEdit}
                className={`${inputClass} disabled:opacity-60 disabled:cursor-not-allowed`}
              >
                <option value={0} disabled>Odaberite pčelinjak…</option>
                {apiaries.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">Vrsta meda</label>
              <select value={honeyType} onChange={e => setHoneyType(Number(e.target.value))} className={inputClass}>
                {HONEY_TYPES.map(t => <option key={t} value={t}>{HoneyTypeLabels[t]}</option>)}
              </select>
            </div>
          </div>

          {/* Date + price */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                Datum <span className="text-red-500">*</span>
              </label>
              <input type="date" value={date} max={today()} onChange={e => setDate(e.target.value)} className={inputClass} />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">Cijena po kg (KM)</label>
              <input type="number" step="0.01" min="0" placeholder="npr. 12.00" value={pricePerKg} onChange={e => setPricePerKg(e.target.value)} className={inputClass} />
            </div>
          </div>

          {/* Notes */}
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">Napomena</label>
            <input type="text" placeholder="npr. Prvo vrcanje sezone" value={notes} onChange={e => setNotes(e.target.value)} className={inputClass} />
          </div>

          {/* Karenca warning (non-blocking, SPEC-08) */}
          {karencaWarnings.length > 0 && (
            <div className="flex items-start gap-2 bg-amber-50 dark:bg-amber-500/10 border border-amber-200 dark:border-amber-500/30 text-amber-800 dark:text-amber-300 rounded-xl px-4 py-3 text-sm">
              <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
              <div>
                <p className="font-medium">Datum vrcanja pada u period tretmana ili karence:</p>
                <ul className="mt-1 space-y-0.5 list-disc list-inside">
                  {karencaWarnings.map(w => <li key={w}>{w}</li>)}
                </ul>
                <p className="text-xs mt-1.5 opacity-80">Upozorenje ne blokira unos — provjerite etiketu preparata.</p>
              </div>
            </div>
          )}

          {/* Per-hive quantities */}
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
              Prinos po košnici (kg) <span className="text-red-500">*</span>
            </label>

            {!apiaryId ? (
              <p className="text-sm text-gray-400 dark:text-slate-500 py-4">Prvo odaberite pčelinjak.</p>
            ) : loadingHives ? (
              <div className="flex justify-center py-6"><Loader2 className="w-5 h-5 animate-spin text-honey-500" /></div>
            ) : hives.length === 0 ? (
              <p className="text-sm text-gray-400 dark:text-slate-500 py-4">Ovaj pčelinjak nema košnica.</p>
            ) : (
              <>
                <div className="grid grid-cols-[2fr_1fr_1fr] gap-2 px-1 mb-1">
                  {['Košnica', 'kg', 'Okviri'].map(h => (
                    <span key={h} className="text-xs font-medium text-gray-400 dark:text-slate-500">{h}</span>
                  ))}
                </div>
                <div className="space-y-2">
                  {hives.map(hive => (
                    <div key={hive.id} className="grid grid-cols-[2fr_1fr_1fr] gap-2 items-center">
                      <span className="text-sm text-gray-700 dark:text-slate-200 truncate">{hive.name}</span>
                      <input
                        type="number" step="0.01" min="0" placeholder="—"
                        value={qty[hive.id] ?? ''}
                        onChange={e => setQty(prev => ({ ...prev, [hive.id]: e.target.value }))}
                        className="px-3 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all"
                      />
                      <input
                        type="number" step="1" min="0" placeholder="—"
                        value={frames[hive.id] ?? ''}
                        onChange={e => setFrames(prev => ({ ...prev, [hive.id]: e.target.value }))}
                        className="px-3 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all"
                      />
                    </div>
                  ))}
                </div>
                <p className="text-xs text-gray-400 dark:text-slate-500 mt-2">Ostavite prazno za košnice koje nisu vrcane.</p>
                {totalKg <= 0 && (
                  <p className="text-xs text-red-500 dark:text-red-400 mt-1">Unesite količinu meda za barem jednu košnicu da biste sačuvali.</p>
                )}
              </>
            )}
          </div>

          {/* Total */}
          <div className="flex items-center justify-end gap-3 pt-2 border-t border-gray-100 dark:border-slate-800">
            <span className="text-sm text-gray-500 dark:text-slate-400">Ukupno</span>
            <span className="text-lg font-semibold text-honey-700 dark:text-honey-300">{totalKg.toFixed(1).replace(/\.0$/, '')} kg</span>
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={() => navigate('/harvests')} className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors">
              Otkaži
            </button>
            <button type="submit" disabled={isSaving || totalKg <= 0} className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors">
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isEdit ? 'Spremi promjene' : 'Sačuvaj vrcanje'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
