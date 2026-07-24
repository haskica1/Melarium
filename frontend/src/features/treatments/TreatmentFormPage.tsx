import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { AlertCircle, ChevronDown, ChevronUp, Loader2 } from 'lucide-react'
import { useApiaries, useBeehivesByApiary } from '../../core/services/queries'
import { useTreatment, useCreateTreatment, useUpdateTreatment } from '../../core/services/treatmentQueries'
import {
  TreatmentPurpose, TreatmentPurposeLabels,
  ActiveSubstance, ActiveSubstanceLabels,
  ApplicationMethod, ApplicationMethodLabels,
} from '../../core/models'
import type { CreateTreatmentEntryPayload } from '../../core/models'
import { ConfirmDialog, FormHeader } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'
import { TREATMENT_PRESETS } from './presets'

const PURPOSES   = Object.values(TreatmentPurpose).filter(v => typeof v === 'number') as TreatmentPurpose[]
const SUBSTANCES = Object.values(ActiveSubstance).filter(v => typeof v === 'number') as ActiveSubstance[]
const METHODS    = Object.values(ApplicationMethod).filter(v => typeof v === 'number') as ApplicationMethod[]

const today = () => new Date().toISOString().split('T')[0]
// Backend tolerates start dates up to +1 day (evening entries across timezones).
const maxStart = () => {
  const d = new Date()
  d.setDate(d.getDate() + 1)
  return d.toISOString().split('T')[0]
}

interface TreatmentCommonPayload {
  purpose: TreatmentPurpose
  productName: string
  activeSubstance: ActiveSubstance
  method: ApplicationMethod
  dosePerHive: string
  startDate: string
  endDate: string | null
  withdrawalDays: number
  batchNumber: string | null
  supplier: string | null
  notes: string | null
  entries: CreateTreatmentEntryPayload[]
}

export default function TreatmentFormPage() {
  const { id } = useParams<{ id: string }>()
  const treatmentId = id ? parseInt(id) : undefined
  const isEdit = treatmentId !== undefined

  const navigate = useNavigate()
  const { toast } = useToast()

  const { data: apiaries = [] } = useApiaries()
  const { data: existing, isLoading: loadingExisting } = useTreatment(treatmentId ?? 0)
  const createTreatment = useCreateTreatment()
  const updateTreatment = useUpdateTreatment(treatmentId ?? 0)

  const [apiaryId, setApiaryId] = useState<number>(0)
  const [purpose, setPurpose] = useState<TreatmentPurpose>(TreatmentPurpose.Varroa)
  const [productName, setProductName] = useState<string>('')
  const [substance, setSubstance] = useState<ActiveSubstance>(ActiveSubstance.Amitraz)
  const [method, setMethod] = useState<ApplicationMethod>(ApplicationMethod.Strips)
  const [dosePerHive, setDosePerHive] = useState<string>('')
  const [startDate, setStartDate] = useState<string>(today())
  const [endDate, setEndDate] = useState<string>('')
  const [withdrawalDays, setWithdrawalDays] = useState<string>('0')
  const [batchNumber, setBatchNumber] = useState<string>('')
  const [supplier, setSupplier] = useState<string>('')
  const [notes, setNotes] = useState<string>('')
  const [checked, setChecked] = useState<Record<number, boolean>>({})
  const [doseNotes, setDoseNotes] = useState<Record<number, string>>({})
  const [presetIdx, setPresetIdx] = useState<string>('')
  const [showAdvanced, setShowAdvanced] = useState<boolean>(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [pendingSave, setPendingSave] = useState<TreatmentCommonPayload | null>(null)

  const { data: hives = [], isLoading: loadingHives } = useBeehivesByApiary(apiaryId)

  // Guards the "pre-check all hives" init so it runs once per apiary pick (not on refetches).
  const initializedApiary = useRef<number>(0)

  // Populate when editing
  useEffect(() => {
    if (existing && isEdit) {
      setApiaryId(existing.apiaryId)
      setPurpose(existing.purpose)
      setProductName(existing.productName)
      setSubstance(existing.activeSubstance)
      setMethod(existing.method)
      setDosePerHive(existing.dosePerHive)
      setStartDate(existing.startDate.split('T')[0])
      setEndDate(existing.endDate ? existing.endDate.split('T')[0] : '')
      setWithdrawalDays(String(existing.withdrawalDays))
      setBatchNumber(existing.batchNumber ?? '')
      setSupplier(existing.supplier ?? '')
      setNotes(existing.notes ?? '')
      // Reveal the advanced section when editing a record that already uses those fields.
      setShowAdvanced(Boolean(existing.batchNumber || existing.supplier || existing.notes))
      const c: Record<number, boolean> = {}
      const d: Record<number, string> = {}
      for (const e of existing.entries) {
        c[e.beehiveId] = true
        if (e.doseNote) d[e.beehiveId] = e.doseNote
      }
      setChecked(c)
      setDoseNotes(d)
      initializedApiary.current = existing.apiaryId
    }
  }, [existing, isEdit])

  // Varroa treatment is normally applied to the whole apiary — pre-check all hives once per apiary pick.
  useEffect(() => {
    if (isEdit || !apiaryId || hives.length === 0) return
    if (initializedApiary.current === apiaryId) return
    initializedApiary.current = apiaryId
    setChecked(Object.fromEntries(hives.map(h => [h.id, true])))
    setDoseNotes({})
  }, [apiaryId, hives, isEdit])

  const isSaving = createTreatment.isPending || updateTreatment.isPending
  const selectedCount = hives.filter(h => checked[h.id]).length
  const allChecked = hives.length > 0 && selectedCount === hives.length

  function applyPreset(idx: string) {
    setPresetIdx(idx)
    const p = TREATMENT_PRESETS[Number(idx)]
    if (!p) return
    setPurpose(p.purpose)
    setProductName(p.productName)
    setSubstance(p.activeSubstance)
    setMethod(p.method)
    setDosePerHive(p.dosePerHive)
    setWithdrawalDays(String(p.withdrawalDays))
  }

  function buildEntries(): CreateTreatmentEntryPayload[] {
    return hives
      .filter(h => checked[h.id])
      .map(h => ({
        beehiveId: h.id,
        doseNote: doseNotes[h.id]?.trim() || null,
      }))
  }

  async function performSave(common: TreatmentCommonPayload) {
    try {
      if (isEdit && treatmentId) {
        await updateTreatment.mutateAsync(common)
        toast.success('Tretman ažuriran.')
      } else {
        await createTreatment.mutateAsync({ apiaryId, ...common })
        toast.success('Tretman zabilježen.')
      }
      navigate('/treatments')
    } catch (err: any) {
      const detail = err?.response?.data?.errors?.entries?.[0]
        ?? err?.response?.data?.detail
        ?? 'Greška pri čuvanju tretmana. Provjerite podatke i pokušajte ponovo.'
      setFormError(detail)
    }
  }

  function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    if (!apiaryId) { setFormError('Odaberite pčelinjak.'); return }
    if (!productName.trim()) { setFormError('Unesite naziv preparata.'); return }
    if (!dosePerHive.trim()) { setFormError('Unesite dozu po košnici.'); return }
    const entries = buildEntries()
    if (entries.length === 0) { setFormError('Označite barem jednu košnicu.'); return }
    if (endDate && endDate < startDate) { setFormError('Kraj tretmana ne može biti prije početka.'); return }
    const karenca = parseInt(withdrawalDays)
    if (isNaN(karenca) || karenca < 0 || karenca > 365) { setFormError('Karenca mora biti između 0 i 365 dana.'); return }

    const common: TreatmentCommonPayload = {
      purpose,
      productName: productName.trim(),
      activeSubstance: substance,
      method,
      dosePerHive: dosePerHive.trim(),
      startDate,
      endDate: endDate || null,
      withdrawalDays: karenca,
      batchNumber: batchNumber.trim() || null,
      supplier: supplier.trim() || null,
      notes: notes.trim() || null,
      entries,
    }

    // Soft warning (not a hard block) for a start date from a prior calendar year — catches an
    // accidental wrong-year date-picker slip without blocking legitimate late/backfilled entries.
    if (Number(startDate.slice(0, 4)) < new Date().getFullYear()) {
      setPendingSave(common)
      return
    }
    performSave(common)
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
  const labelClass = 'block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5'

  return (
    <div className="max-w-2xl mx-auto">
      <FormHeader
        icon="💊"
        title={isEdit ? 'Uredi tretman' : 'Novi tretman'}
        onBack={() => navigate('/treatments')}
        backLabel="Nazad na tretmane"
      />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-8 py-8">
        {formError && (
          <div className="flex items-start gap-2 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm mb-5">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            {formError}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-6">
          {/* Preset quick-fill */}
          <div className="bg-honey-50 dark:bg-slate-800/60 border border-honey-100 dark:border-slate-700 rounded-xl px-4 py-3">
            <label className={labelClass}>Brzi odabir preparata</label>
            <select value={presetIdx} onChange={e => applyPreset(e.target.value)} className={inputClass}>
              <option value="">Ručni unos…</option>
              {TREATMENT_PRESETS.map((p, i) => <option key={p.label} value={i}>{p.label}</option>)}
            </select>
            <p className="text-xs text-gray-500 dark:text-slate-400 mt-1.5">
              Popunjava preparat, tvar, način, dozu i karencu — sve ostaje izmjenjivo. Provjerite karencu na etiketi.
            </p>
          </div>

          {/* Apiary + purpose */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className={labelClass}>
                Pčelinjak <span className="text-red-500">*</span>
              </label>
              <select
                value={apiaryId}
                onChange={e => { setApiaryId(Number(e.target.value)); setChecked({}); setDoseNotes({}) }}
                disabled={isEdit}
                className={`${inputClass} disabled:opacity-60 disabled:cursor-not-allowed`}
              >
                <option value={0} disabled>Odaberite pčelinjak…</option>
                {apiaries.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
              </select>
            </div>
            <div>
              <label className={labelClass}>Namjena</label>
              <select value={purpose} onChange={e => setPurpose(Number(e.target.value))} className={inputClass}>
                {PURPOSES.map(p => <option key={p} value={p}>{TreatmentPurposeLabels[p]}</option>)}
              </select>
            </div>
          </div>

          {/* Preparat + doza (bitno) */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className={labelClass}>
                Preparat <span className="text-red-500">*</span>
              </label>
              <input type="text" placeholder="npr. Apivar" value={productName} onChange={e => setProductName(e.target.value)} className={inputClass} />
            </div>
            <div>
              <label className={labelClass}>
                Doza po košnici <span className="text-red-500">*</span>
              </label>
              <input type="text" placeholder="npr. 2 trake po košnici" value={dosePerHive} onChange={e => setDosePerHive(e.target.value)} className={inputClass} />
            </div>
          </div>

          {/* Dates + karenca */}
          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className={labelClass}>
                Početak <span className="text-red-500">*</span>
              </label>
              <input type="date" value={startDate} max={maxStart()} onChange={e => setStartDate(e.target.value)} className={inputClass} />
            </div>
            <div>
              <label className={labelClass}>Kraj</label>
              <input type="date" value={endDate} min={startDate} onChange={e => setEndDate(e.target.value)} className={inputClass} />
              <p className="text-xs text-gray-400 dark:text-slate-500 mt-1">Prazno = tretman u toku.</p>
            </div>
            <div>
              <label className={labelClass}>Karenca (dana)</label>
              <input type="number" min="0" max="365" step="1" value={withdrawalDays} onChange={e => setWithdrawalDays(e.target.value)} className={inputClass} />
            </div>
          </div>

          {/* Hive selection */}
          <div>
            <div className="flex items-center justify-between mb-2">
              <label className="text-sm font-medium text-gray-700 dark:text-slate-300">
                Tretirane košnice <span className="text-red-500">*</span>
              </label>
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
            ) : hives.length === 0 ? (
              <p className="text-sm text-gray-400 dark:text-slate-500 py-4">Ovaj pčelinjak nema košnica.</p>
            ) : (
              <>
                <div className="grid grid-cols-[auto_2fr_2fr] gap-2 px-1 mb-1 items-center">
                  <span />
                  {['Košnica', 'Odstupanje doze (opcionalno)'].map(h => (
                    <span key={h} className="text-xs font-medium text-gray-400 dark:text-slate-500">{h}</span>
                  ))}
                </div>
                <div className="space-y-2">
                  {hives.map(hive => (
                    <div key={hive.id} className="grid grid-cols-[auto_2fr_2fr] gap-2 items-center">
                      <input
                        type="checkbox"
                        checked={!!checked[hive.id]}
                        onChange={e => setChecked(prev => ({ ...prev, [hive.id]: e.target.checked }))}
                        className="w-4 h-4 rounded border-gray-300 dark:border-slate-600 text-honey-500 focus:ring-honey-400 accent-honey-500"
                      />
                      <span className="text-sm text-gray-700 dark:text-slate-200 truncate">{hive.name}</span>
                      <input
                        type="text" placeholder="—"
                        value={doseNotes[hive.id] ?? ''}
                        disabled={!checked[hive.id]}
                        onChange={e => setDoseNotes(prev => ({ ...prev, [hive.id]: e.target.value }))}
                        className="px-3 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all disabled:opacity-40"
                      />
                    </div>
                  ))}
                </div>
                <p className="text-xs text-gray-400 dark:text-slate-500 mt-2">
                  Odabrano: {selectedCount} od {hives.length}. Odstupanje unesite samo ako se doza razlikuje od navedene.
                </p>
                {selectedCount === 0 && (
                  <p className="text-xs text-red-500 dark:text-red-400 mt-1">Odaberite barem jednu košnicu da biste sačuvali tretman.</p>
                )}
              </>
            )}
          </div>

          {/* Više detalja — napredna / zakonska polja (skriveno po defaultu za brži unos) */}
          <div className="border-t border-gray-100 dark:border-slate-800 pt-4">
            <button
              type="button"
              onClick={() => setShowAdvanced(v => !v)}
              className="flex items-center gap-1.5 text-sm font-medium text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300 transition-colors"
            >
              {showAdvanced ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
              Više detalja
              <span className="text-xs font-normal text-gray-400 dark:text-slate-500">(tvar, način, LOT, dobavljač, napomena)</span>
            </button>

            {showAdvanced && (
              <div className="space-y-4 mt-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className={labelClass}>Aktivna tvar</label>
                    <select value={substance} onChange={e => setSubstance(Number(e.target.value))} className={inputClass}>
                      {SUBSTANCES.map(s => <option key={s} value={s}>{ActiveSubstanceLabels[s]}</option>)}
                    </select>
                  </div>
                  <div>
                    <label className={labelClass}>Način primjene</label>
                    <select value={method} onChange={e => setMethod(Number(e.target.value))} className={inputClass}>
                      {METHODS.map(m => <option key={m} value={m}>{ApplicationMethodLabels[m]}</option>)}
                    </select>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className={labelClass}>LOT broj</label>
                    <input type="text" placeholder="broj serije s pakovanja" value={batchNumber} onChange={e => setBatchNumber(e.target.value)} className={inputClass} />
                    <p className="text-xs text-gray-400 dark:text-slate-500 mt-1">Zakonski se očekuje u evidenciji.</p>
                  </div>
                  <div>
                    <label className={labelClass}>Dobavljač</label>
                    <input type="text" placeholder="gdje je preparat kupljen" value={supplier} onChange={e => setSupplier(e.target.value)} className={inputClass} />
                  </div>
                </div>

                <div>
                  <label className={labelClass}>Napomena</label>
                  <input type="text" placeholder="npr. Jesenji tretman nakon vrcanja" value={notes} onChange={e => setNotes(e.target.value)} className={inputClass} />
                </div>
              </div>
            )}
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={() => navigate('/treatments')} className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors">
              Otkaži
            </button>
            <button type="submit" disabled={isSaving || selectedCount === 0} className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors">
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isEdit ? 'Spremi promjene' : 'Sačuvaj tretman'}
            </button>
          </div>
        </form>
      </div>

      <ConfirmDialog
        isOpen={!!pendingSave}
        title="Datum iz prošle godine"
        message={pendingSave
          ? `Datum početka tretmana je ${pendingSave.startDate.split('-').reverse().join('.')}. — iz ${pendingSave.startDate.slice(0, 4)}. godine. Je li ovo namjerno?`
          : ''}
        confirmLabel="Da, sačuvaj"
        onConfirm={() => { const p = pendingSave; setPendingSave(null); if (p) performSave(p) }}
        onCancel={() => setPendingSave(null)}
        isLoading={isSaving}
      />
    </div>
  )
}
