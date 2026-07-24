import { useEffect, useState } from 'react'
import { MapContainer, TileLayer, Marker, useMapEvents } from 'react-leaflet'
import { Crosshair, MapPin, X } from 'lucide-react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

// Fix Leaflet's broken default icons when bundled with Vite
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
delete (L.Icon.Default.prototype as any)._getIconUrl
L.Icon.Default.mergeOptions({ iconUrl: markerIcon, iconRetinaUrl: markerIcon2x, shadowUrl: markerShadow })

// Amber-coloured drop-pin icon
const amberIcon = new L.Icon({
  iconUrl: markerIcon,
  iconRetinaUrl: markerIcon2x,
  shadowUrl: markerShadow,
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
  className: 'leaflet-marker-amber',
})

interface Props {
  initialLat?: number | null
  initialLng?: number | null
  onConfirm: (lat: number, lng: number) => void
  onClose: () => void
}

interface LatLng { lat: number; lng: number }

const DEFAULT_CENTER: LatLng = { lat: 44.0, lng: 17.5 } // Bosnia/Croatia fallback
const DEFAULT_ZOOM = 7

function ClickHandler({ onPick }: { onPick: (ll: LatLng) => void }) {
  useMapEvents({ click: (e) => onPick({ lat: e.latlng.lat, lng: e.latlng.lng }) })
  return null
}

export default function LocationPickerModal({ initialLat, initialLng, onConfirm, onClose }: Props) {
  const hasInitial = initialLat != null && initialLng != null
  const [pin, setPin] = useState<LatLng | null>(
    hasInitial ? { lat: initialLat!, lng: initialLng! } : null
  )
  const [center, setCenter] = useState<LatLng>(
    hasInitial ? { lat: initialLat!, lng: initialLng! } : DEFAULT_CENTER
  )
  const [zoom] = useState(hasInitial ? 13 : DEFAULT_ZOOM)
  const [locating, setLocating] = useState(false)

  // Try to get the user's location on first open (only if no existing coords)
  useEffect(() => {
    if (hasInitial) return
    setLocating(true)
    navigator.geolocation?.getCurrentPosition(
      (pos) => {
        setCenter({ lat: pos.coords.latitude, lng: pos.coords.longitude })
        setLocating(false)
      },
      () => setLocating(false),
      { timeout: 5000 }
    )
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  function handleLocateMe() {
    setLocating(true)
    navigator.geolocation?.getCurrentPosition(
      (pos) => {
        setCenter({ lat: pos.coords.latitude, lng: pos.coords.longitude })
        setPin({ lat: pos.coords.latitude, lng: pos.coords.longitude })
        setLocating(false)
      },
      () => setLocating(false),
      { timeout: 8000 }
    )
  }

  function fmt(n: number) {
    return n.toFixed(6)
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center bg-black/60"
      onClick={e => e.stopPropagation()}
    >
      <div
        className="relative flex flex-col w-full max-w-2xl mx-0 sm:mx-4 bg-white dark:bg-slate-900 rounded-t-2xl sm:rounded-2xl shadow-2xl overflow-hidden"
        style={{ height: '85dvh', maxHeight: 640 }}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-honey-100 dark:border-slate-800 shrink-0">
          <div className="flex items-center gap-2">
            <MapPin className="w-4 h-4 text-honey-600 dark:text-honey-400" />
            <span className="font-semibold text-gray-800 dark:text-slate-100 text-sm">Odaberi lokaciju pčelinjaka</span>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-full text-gray-400 dark:text-slate-500 hover:bg-gray-100 dark:hover:bg-slate-800 hover:text-gray-600 dark:hover:text-slate-300 transition-colors"
            aria-label="Zatvori"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Instruction bar */}
        <div className="px-4 py-2 bg-honey-50 dark:bg-slate-800/60 border-b border-honey-100 dark:border-slate-800 text-xs text-honey-800 dark:text-honey-300 shrink-0">
          Kliknite bilo gdje na mapi da postavite marker.
        </div>

        {/* Map — `isolate` keeps Leaflet's internal z-indexed panes/controls from leaking onto siblings */}
        <div className="flex-1 relative isolate">
          <MapContainer
            center={[center.lat, center.lng]}
            zoom={zoom}
            className="w-full h-full"
            zoomControl={true}
          >
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <ClickHandler onPick={setPin} />
            {pin && <Marker position={[pin.lat, pin.lng]} icon={amberIcon} />}
          </MapContainer>

          {/* Locate me button */}
          <button
            onClick={handleLocateMe}
            disabled={locating}
            title="Koristi moju lokaciju"
            className="absolute bottom-4 right-4 z-[1000] flex items-center justify-center w-10 h-10 rounded-full bg-white dark:bg-slate-800 shadow-md border border-gray-200 dark:border-slate-700 text-gray-600 dark:text-slate-300 hover:text-honey-600 dark:hover:text-honey-400 hover:border-honey-300 dark:hover:border-honey-500/50 transition-colors disabled:opacity-50"
          >
            <Crosshair className={`w-5 h-5 ${locating ? 'animate-pulse' : ''}`} />
          </button>
        </div>

        {/* Footer */}
        <div className="px-4 py-3 border-t border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 shrink-0 flex items-center gap-3">
          <div className="flex-1 text-xs text-gray-500 dark:text-slate-400 font-mono">
            {pin
              ? <span className="text-gray-700 dark:text-slate-300">{fmt(pin.lat)}, {fmt(pin.lng)}</span>
              : <span className="italic">Nije odabrana lokacija</span>
            }
          </div>
          <button onClick={onClose} className="btn-secondary py-2 px-4 text-sm">
            Otkaži
          </button>
          <button
            onClick={() => pin && onConfirm(pin.lat, pin.lng)}
            disabled={!pin}
            className="btn-primary py-2 px-4 text-sm"
          >
            Potvrdi
          </button>
        </div>
      </div>
    </div>
  )
}
