import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Download, Pencil, Plus, Trash2, MapPin, Wind, Droplets, Thermometer, Search } from 'lucide-react'
import { format, parseISO, isPast, isToday } from 'date-fns'
import { bs } from 'date-fns/locale'
import { downloadBeehivesQrPdf } from '../../shared/utils/qrPdf'
import { beehiveService } from '../../core/services/beehiveService'
import {
  useApiary, useApiaryWeather, useDeleteBeehive,
  useTodosByApiary, useCreateTodo, useUpdateTodo, useDeleteTodo,
  queryKeys,
} from '../../core/services/queries'
import {
  LoadingSpinner,
  PageSkeleton,
  ErrorMessage,
  EmptyState,
  ConfirmDialog,
  VitalCard,
} from '../../shared/components'
import { TodoSection } from '../../shared/components/TodoSection'
import { CollapsibleSection } from '../../shared/components/CollapsibleSection'
import { ApiaryHarvestsSection } from '../harvests/ApiaryHarvestsSection'
import { ApiaryTreatmentsSection } from '../treatments/ApiaryTreatmentsSection'
import { ApiaryMovesSection } from '../pastures/ApiaryMovesSection'
import { useApiaryMoves } from '../../core/services/pastureQueries'
import type { Beehive, DailyWeather } from '../../core/models'
import { usePermissions } from '../../core/hooks/usePermissions'

// ── WMO weather code → emoji + label ─────────────────────────────────────────

function wmoToIcon(code?: number): string {
  if (code == null) return '🌡️'
  if (code === 0)                   return '☀️'
  if (code === 1)                   return '🌤️'
  if (code === 2)                   return '⛅'
  if (code === 3)                   return '☁️'
  if (code === 45 || code === 48)   return '🌫️'
  if (code >= 51 && code <= 55)     return '🌦️'
  if (code >= 61 && code <= 65)     return '🌧️'
  if (code >= 71 && code <= 77)     return '🌨️'
  if (code >= 80 && code <= 82)     return '🌧️'
  if (code >= 85 && code <= 86)     return '🌨️'
  if (code >= 95 && code <= 99)     return '⛈️'
  return '🌡️'
}

function wmoToLabel(code?: number): string {
  if (code == null) return 'Nepoznato'
  if (code === 0)                   return 'Vedro nebo'
  if (code === 1)                   return 'Uglavnom vedro'
  if (code === 2)                   return 'Djelimično oblačno'
  if (code === 3)                   return 'Oblačno'
  if (code === 45 || code === 48)   return 'Magla'
  if (code >= 51 && code <= 55)     return 'Rosulja'
  if (code >= 61 && code <= 65)     return 'Kiša'
  if (code >= 71 && code <= 77)     return 'Snijeg'
  if (code >= 80 && code <= 82)     return 'Pljuskovi kiše'
  if (code >= 85 && code <= 86)     return 'Pljuskovi snijega'
  if (code >= 95 && code <= 99)     return 'Grmljavina'
  return 'Nepoznato'
}

// ── Weather card for a single day ─────────────────────────────────────────────

function DayCard({ day, isToday }: { day: DailyWeather; isToday: boolean }) {
  const date = parseISO(day.date)
  return (
    <div className={`rounded-xl p-3 text-center flex flex-col gap-1 border transition-all ${
      isToday
        ? 'bg-honey-50 border-honey-300 shadow-honey dark:bg-honey-500/10 dark:border-honey-500/40 dark:shadow-none'
        : 'bg-white border-gray-100 hover:border-honey-200 dark:bg-slate-800 dark:border-slate-700 dark:hover:border-honey-500/40'
    }`}>
      <p className="text-xs font-semibold text-gray-500 dark:text-slate-400 uppercase tracking-wide">
        {isToday ? 'Danas' : format(date, 'EEE', { locale: bs })}
      </p>
      <p className="text-[11px] text-gray-400 dark:text-slate-500">{format(date, 'MMM d', { locale: bs })}</p>
      <span className="text-3xl my-1" title={wmoToLabel(day.weatherCode)}>
        {wmoToIcon(day.weatherCode)}
      </span>
      <p className="text-[11px] text-gray-500 dark:text-slate-400 leading-tight">{wmoToLabel(day.weatherCode)}</p>
      <div className="flex justify-center gap-2 mt-1">
        <span className="text-sm font-bold text-red-500">
          {day.maxTemp != null ? `${Math.round(day.maxTemp)}°` : '–'}
        </span>
        <span className="text-sm text-blue-400">
          {day.minTemp != null ? `${Math.round(day.minTemp)}°` : '–'}
        </span>
      </div>
      {day.precipitationProbability != null && (
        <p className="text-[10px] text-blue-500 flex items-center justify-center gap-0.5 mt-0.5">
          <Droplets className="w-2.5 h-2.5" />
          {Math.round(day.precipitationProbability)}%
        </p>
      )}
    </div>
  )
}

// ── Main component ────────────────────────────────────────────────────────────

export default function ApiaryDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const apiaryId = Number(id)

  const { canManageApiaries, canManageHives, canManageApiaryTodos, canEditDelete } = usePermissions()
  const { data: apiary, isLoading, error } = useApiary(apiaryId)
  const { data: weather, isLoading: weatherLoading } = useApiaryWeather(
    apiaryId,
    apiary?.hasLocation ?? false,
  )
  const deleteMutation = useDeleteBeehive(apiaryId)

  const todoKey = queryKeys.todosByApiary(apiaryId)
  const { data: todos = [], isLoading: todosLoading } = useTodosByApiary(apiaryId)
  const createTodo = useCreateTodo(todoKey)
  const updateTodo = useUpdateTodo(todoKey)
  const deleteTodo = useDeleteTodo(todoKey)

  // Current pasture chip (SPEC-10) — same query the "Selidbe" section uses (deduped by key).
  const { data: apiaryMoves = [] } = useApiaryMoves(apiaryId)
  const currentPastureMove = apiaryMoves[0]

  const [deleteTarget, setDeleteTarget] = useState<{ id: number; name: string } | null>(null)
  const [hiveQuery, setHiveQuery] = useState('')
  const [qrAllOpen, setQrAllOpen] = useState(false)
  const [qrAllSize, setQrAllSize] = useState({ w: 60, h: 60 })
  const [qrAllLoading, setQrAllLoading] = useState(false)

  // QR codes are not part of the list payload — fetch them only when the user exports.
  const handleQrExport = async () => {
    if (!apiary) return
    setQrAllLoading(true)
    try {
      const qrCodes = await beehiveService.getQrCodesByApiary(apiaryId)
      downloadBeehivesQrPdf(apiary.name, qrCodes, qrAllSize)
      setQrAllOpen(false)
    } finally {
      setQrAllLoading(false)
    }
  }

  const handleDeleteBeehive = async () => {
    if (!deleteTarget) return
    await deleteMutation.mutateAsync(deleteTarget.id)
    setDeleteTarget(null)
  }

  if (isLoading) return <PageSkeleton />
  if (error) return <ErrorMessage message={error.message} />
  if (!apiary) return null

  // Use local date (not UTC) — Open-Meteo returns dates in the location's timezone
  const today = format(new Date(), 'yyyy-MM-dd')
  const mapUrl = `https://maps.google.com/?q=${apiary.latitude},${apiary.longitude}`

  // ── Derived "vitals" (computed from already-loaded data — no extra requests) ──
  const totalInspections = apiary.beehives?.reduce((s, b) => s + b.inspectionCount, 0) ?? 0
  const openTodos = todos.filter(t => !t.isCompleted)
  const overdueCount = openTodos.filter(t => {
    if (!t.dueDate) return false
    const d = parseISO(t.dueDate)
    return isPast(d) && !isToday(d)
  }).length
  const todayWeather = weather?.daily[0]

  const activeWeatherCode = weather?.currentWeatherCode ?? todayWeather?.weatherCode
  const weatherValue = !apiary.hasLocation
    ? '—'
    : weatherLoading
    ? '…'
    : weather?.currentTemperature != null
    ? `${Math.round(weather.currentTemperature)}°C`
    : todayWeather
    ? `${Math.round(todayWeather.maxTemp ?? 0)}° / ${Math.round(todayWeather.minTemp ?? 0)}°`
    : '—'
  const weatherSub = !apiary.hasLocation
    ? 'Nema lokacije'
    : weatherLoading
    ? 'Učitavanje…'
    : weather?.currentTemperature != null && todayWeather
    ? `${wmoToLabel(activeWeatherCode)} · max ${Math.round(todayWeather.maxTemp ?? 0)}°`
    : todayWeather
    ? wmoToLabel(todayWeather.weatherCode)
    : 'Nije dostupno'
  const weatherIcon = wmoToIcon(activeWeatherCode)

  // ── Beehive search ──
  const beehives = apiary.beehives ?? []
  const hq = hiveQuery.trim().toLowerCase()
  const filteredBeehives = hq
    ? beehives.filter(b =>
        b.name.toLowerCase().includes(hq) ||
        b.typeName.toLowerCase().includes(hq) ||
        b.materialName.toLowerCase().includes(hq))
    : beehives

  return (
    <div className="animate-fade-in space-y-6">

      {/* ── Hero ──────────────────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div className="flex items-center gap-4 min-w-0">
              <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
                🏡
              </div>
              <div className="min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50 truncate">
                    {apiary.name}
                  </h1>
                  {currentPastureMove && (
                    <span
                      className="inline-flex items-center gap-1 text-xs rounded-full px-2 py-0.5 bg-honey-100 text-honey-800 dark:bg-honey-500/15 dark:text-honey-300 shrink-0"
                      title={`Na pašnjaku od ${format(new Date(currentPastureMove.movedAt), 'dd.MM.yyyy')}`}
                    >
                      ⛺ {currentPastureMove.toPastureName} · od {format(new Date(currentPastureMove.movedAt), 'dd.MM.')}
                    </span>
                  )}
                </div>
                {apiary.description && (
                  <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400 line-clamp-2">
                    {apiary.description}
                  </p>
                )}
              </div>
            </div>

            <div className="flex gap-2 shrink-0">
              {apiary.hasLocation && (
                <a href={mapUrl} target="_blank" rel="noreferrer" className="btn-secondary text-sm">
                  <MapPin className="w-4 h-4" /> Karta
                </a>
              )}
              {canManageApiaries && (
                <Link to={`/apiaries/${apiaryId}/edit`} className="btn-secondary text-sm">
                  <Pencil className="w-4 h-4" /> Uredi
                </Link>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* ── Vitals strip ──────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
        <VitalCard
          icon="🐝"
          label="Košnice"
          value={String(apiary.beehiveCount)}
          sub="u ovom pčelinjaku"
          gradient="from-honey-400 to-honey-600"
        />
        <VitalCard
          icon="📋"
          label="Pregledi"
          value={String(totalInspections)}
          sub="ukupno"
          gradient="from-amber-400 to-orange-500"
        />
        <VitalCard
          icon="✅"
          label="Otvoreni zadaci"
          value={String(openTodos.length)}
          sub={overdueCount > 0 ? `${overdueCount} kasni` : 'U redu'}
          subAlert={overdueCount > 0}
          gradient="from-violet-400 to-indigo-600"
        />
        <VitalCard
          icon={weatherIcon}
          label="Danas"
          value={weatherValue}
          sub={weatherSub}
          gradient="from-sky-400 to-blue-600"
        />
      </div>

      {/* ── Bento grid ────────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">

        {/* Main column */}
        <div className="lg:col-span-7 xl:col-span-8 space-y-6">
          {/* Beehives */}
          <CollapsibleSection
            title="Košnice"
            icon="🏠"
            count={apiary.beehiveCount}
            action={
              (beehives.length > 0 || canManageHives) ? (
                <div className="flex items-center gap-2">
                  {beehives.length > 0 && (
                    <button onClick={() => setQrAllOpen(true)} className="btn-secondary text-sm">
                      <Download className="w-4 h-4" /> QR kodovi
                    </button>
                  )}
                  {canManageHives && (
                    <Link to={`/beehives/new?apiaryId=${apiaryId}`} className="btn-primary text-sm">
                      <Plus className="w-4 h-4" /> Dodaj košnicu
                    </Link>
                  )}
                </div>
              ) : undefined
            }
          >
            {beehives.length === 0 ? (
              <EmptyState
                title="Nema košnica"
                description="Dodajte vašu prvu košnicu u ovaj pčelinjak."
                action={
                  canManageHives ? (
                    <Link to={`/beehives/new?apiaryId=${apiaryId}`} className="btn-primary text-sm">
                      <Plus className="w-4 h-4" /> Dodaj košnicu
                    </Link>
                  ) : undefined
                }
              />
            ) : (
              <>
                {beehives.length > 1 && (
                  <div className="relative mb-3">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-slate-500 pointer-events-none" />
                    <input
                      value={hiveQuery}
                      onChange={e => setHiveQuery(e.target.value)}
                      placeholder="Pretraži košnice…"
                      className="form-input pl-9 py-2 text-sm w-full"
                    />
                  </div>
                )}
                {filteredBeehives.length === 0 ? (
                  <div className="text-center py-8">
                    <Search className="w-7 h-7 text-honey-300 dark:text-honey-500/40 mx-auto mb-2" />
                    <p className="text-sm text-gray-500 dark:text-slate-400">Nema košnica koje odgovaraju &quot;{hiveQuery}&quot;.</p>
                  </div>
                ) : (
                  <div className="grid gap-3 sm:grid-cols-2">
                    {filteredBeehives.map((beehive: Beehive) => (
                  <div
                    key={beehive.id}
                    className="card hover:shadow-honey hover:-translate-y-0.5 transition-all duration-200 group cursor-pointer"
                    onClick={() => navigate(`/beehives/${beehive.id}`)}
                  >
                    <div className="flex items-start gap-3">
                      <span className="text-2xl shrink-0 mt-0.5">🏠</span>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-start justify-between gap-2">
                          <h3 className="font-semibold text-gray-800 dark:text-slate-100 truncate group-hover:text-honey-700 dark:group-hover:text-honey-400 transition-colors">
                            {beehive.name}
                          </h3>
                          {canEditDelete && (
                            <div className="flex gap-1 shrink-0" onClick={e => e.stopPropagation()}>
                              <Link
                                to={`/beehives/${beehive.id}/edit`}
                                className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                              >
                                <Pencil className="w-3.5 h-3.5" />
                              </Link>
                              <button
                                onClick={() => setDeleteTarget({ id: beehive.id, name: beehive.name })}
                                className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            </div>
                          )}
                        </div>
                        <div className="flex flex-wrap gap-1.5 mt-1.5">
                          <span className="badge bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300">{beehive.typeName}</span>
                          <span className="badge bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300">{beehive.materialName}</span>
                        </div>
                        <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                          📋 {beehive.inspectionCount} {beehive.inspectionCount === 1 ? 'pregled' : 'pregleda'}
                        </p>
                      </div>
                    </div>
                  </div>
                    ))}
                  </div>
                )}
              </>
            )}
          </CollapsibleSection>

          {/* Weather forecast */}
          <CollapsibleSection
            title="7-dnevna vremenska prognoza"
            icon="🌤️"
            action={
              apiary.hasLocation
                ? <a href={mapUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium"><MapPin className="w-3 h-3" />Vidi na mapi</a>
                : undefined
            }
          >
            {!apiary.hasLocation ? (
              <div className="text-center py-6 border-dashed border-2 border-gray-200 dark:border-slate-700 rounded-xl">
                <MapPin className="w-8 h-8 text-gray-300 dark:text-slate-600 mx-auto mb-2" />
                <p className="text-gray-500 dark:text-slate-400 text-sm">Nije postavljena lokacija za ovaj pčelinjak.</p>
                <Link to={`/apiaries/${apiaryId}/edit`} className="inline-flex items-center gap-1 mt-3 text-sm text-honey-600 dark:text-honey-400 hover:underline">
                  <Pencil className="w-3.5 h-3.5" /> Dodaj lokaciju
                </Link>
              </div>
            ) : weatherLoading ? (
              <LoadingSpinner message="Preuzimanje prognoze…" />
            ) : weather ? (
              <>
                <div className="overflow-x-auto -mx-1 px-1">
                  <div className="grid grid-cols-7 gap-2 min-w-[480px]">
                    {weather.daily.map((day) => (
                      <DayCard key={day.date} day={day} isToday={day.date === today} />
                    ))}
                  </div>
                </div>
                {weather.daily[0] && (
                  <div className="mt-3 bg-honey-50 dark:bg-slate-800 rounded-xl px-4 py-3 flex flex-wrap gap-4 text-sm text-gray-600 dark:text-slate-300">
                    {weather.currentTemperature != null ? (
                      <span className="flex items-center gap-1.5">
                        <Thermometer className="w-4 h-4 text-red-400" />
                        Sada: <strong className="text-red-500">{Math.round(weather.currentTemperature)}°C</strong>
                        {weather.currentApparentTemperature != null && (
                          <span className="text-gray-400 dark:text-slate-500 text-xs">(osjeća se {Math.round(weather.currentApparentTemperature)}°)</span>
                        )}
                        <span className="text-gray-400 dark:text-slate-500 text-xs">
                          · max {Math.round(weather.daily[0].maxTemp ?? 0)}° / min {Math.round(weather.daily[0].minTemp ?? 0)}°
                        </span>
                      </span>
                    ) : (
                      <span className="flex items-center gap-1.5">
                        <Thermometer className="w-4 h-4 text-red-400" />
                        Danas: <strong className="text-red-500">{Math.round(weather.daily[0].maxTemp ?? 0)}°C</strong>
                        {' / '}
                        <strong className="text-blue-400">{Math.round(weather.daily[0].minTemp ?? 0)}°C</strong>
                      </span>
                    )}
                    {weather.daily[0].precipitationProbability != null && (
                      <span className="flex items-center gap-1.5">
                        <Droplets className="w-4 h-4 text-blue-400" />
                        Kiša: <strong>{Math.round(weather.daily[0].precipitationProbability)}%</strong>
                      </span>
                    )}
                    {(weather.currentWindSpeed ?? weather.daily[0].maxWindSpeed) != null && (
                      <span className="flex items-center gap-1.5">
                        <Wind className="w-4 h-4 text-gray-400" />
                        Vjetar: <strong>{Math.round(weather.currentWindSpeed ?? weather.daily[0].maxWindSpeed ?? 0)} km/h</strong>
                      </span>
                    )}
                    {weather.daily[0].sunrise && weather.daily[0].sunset && (
                      <span className="flex items-center gap-1.5 text-amber-500 dark:text-amber-400">
                        🌅 {weather.daily[0].sunrise.slice(11, 16)}
                        <span className="text-gray-400 dark:text-slate-500">/</span>
                        🌇 {weather.daily[0].sunset.slice(11, 16)}
                      </span>
                    )}
                    {weather.daily[0].uvIndexMax != null && (
                      <span className="flex items-center gap-1.5">
                        ☀️ UV: <strong className={
                          weather.daily[0].uvIndexMax >= 8 ? 'text-red-500' :
                          weather.daily[0].uvIndexMax >= 6 ? 'text-orange-500' :
                          weather.daily[0].uvIndexMax >= 3 ? 'text-yellow-500' : 'text-green-500'
                        }>{weather.daily[0].uvIndexMax.toFixed(1)}</strong>
                      </span>
                    )}
                    <span className="ml-auto text-xs text-gray-400 dark:text-slate-500">via Open-Meteo · {weather.timezone}</span>
                  </div>
                )}
              </>
            ) : (
              <p className="text-center py-4 text-gray-400 dark:text-slate-500 text-sm">Vremenski podaci nisu dostupni.</p>
            )}
          </CollapsibleSection>

          {/* To-do list */}
          <TodoSection
            todos={todos}
            isLoading={todosLoading}
            apiaryId={apiaryId}
            canCreate={canManageApiaryTodos}
            canManage={canManageApiaryTodos}
            onCreate={p => createTodo.mutateAsync(p)}
            onUpdate={(id, p) => updateTodo.mutateAsync({ id, payload: p })}
            onDelete={id => deleteTodo.mutateAsync(id)}
            isMutating={createTodo.isPending || updateTodo.isPending || deleteTodo.isPending}
          />

          {/* Harvests (vrcanja) */}
          <ApiaryHarvestsSection apiaryId={apiaryId} />

          {/* Treatments (tretmani) */}
          <ApiaryTreatmentsSection apiaryId={apiaryId} />

          {/* Pasture moves (selidbe) */}
          <ApiaryMovesSection apiaryId={apiaryId} canManage={canManageApiaries} hasHomeLocation={apiary.hasHomeLocation} />
        </div>

        {/* Sidebar */}
        <div className="lg:col-span-5 xl:col-span-4 space-y-6">
          {/* Apiary details — always visible */}
          <div className="card">
            <div className="flex items-center gap-2 mb-4">
              <span className="text-lg leading-none">🏡</span>
              <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Detalji pčelinjaka</h2>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <DetailTile icon="📅" label="Osnovan" value={format(new Date(apiary.createdAt), 'dd MMM yyyy')} />
              <DetailTile icon="🐝" label="Košnice" value={String(apiary.beehiveCount)} />
              <DetailTile icon="📋" label="Pregledi" value={String(totalInspections)} />
              <DetailTile
                icon="📍"
                label="Lokacija"
                value={apiary.hasLocation ? 'Postavljena' : 'Nije postavljena'}
              />
            </div>

            {/* Location detail */}
            <div className="mt-4 pt-4 border-t border-honey-100 dark:border-slate-800">
              {apiary.hasLocation ? (
                <div className="flex items-center gap-2 text-xs">
                  <MapPin className="w-3.5 h-3.5 shrink-0 text-honey-500" />
                  <span className="font-mono text-gray-500 dark:text-slate-400 truncate">
                    {apiary.latitude?.toFixed(5)}, {apiary.longitude?.toFixed(5)}
                  </span>
                  <a href={mapUrl} target="_blank" rel="noreferrer" className="ml-auto shrink-0 text-honey-600 dark:text-honey-400 hover:underline font-medium">
                    Karta
                  </a>
                </div>
              ) : canManageApiaries ? (
                <Link to={`/apiaries/${apiaryId}/edit`} className="flex items-center gap-1.5 text-xs text-honey-600 dark:text-honey-400 hover:underline">
                  <MapPin className="w-3.5 h-3.5" /> Dodaj lokaciju za vremensku prognozu
                </Link>
              ) : (
                <p className="flex items-center gap-1.5 text-xs text-gray-400 dark:text-slate-500">
                  <MapPin className="w-3.5 h-3.5" /> Nema lokacije
                </p>
              )}
            </div>

            {apiary.createdByName && (
              <p className="mt-3 pt-3 border-t border-honey-100 dark:border-slate-800 text-xs text-gray-500 dark:text-slate-400 flex items-center gap-1.5">
                👤 Kreirao {apiary.createdByName}
              </p>
            )}
          </div>

        </div>
      </div>

      <ConfirmDialog
        isOpen={!!deleteTarget}
        title="Obriši košnicu"
        message={`Obrisati "${deleteTarget?.name}"? Svi zapisi o pregledima će također biti uklonjeni.`}
        onConfirm={handleDeleteBeehive}
        onCancel={() => setDeleteTarget(null)}
        isLoading={deleteMutation.isPending}
      />

      {/* QR download all modal */}
      {qrAllOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setQrAllOpen(false)} />
          <div className="relative bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl p-8 max-w-sm w-full animate-fade-in">
            <div className="text-center mb-4">
              <h2 className="font-display text-xl font-bold text-gray-800 dark:text-slate-100">Preuzmi QR kodove</h2>
              <p className="text-xs text-gray-400 dark:text-slate-500 mt-1">{apiary.name}</p>
            </div>

            <p className="text-sm text-gray-500 dark:text-slate-400 mb-4">
              Odaberite dimenzije QR koda u PDF-u (u milimetrima):
            </p>

            <div className="grid grid-cols-2 gap-3 mb-6">
              <div>
                <label className="form-label text-xs">Širina (mm)</label>
                <input
                  type="number"
                  min={20}
                  max={200}
                  className="form-input text-sm"
                  value={qrAllSize.w}
                  onChange={e => setQrAllSize(s => ({ ...s, w: Math.max(20, Math.min(200, Number(e.target.value))) }))}
                />
              </div>
              <div>
                <label className="form-label text-xs">Visina (mm)</label>
                <input
                  type="number"
                  min={20}
                  max={200}
                  className="form-input text-sm"
                  value={qrAllSize.h}
                  onChange={e => setQrAllSize(s => ({ ...s, h: Math.max(20, Math.min(200, Number(e.target.value))) }))}
                />
              </div>
            </div>

            <div className="flex gap-3">
              <button
                onClick={() => setQrAllOpen(false)}
                className="btn-secondary flex-1 text-sm py-2 px-3"
              >
                Odustani
              </button>
              <button
                onClick={handleQrExport}
                disabled={qrAllLoading}
                className="btn-primary flex-1 text-sm py-2 px-3 disabled:opacity-60"
              >
                <Download className="w-4 h-4" /> {qrAllLoading ? 'Pripremam…' : 'Preuzmi PDF'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

// ── Vitals KPI tile ────────────────────────────────────────────────────────────

/* VitalCard now lives in shared/components (with count-up animation). */

// ── Apiary detail tile ─────────────────────────────────────────────────────────

function DetailTile({ icon, label, value }: { icon: string; label: string; value: string }) {
  return (
    <div className="rounded-xl bg-honey-50/70 dark:bg-slate-800/50 border border-honey-100/60 dark:border-slate-800 p-3">
      <div className="text-lg leading-none">{icon}</div>
      <div className="text-[11px] text-gray-500 dark:text-slate-400 mt-1.5">{label}</div>
      <div className="text-sm font-semibold text-gray-800 dark:text-slate-100 truncate">{value}</div>
    </div>
  )
}
