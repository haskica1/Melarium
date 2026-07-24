import { useState } from 'react'
import { format } from 'date-fns'
import { ArrowRight, FileCheck2, Home, Loader2, MapPin, Tent, Trash2, Truck, X } from 'lucide-react'
import { CollapsibleSection } from '../../shared/components/CollapsibleSection'
import { ConfirmDialog } from '../../shared/components'
import LocationPickerModal from '../../shared/components/LocationPickerModal'
import {
  useApiaryMoves,
  usePastures,
  useCreateApiaryMove,
  useDeleteApiaryMove,
  useReturnHomeApiaryMove,
  useSetHomeLocation,
} from '../../core/services/pastureQueries'
import type { ApiaryMove } from '../../core/models'
import { useToast } from '../../core/context/ToastContext'

const today = () => new Date().toISOString().split('T')[0]

interface ApiaryMovesSectionProps {
  apiaryId: number
  canManage: boolean
  /** Whether the apiary's matična lokacija has been captured (may be unknown for pre-existing apiaries). */
  hasHomeLocation: boolean
}

/** "Selidbe" section for the apiary detail page (SPEC-10) — history + "Preseli"/"Vrati na matičnu lokaciju". */
export function ApiaryMovesSection({ apiaryId, canManage, hasHomeLocation }: ApiaryMovesSectionProps) {
  const { toast } = useToast()
  const { data: moves = [], isLoading } = useApiaryMoves(apiaryId)
  const deleteMove = useDeleteApiaryMove(apiaryId)
  const setHomeLocation = useSetHomeLocation(apiaryId)

  const [moveModalOpen, setMoveModalOpen] = useState(false)
  const [homePickerOpen, setHomePickerOpen] = useState(false)
  const [confirmTarget, setConfirmTarget] = useState<ApiaryMove | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  const latest = moves[0]
  const isAway = latest != null && latest.toPastureId != null

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await deleteMove.mutateAsync(confirmTarget.id)
      toast.success('Selidba obrisana — pčelinjak je vraćen na prethodnu lokaciju.')
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.errors?.moveId?.[0] ?? e?.response?.data?.detail ?? 'Greška pri brisanju selidbe.')
    } finally {
      setIsDeleting(false)
    }
  }

  async function handleSetHomeLocation(lat: number, lng: number) {
    try {
      await setHomeLocation.mutateAsync({ latitude: lat, longitude: lng })
      toast.success('Matična lokacija je postavljena.')
      setHomePickerOpen(false)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri postavljanju matične lokacije.')
    }
  }

  return (
    <CollapsibleSection
      title="Selidbe"
      icon="⛺"
      count={moves.length}
      defaultOpen={false}
      action={canManage ? (
        <div className="flex items-center gap-3">
          {isAway && !hasHomeLocation && (
            <button
              onClick={() => setHomePickerOpen(true)}
              className="inline-flex items-center gap-1 text-xs text-gray-500 dark:text-slate-400 hover:underline font-medium"
              title="Matična lokacija nije poznata — postavite je da biste mogli koristiti 'Vrati na matičnu lokaciju'"
            >
              <MapPin className="w-3.5 h-3.5" /> Postavi matičnu lokaciju
            </button>
          )}
          <button
            onClick={() => setMoveModalOpen(true)}
            className="inline-flex items-center gap-1 text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium"
          >
            <Truck className="w-3.5 h-3.5" /> Preseli
          </button>
        </div>
      ) : undefined}
    >
      {isLoading ? (
        <div className="flex justify-center py-6"><Loader2 className="w-5 h-5 animate-spin text-honey-500" /></div>
      ) : moves.length === 0 ? (
        <p className="text-center py-6 text-sm text-gray-400 dark:text-slate-500">
          Pčelinjak je na matičnoj lokaciji — još nema zabilježenih selidbi.
        </p>
      ) : (
        <div className="space-y-2">
          {moves.map(m => (
            <div key={m.id} className="flex items-center gap-3 px-3 py-2.5 rounded-xl bg-gray-50 dark:bg-slate-800/60">
              <span className="text-sm text-gray-500 dark:text-slate-400 w-20 shrink-0">
                {format(new Date(m.movedAt), 'dd.MM.yyyy')}
              </span>
              <span className="flex items-center gap-1.5 text-sm min-w-0 flex-wrap">
                <span className="text-gray-500 dark:text-slate-400">{m.fromPastureName ?? 'Matična lokacija'}</span>
                <ArrowRight className="w-3.5 h-3.5 text-gray-400 shrink-0" />
                <span className="font-medium text-gray-800 dark:text-slate-100">{m.toPastureName}</span>
              </span>
              {m.certificateNumber && (
                <span className="hidden sm:flex items-center gap-1 text-xs text-gray-400 dark:text-slate-500 shrink-0" title="Broj veterinarske svjedodžbe">
                  <FileCheck2 className="w-3.5 h-3.5" /> {m.certificateNumber}
                </span>
              )}
              {canManage && m.id === latest?.id && (
                <button
                  onClick={() => setConfirmTarget(m)}
                  className="ml-auto p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors shrink-0"
                  aria-label="Obriši selidbu"
                  title="Obriši (samo posljednja selidba)"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              )}
            </div>
          ))}
        </div>
      )}

      {moveModalOpen && (
        <MoveApiaryModal
          apiaryId={apiaryId}
          currentPastureId={latest?.toPastureId ?? null}
          canReturnHome={isAway && hasHomeLocation}
          onClose={() => setMoveModalOpen(false)}
        />
      )}

      {homePickerOpen && (
        <LocationPickerModal
          onConfirm={handleSetHomeLocation}
          onClose={() => setHomePickerOpen(false)}
        />
      )}

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši selidbu"
        message={confirmTarget
          ? `Obrisati selidbu na "${confirmTarget.toPastureName}" od ${format(new Date(confirmTarget.movedAt), 'dd.MM.yyyy')}? Pčelinjak se vraća na prethodnu lokaciju (${confirmTarget.fromPastureName ?? 'matična lokacija'}).`
          : ''}
        confirmLabel="Obriši"
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </CollapsibleSection>
  )
}

// ── Move modal ─────────────────────────────────────────────────────────────────

function MoveApiaryModal({ apiaryId, currentPastureId, canReturnHome, onClose }: {
  apiaryId: number
  currentPastureId: number | null
  canReturnHome: boolean
  onClose: () => void
}) {
  const { toast } = useToast()
  const { data: pastures = [] } = usePastures()
  const createMove = useCreateApiaryMove(apiaryId)
  const returnHome = useReturnHomeApiaryMove(apiaryId)

  const [toPastureId, setToPastureId] = useState<number>(0)
  const [movedAt, setMovedAt] = useState<string>(today())
  const [certificateNumber, setCertificateNumber] = useState('')
  const [notes, setNotes] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const options = pastures.filter(p => p.id !== currentPastureId)

  async function handleReturnHome() {
    try {
      await returnHome.mutateAsync()
      toast.success('Pčelinjak je vraćen na matičnu lokaciju.')
      onClose()
    } catch (e: any) {
      toast.error(e?.response?.data?.errors?.apiaryId?.[0] ?? e?.response?.data?.detail ?? 'Greška pri povratku na matičnu lokaciju.')
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)
    if (!toPastureId) { setFormError('Odaberite pašnjak.'); return }

    try {
      await createMove.mutateAsync({
        toPastureId,
        movedAt,
        certificateNumber: certificateNumber.trim() || null,
        notes: notes.trim() || null,
      })
      toast.success('Selidba zabilježena — lokacija pčelinjaka je ažurirana.')
      onClose()
    } catch (err: any) {
      const errors = err?.response?.data?.errors ?? err?.response?.data
      const first = errors && typeof errors === 'object' ? (Object.values(errors)[0] as string[])?.[0] : undefined
      setFormError(first ?? err?.response?.data?.detail ?? 'Greška pri bilježenju selidbe.')
    }
  }

  const inputClass =
    'w-full px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all'
  const labelClass = 'block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="bg-white dark:bg-slate-900 rounded-2xl shadow-xl border border-honey-100 dark:border-slate-800 w-full max-w-md"
        onClick={e => e.stopPropagation()}
      >
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-slate-800">
          <h2 className="font-display text-lg font-semibold text-gray-900 dark:text-slate-100 flex items-center gap-2">
            <Tent className="w-5 h-5 text-honey-500" /> Preseli pčelinjak
          </h2>
          <button onClick={onClose} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 dark:hover:bg-slate-800 transition-colors" aria-label="Zatvori">
            <X className="w-5 h-5" />
          </button>
        </div>

        {canReturnHome && (
          <div className="px-6 pt-5">
            <button
              type="button"
              onClick={handleReturnHome}
              disabled={returnHome.isPending}
              className="w-full flex items-center justify-center gap-2 px-4 py-3 rounded-xl border-2 border-dashed border-honey-300 dark:border-honey-500/40 text-honey-700 dark:text-honey-300 text-sm font-semibold hover:bg-honey-50 dark:hover:bg-honey-500/10 transition-colors disabled:opacity-60"
            >
              {returnHome.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Home className="w-4 h-4" />}
              Vrati na matičnu lokaciju
            </button>
            <div className="flex items-center gap-3 my-4">
              <div className="h-px flex-1 bg-gray-200 dark:bg-slate-700" />
              <span className="text-xs text-gray-400 dark:text-slate-500 shrink-0">ili preseli na drugi pašnjak</span>
              <div className="h-px flex-1 bg-gray-200 dark:bg-slate-700" />
            </div>
          </div>
        )}

        <form onSubmit={handleSubmit} className={`px-6 pb-5 space-y-4 ${canReturnHome ? '' : 'pt-5'}`}>
          {formError && (
            <p className="text-sm text-red-600 dark:text-red-300 bg-red-50 dark:bg-red-500/10 rounded-lg px-4 py-3">{formError}</p>
          )}

          <div>
            <label className={labelClass}>Na pašnjak <span className="text-red-500">*</span></label>
            <select value={toPastureId} onChange={e => setToPastureId(Number(e.target.value))} className={inputClass}>
              <option value={0} disabled>Odaberite pašnjak…</option>
              {options.map(p => (
                <option key={p.id} value={p.id}>
                  {p.name}{p.floraNotes ? ` — ${p.floraNotes}` : ''}
                </option>
              ))}
            </select>
            {options.length === 0 && (
              <p className="text-xs text-amber-600 dark:text-amber-400 mt-1">
                Nema dostupnih pašnjaka — dodajte ih na stranici "Pašnjaci".
              </p>
            )}
          </div>

          <div>
            <label className={labelClass}>Datum selidbe <span className="text-red-500">*</span></label>
            <input type="date" value={movedAt} max={today()} onChange={e => setMovedAt(e.target.value)} className={inputClass} />
          </div>

          <div>
            <label className={labelClass}>Broj veterinarske svjedodžbe</label>
            <input type="text" maxLength={50} placeholder="zakonski se očekuje pri selidbi" value={certificateNumber} onChange={e => setCertificateNumber(e.target.value)} className={inputClass} />
          </div>

          <div>
            <label className={labelClass}>Napomena</label>
            <input type="text" maxLength={500} placeholder="npr. 24 košnice, prevoz traktorom" value={notes} onChange={e => setNotes(e.target.value)} className={inputClass} />
          </div>

          <p className="text-xs text-gray-400 dark:text-slate-500">
            Pčelinjak preuzima koordinate pašnjaka — prognoza, alarmi i mapa odmah prate novu lokaciju.
          </p>

          <div className="flex gap-3 pt-1">
            <button type="button" onClick={onClose} className="flex-1 px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors">
              Otkaži
            </button>
            <button type="submit" disabled={createMove.isPending} className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors">
              {createMove.isPending && <Loader2 className="w-4 h-4 animate-spin" />}
              Preseli
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
