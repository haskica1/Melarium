import { useEffect, useState } from 'react'
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import { Flower2, Loader2, MapPin, PencilLine, Plus, Tent, Trash2, X } from 'lucide-react'
import {
  usePastures,
  useCreatePasture,
  useUpdatePasture,
  useDeletePasture,
} from '../../core/services/pastureQueries'
import type { Pasture, SavePasturePayload } from '../../core/models'
import { ConfirmDialog, EmptyState, VitalsSkeleton } from '../../shared/components'
import LocationPickerModal from '../../shared/components/LocationPickerModal'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useToast } from '../../core/context/ToastContext'

/** Auto-fits the map so every pasture marker is visible; re-fits only when the coordinates change. */
function FitBounds({ points }: { points: [number, number][] }) {
  const map = useMap()
  const key = points.map(p => p.join(',')).join('|')
  useEffect(() => {
    if (points.length === 0) return
    if (points.length === 1) map.setView(points[0], 12)
    else map.fitBounds(points, { padding: [40, 40], maxZoom: 13 })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, key])
  return null
}

export default function PasturesPage() {
  const { canManageApiaries } = usePermissions()
  const { toast } = useToast()

  const { data: pastures = [], isLoading } = usePastures()
  const createPasture = useCreatePasture()
  const updatePasture = useUpdatePasture()
  const deletePasture = useDeletePasture()

  const [formTarget, setFormTarget] = useState<Pasture | 'new' | null>(null)
  const [confirmTarget, setConfirmTarget] = useState<Pasture | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  const located = pastures.filter(p => p.latitude != null && p.longitude != null)
  const occupied = pastures.filter(p => p.apiariesOnPasture > 0).length

  async function handleSave(payload: SavePasturePayload) {
    if (formTarget === 'new') {
      await createPasture.mutateAsync(payload)
      toast.success('Pašnjak dodan.')
    } else if (formTarget) {
      await updatePasture.mutateAsync({ id: formTarget.id, payload })
      toast.success('Pašnjak ažuriran.')
    }
    setFormTarget(null)
  }

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await deletePasture.mutateAsync(confirmTarget.id)
      toast.success(`Pašnjak "${confirmTarget.name}" obrisan.`)
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.errors?.pasture?.[0]
        ?? e?.response?.data?.detail
        ?? 'Greška pri brisanju pašnjaka.')
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <div className="animate-fade-in space-y-6">
      {/* Hero */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center gap-4 min-w-0">
            <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center shadow-honey dark:shadow-none">
              <Tent className="w-7 h-7 text-honey-600 dark:text-honey-400" />
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Pašnjaci</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
                Registar paša za selidbe — {occupied} od {pastures.length} trenutno zauzeto.
              </p>
            </div>
          </div>
          {canManageApiaries && (
            <button onClick={() => setFormTarget('new')} className="btn-primary text-sm shrink-0">
              <Plus className="w-4 h-4" /> Novi pašnjak
            </button>
          )}
        </div>
      </div>

      {isLoading && <VitalsSkeleton />}

      {!isLoading && pastures.length === 0 && (
        <EmptyState
          title="Još nema pašnjaka."
          description="Dodajte pašnjake na koje selite pčelinjake — selidbe se bilježe na stranici pčelinjaka."
          action={canManageApiaries ? (
            <button onClick={() => setFormTarget('new')} className="btn-primary text-sm">
              <Plus className="w-4 h-4" /> Novi pašnjak
            </button>
          ) : undefined}
        />
      )}

      {/* Overview map */}
      {/* `isolate` contains Leaflet's internal z-indexed panes/controls (up to ~800) inside their own
          stacking context — otherwise they leak above later-in-DOM but lower-z-index elements like
          the "new pasture" modal (z-50). */}
      {!isLoading && located.length > 0 && (
        <div className="relative isolate rounded-2xl overflow-hidden border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none">
          <MapContainer
            center={[located[0].latitude!, located[0].longitude!]}
            zoom={9}
            style={{ height: 320, width: '100%' }}
            scrollWheelZoom={false}
          >
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <FitBounds points={located.map(p => [p.latitude!, p.longitude!] as [number, number])} />
            {located.map(p => (
              <Marker key={p.id} position={[p.latitude!, p.longitude!]}>
                <Popup>
                  <strong>{p.name}</strong>
                  {p.floraNotes && <><br />{p.floraNotes}</>}
                  <br />{p.apiariesOnPasture > 0 ? `Pčelinjaka trenutno: ${p.apiariesOnPasture}` : 'Slobodan'}
                </Popup>
              </Marker>
            ))}
          </MapContainer>
        </div>
      )}

      {/* Registry */}
      {!isLoading && pastures.length > 0 && (
        <div className="grid sm:grid-cols-2 gap-3">
          {pastures.map(p => (
            <div key={p.id} className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4">
              <div className="flex items-start gap-3">
                <div className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 bg-honey-50 text-honey-600 dark:bg-honey-500/15 dark:text-honey-300">
                  <Tent className="w-5 h-5" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-semibold text-gray-900 dark:text-slate-100">{p.name}</span>
                    {p.apiariesOnPasture > 0 && (
                      <span className="text-xs rounded-full px-2 py-0.5 bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300">
                        {p.apiariesOnPasture} {p.apiariesOnPasture === 1 ? 'pčelinjak' : 'pčelinjaka'}
                      </span>
                    )}
                  </div>
                  {p.floraNotes && (
                    <p className="mt-0.5 text-sm text-gray-500 dark:text-slate-400 flex items-center gap-1.5">
                      <Flower2 className="w-3.5 h-3.5 shrink-0" /> {p.floraNotes}
                    </p>
                  )}
                  <p className="mt-0.5 text-xs text-gray-400 dark:text-slate-500 flex items-center gap-1.5">
                    <MapPin className="w-3 h-3 shrink-0" />
                    {p.latitude != null && p.longitude != null
                      ? `${p.latitude.toFixed(4)}, ${p.longitude.toFixed(4)}${p.address ? ` · ${p.address}` : ''}`
                      : p.address ?? 'Bez lokacije'}
                  </p>
                </div>
                {canManageApiaries && (
                  <div className="flex items-center gap-1 shrink-0">
                    <button
                      onClick={() => setFormTarget(p)}
                      className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                      aria-label="Uredi pašnjak"
                    >
                      <PencilLine className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => setConfirmTarget(p)}
                      className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                      aria-label="Obriši pašnjak"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {formTarget && (
        <PastureFormModal
          pasture={formTarget === 'new' ? null : formTarget}
          isSaving={createPasture.isPending || updatePasture.isPending}
          onSave={handleSave}
          onClose={() => setFormTarget(null)}
        />
      )}

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši pašnjak"
        message={confirmTarget ? `Obrisati pašnjak "${confirmTarget.name}"? Brisanje nije moguće dok je na njemu pčelinjak ili dok postoje selidbe koje ga referenciraju.` : ''}
        confirmLabel="Obriši"
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}

// ── Form modal ─────────────────────────────────────────────────────────────────

interface PastureFormModalProps {
  pasture: Pasture | null
  isSaving: boolean
  onSave: (payload: SavePasturePayload) => Promise<void>
  onClose: () => void
}

function PastureFormModal({ pasture, isSaving, onSave, onClose }: PastureFormModalProps) {
  const [name, setName] = useState(pasture?.name ?? '')
  const [address, setAddress] = useState(pasture?.address ?? '')
  const [floraNotes, setFloraNotes] = useState(pasture?.floraNotes ?? '')
  const [notes, setNotes] = useState(pasture?.notes ?? '')
  const [location, setLocation] = useState<{ lat: number; lng: number } | null>(
    pasture?.latitude != null && pasture?.longitude != null
      ? { lat: pasture.latitude, lng: pasture.longitude }
      : null,
  )
  const [pickerOpen, setPickerOpen] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)
    if (!name.trim()) { setFormError('Naziv pašnjaka je obavezan.'); return }

    try {
      await onSave({
        name: name.trim(),
        latitude: location?.lat ?? null,
        longitude: location?.lng ?? null,
        address: address.trim() || null,
        floraNotes: floraNotes.trim() || null,
        notes: notes.trim() || null,
      })
    } catch (err: any) {
      const errors = err?.response?.data?.errors ?? err?.response?.data
      const first = errors && typeof errors === 'object' ? (Object.values(errors)[0] as string[])?.[0] : undefined
      setFormError(first ?? err?.response?.data?.detail ?? 'Greška pri čuvanju pašnjaka.')
    }
  }

  const inputClass =
    'w-full px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all'
  const labelClass = 'block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="bg-white dark:bg-slate-900 rounded-2xl shadow-xl border border-honey-100 dark:border-slate-800 w-full max-w-lg max-h-[90vh] overflow-y-auto"
        onClick={e => e.stopPropagation()}
      >
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-slate-800">
          <h2 className="font-display text-lg font-semibold text-gray-900 dark:text-slate-100">
            {pasture ? 'Uredi pašnjak' : 'Novi pašnjak'}
          </h2>
          <button onClick={onClose} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 dark:hover:bg-slate-800 transition-colors" aria-label="Zatvori">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">
          {formError && (
            <p className="text-sm text-red-600 dark:text-red-300 bg-red-50 dark:bg-red-500/10 rounded-lg px-4 py-3">{formError}</p>
          )}

          <div>
            <label className={labelClass}>Naziv <span className="text-red-500">*</span></label>
            <input type="text" maxLength={100} placeholder="npr. Kadulja — Podveležje" value={name} onChange={e => setName(e.target.value)} className={inputClass} />
          </div>

          <div>
            <label className={labelClass}>Lokacija</label>
            <div className="flex items-center gap-2">
              <button type="button" onClick={() => setPickerOpen(true)} className="flex items-center gap-1.5 px-3 py-2 rounded-xl border border-honey-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-700 transition-colors">
                <MapPin className="w-4 h-4 text-honey-600 dark:text-honey-400" />
                {location ? 'Promijeni na mapi' : 'Odaberi na mapi'}
              </button>
              {location && (
                <>
                  <span className="text-xs text-gray-500 dark:text-slate-400">{location.lat.toFixed(4)}, {location.lng.toFixed(4)}</span>
                  <button type="button" onClick={() => setLocation(null)} className="text-xs text-red-500 hover:underline">Ukloni</button>
                </>
              )}
            </div>
            <p className="text-xs text-gray-400 dark:text-slate-500 mt-1">
              Selidbom pčelinjak preuzima koordinate pašnjaka (prognoza i mapa prate lokaciju).
            </p>
          </div>

          <div>
            <label className={labelClass}>Adresa / opis puta</label>
            <input type="text" maxLength={200} placeholder="npr. makadam iznad sela Kruševo" value={address} onChange={e => setAddress(e.target.value)} className={inputClass} />
          </div>

          <div>
            <label className={labelClass}>Flora</label>
            <input type="text" maxLength={300} placeholder="npr. bagrem, lipa; paša traje V–VI" value={floraNotes} onChange={e => setFloraNotes(e.target.value)} className={inputClass} />
          </div>

          <div>
            <label className={labelClass}>Napomena</label>
            <input type="text" maxLength={500} placeholder="npr. dogovor s vlasnikom parcele" value={notes} onChange={e => setNotes(e.target.value)} className={inputClass} />
          </div>

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="flex-1 px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors">
              Otkaži
            </button>
            <button type="submit" disabled={isSaving} className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors">
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {pasture ? 'Spremi promjene' : 'Sačuvaj pašnjak'}
            </button>
          </div>
        </form>
      </div>

      {pickerOpen && (
        <LocationPickerModal
          initialLat={location?.lat}
          initialLng={location?.lng}
          onConfirm={(lat, lng) => { setLocation({ lat, lng }); setPickerOpen(false) }}
          onClose={() => setPickerOpen(false)}
        />
      )}
    </div>
  )
}
