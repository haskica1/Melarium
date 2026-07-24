import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Bot, CloudOff, Download, Pencil, Plus, QrCode, Thermometer, Trash2 } from 'lucide-react'
import { differenceInDays, format, isPast, isToday, parseISO } from 'date-fns'
import { downloadBeehiveQrPdf } from '../../shared/utils/qrPdf'
import {
  useBeehive, useDeleteInspection,
  useTodosByBeehive, useCreateTodo, useUpdateTodo, useDeleteTodo,
  useDietsByBeehive,
  queryKeys,
} from '../../core/services/queries'
import {
  PageSkeleton,
  ErrorMessage,
  EmptyState,
  ConfirmDialog,
  HoneyLevelBadge,
  VitalCard,
} from '../../shared/components'
import { TodoSection } from '../../shared/components/TodoSection'
import { CollapsibleSection } from '../../shared/components/CollapsibleSection'
import DietSection from '../diets/DietSection'
import { InspectionPhotoStrip } from '../inspections/InspectionPhotos'
import { QueenSection } from './QueenSection'
import { HiveYieldCard } from './HiveYieldCard'
import { HiveTreatmentCard } from '../treatments/HiveTreatmentCard'
import { useAuth } from '../../core/context/AuthContext'
import { useOutbox } from '../../core/hooks/useOutbox'
import { DietStatus, HoneyLevel } from '../../core/models'
import type { Inspection } from '../../core/models'
import { usePermissions } from '../../core/hooks/usePermissions'

// QR label side in mm — printable range. Empty/invalid input falls back to the minimum.
const QR_MIN_MM = 10
const QR_MAX_MM = 200
const clampQrMm = (value: string): number =>
  Math.min(QR_MAX_MM, Math.max(QR_MIN_MM, Math.round(Number(value) || QR_MIN_MM)))

// ── Component ─────────────────────────────────────────────────────────────────

export default function BeehiveDetailPage() {
  const { id } = useParams<{ id: string }>()
  const beehiveId = Number(id)

  const { canEditDelete, canManageInspections, canManageHiveTodos, isAssignedToHive } = usePermissions()
  const { data: beehive, isLoading, error } = useBeehive(beehiveId)
  const deleteMutation = useDeleteInspection(beehiveId)

  // Unsent offline inspections for this hive (SPEC-07)
  const { user } = useAuth()
  const pendingOutbox = useOutbox(user?.email).filter(i => i.beehiveId === beehiveId)

  const todoKey = queryKeys.todosByBeehive(beehiveId)
  const { data: todos = [], isLoading: todosLoading } = useTodosByBeehive(beehiveId)
  const { data: diets = [] } = useDietsByBeehive(beehiveId)
  const createTodo = useCreateTodo(todoKey)
  const updateTodo = useUpdateTodo(todoKey)
  const deleteTodo = useDeleteTodo(todoKey)

  const [deleteTarget, setDeleteTarget] = useState<{ id: number } | null>(null)
  const [qrOpen, setQrOpen] = useState(false)
  // Kept as strings so the field can be cleared / retyped freely; clamped to 10–200 mm on blur & export.
  const [qrSize, setQrSize] = useState({ w: '60', h: '60' })

  const handleDelete = async () => {
    if (!deleteTarget) return
    await deleteMutation.mutateAsync(deleteTarget.id)
    setDeleteTarget(null)
  }

  if (isLoading) return <PageSkeleton />
  if (error) return <ErrorMessage message={error.message} />
  if (!beehive) return null

  const hasQr = !!beehive.uniqueId && !!beehive.qrCodeBase64
  const canManageThisHive = canManageInspections || isAssignedToHive(beehiveId)

  // ── Derived "vitals" (computed from already-loaded data — no extra requests) ──
  const inspections = [...(beehive.inspections ?? [])].sort(
    (a, b) => +new Date(b.date) - +new Date(a.date),
  )
  const latest = inspections[0]
  const openTodos = todos.filter(t => !t.isCompleted)
  const overdueCount = openTodos.filter(t => {
    if (!t.dueDate) return false
    const d = parseISO(t.dueDate)
    return isPast(d) && !isToday(d)
  }).length
  const activeDiets = diets.filter(
    d => d.status === DietStatus.InProgress || d.status === DietStatus.NotStarted,
  ).length
  const lastInspDays = latest ? differenceInDays(new Date(), new Date(latest.date)) : null

  const honeyGradient = !latest
    ? 'from-slate-400 to-slate-500'
    : latest.honeyLevel === HoneyLevel.High
    ? 'from-emerald-400 to-green-600'
    : latest.honeyLevel === HoneyLevel.Medium
    ? 'from-honey-400 to-orange-500'
    : 'from-red-400 to-rose-600'

  const lastInspLabel =
    lastInspDays == null ? 'Nema pregleda'
    : lastInspDays === 0 ? 'Danas'
    : lastInspDays === 1 ? 'Jučer'
    : `Prije ${lastInspDays} dana`

  const addInspectionBtn = (
    <Link to={`/inspections/new?beehiveId=${beehiveId}`} className="btn-primary text-sm">
      <Plus className="w-4 h-4" /> Dodaj pregled
    </Link>
  )

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
                🐝
              </div>
              <div className="min-w-0">
                <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50 truncate">
                  {beehive.name}
                </h1>
                <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
                  {beehive.typeName} · {beehive.materialName}
                </p>
              </div>
            </div>

            <div className="flex gap-2 shrink-0">
              <Link to={`/advisor?beehiveId=${beehiveId}`} className="btn-secondary text-sm" title="Pitaj savjetnika" aria-label="Pitaj savjetnika">
                <Bot className="w-4 h-4" /> <span className="hidden sm:inline">Pitaj savjetnika</span>
              </Link>
              {hasQr && (
                <button onClick={() => setQrOpen(true)} className="btn-secondary text-sm" title="QR kod" aria-label="QR kod">
                  <QrCode className="w-4 h-4" /> <span className="hidden sm:inline">QR kod</span>
                </button>
              )}
              {canEditDelete && (
                <Link to={`/beehives/${beehiveId}/edit`} className="btn-secondary text-sm" title="Uredi" aria-label="Uredi">
                  <Pencil className="w-4 h-4" /> <span className="hidden sm:inline">Uredi</span>
                </Link>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* ── Vitals strip ──────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
        <VitalCard
          icon="🍯"
          label="Nivo meda"
          value={latest ? latest.honeyLevelName : '—'}
          sub={latest ? format(new Date(latest.date), 'dd MMM yyyy') : 'Nema podataka'}
          gradient={honeyGradient}
        />
        <VitalCard
          icon="🌡️"
          label="Temperatura"
          value={latest?.temperature != null ? `${latest.temperature}°C` : '—'}
          sub={lastInspLabel}
          gradient="from-sky-400 to-blue-600"
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
          icon="🌿"
          label="Aktivna prehrana"
          value={String(activeDiets)}
          sub={`${diets.length} ukupno`}
          gradient="from-emerald-400 to-teal-600"
        />
      </div>

      {/* ── Unsent offline inspections hint (SPEC-07) ─────────────────────────── */}
      {pendingOutbox.length > 0 && (
        <Link
          to="/outbox"
          className="flex items-center gap-2.5 px-4 py-3 rounded-2xl border border-amber-200 dark:border-amber-500/30
            bg-amber-50 dark:bg-amber-500/10 text-sm font-medium text-amber-800 dark:text-amber-300
            hover:bg-amber-100 dark:hover:bg-amber-500/20 transition-colors"
        >
          <CloudOff className="w-4 h-4 shrink-0" />
          {pendingOutbox.length === 1
            ? '1 pregled ove košnice čeka slanje.'
            : `${pendingOutbox.length} pregleda ove košnice čeka slanje.`}
          <span className="ml-auto underline shrink-0">Otvori</span>
        </Link>
      )}

      {/* ── Bento grid ────────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">

        {/* Main column */}
        <div className="lg:col-span-7 xl:col-span-8 space-y-6">
          {/* Inspection history (timeline) */}
          <CollapsibleSection
            title="Historija pregleda"
            icon="📋"
            count={beehive.inspectionCount}
            action={canManageThisHive ? addInspectionBtn : undefined}
          >
            {!inspections.length ? (
              <EmptyState
                title="Nema pregleda"
                description="Zabilježite vaš prvi pregled za ovu košnicu."
                action={
                  <Link to={`/inspections/new?beehiveId=${beehiveId}`} className="btn-primary text-sm">
                    <Plus className="w-4 h-4" /> Zabilježi pregled
                  </Link>
                }
              />
            ) : (
              <div className="relative pl-1">
                {inspections.map((inspection, i) => (
                  <InspectionTimelineItem
                    key={inspection.id}
                    inspection={inspection}
                    beehiveId={beehiveId}
                    canManage={canManageThisHive}
                    isLast={i === inspections.length - 1}
                    onDelete={() => setDeleteTarget({ id: inspection.id })}
                  />
                ))}
              </div>
            )}
          </CollapsibleSection>

          {/* To-do list */}
          <TodoSection
            todos={todos}
            isLoading={todosLoading}
            beehiveId={beehiveId}
            canCreate={canManageHiveTodos || isAssignedToHive(beehiveId)}
            canManage={canManageHiveTodos || isAssignedToHive(beehiveId)}
            onCreate={p => createTodo.mutateAsync(p)}
            onUpdate={(id, p) => updateTodo.mutateAsync({ id, payload: p })}
            onDelete={id => deleteTodo.mutateAsync(id)}
            isMutating={createTodo.isPending || updateTodo.isPending || deleteTodo.isPending}
          />

          {/* Feeding programmes */}
          <DietSection beehiveId={beehiveId} />
        </div>

        {/* Sidebar */}
        <div className="lg:col-span-5 xl:col-span-4 space-y-6">
          {/* Hive details — always visible */}
          <div className="card">
            <div className="flex items-center gap-2 mb-4">
              <span className="text-lg leading-none">🐝</span>
              <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Detalji košnice</h2>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <DetailTile icon="🐝" label="Tip" value={beehive.typeName} />
              <DetailTile icon="🪵" label="Materijal" value={beehive.materialName} />
              <DetailTile
                icon="📅"
                label="Osnovan"
                value={format(new Date(beehive.dateCreated), 'dd MMM yyyy')}
              />
              <DetailTile icon="📋" label="Pregledi" value={String(beehive.inspectionCount)} />
            </div>

            {beehive.notes && (
              <p className="mt-4 pt-4 border-t border-honey-100 dark:border-slate-800 text-sm text-gray-600 dark:text-slate-300 italic">
                📝 {beehive.notes}
              </p>
            )}
            {beehive.uniqueId && (
              <p className="mt-3 pt-3 border-t border-honey-100 dark:border-slate-800 text-xs text-gray-400 dark:text-slate-500 font-mono flex items-center gap-1.5">
                <QrCode className="w-3.5 h-3.5 shrink-0 text-honey-400" />
                {beehive.uniqueId}
              </p>
            )}
            {beehive.createdByName && (
              <p className="mt-3 pt-3 border-t border-honey-100 dark:border-slate-800 text-xs text-gray-500 dark:text-slate-400 flex items-center gap-1.5">
                👤 Kreirao {beehive.createdByName}
              </p>
            )}
          </div>

          {/* Queen (matica) */}
          <QueenSection beehiveId={beehiveId} canManage={canManageThisHive} />

          {/* Honey yield (prinos) */}
          <HiveYieldCard beehiveId={beehiveId} />

          {/* Treatments (tretmani) */}
          <HiveTreatmentCard beehiveId={beehiveId} />

        </div>
      </div>

      {/* Delete inspection confirmation */}
      <ConfirmDialog
        isOpen={!!deleteTarget}
        title="Obriši pregled"
        message="Jeste li sigurni da želite obrisati ovaj zapis pregleda? Ova radnja se ne može poništiti."
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
        isLoading={deleteMutation.isPending}
      />

      {/* QR code modal */}
      {qrOpen && hasQr && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setQrOpen(false)} />
          <div className="relative bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl p-6 max-w-sm w-full animate-fade-in">
            <div className="text-center mb-3">
              <h2 className="font-display text-xl font-bold text-gray-800 dark:text-slate-100">{beehive.name}</h2>
              <p className="text-xs text-gray-400 dark:text-slate-500 font-mono mt-1">{beehive.uniqueId}</p>
            </div>

            <img
              src={`data:image/png;base64,${beehive.qrCodeBase64}`}
              alt={`QR code for ${beehive.name}`}
              className="w-full max-w-[220px] mx-auto block rounded-lg border border-gray-100 bg-white p-1"
            />

            <div className="mt-4 grid grid-cols-2 gap-3">
              <div>
                <label className="form-label text-xs">Širina (mm)</label>
                <input
                  type="number"
                  min={QR_MIN_MM}
                  max={QR_MAX_MM}
                  className="form-input text-sm"
                  value={qrSize.w}
                  onChange={e => setQrSize(s => ({ ...s, w: e.target.value }))}
                  onBlur={e => setQrSize(s => ({ ...s, w: String(clampQrMm(e.target.value)) }))}
                />
              </div>
              <div>
                <label className="form-label text-xs">Visina (mm)</label>
                <input
                  type="number"
                  min={QR_MIN_MM}
                  max={QR_MAX_MM}
                  className="form-input text-sm"
                  value={qrSize.h}
                  onChange={e => setQrSize(s => ({ ...s, h: e.target.value }))}
                  onBlur={e => setQrSize(s => ({ ...s, h: String(clampQrMm(e.target.value)) }))}
                />
              </div>
            </div>

            <div className="flex gap-3 mt-4">
              <button
                onClick={() => setQrOpen(false)}
                className="btn-secondary flex-1 text-sm py-2 px-3"
              >
                Zatvori
              </button>
              <button
                onClick={() => downloadBeehiveQrPdf(beehive, { w: clampQrMm(qrSize.w), h: clampQrMm(qrSize.h) })}
                className="btn-primary flex-1 text-sm py-2 px-3"
              >
                <Download className="w-4 h-4" /> Preuzmi PDF
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

// ── Hive detail tile ───────────────────────────────────────────────────────────

function DetailTile({ icon, label, value }: { icon: string; label: string; value: string }) {
  return (
    <div className="rounded-xl bg-honey-50/70 dark:bg-slate-800/50 border border-honey-100/60 dark:border-slate-800 p-3">
      <div className="text-lg leading-none">{icon}</div>
      <div className="text-[11px] text-gray-500 dark:text-slate-400 mt-1.5">{label}</div>
      <div className="text-sm font-semibold text-gray-800 dark:text-slate-100 truncate">{value}</div>
    </div>
  )
}

// ── Inspection timeline item ────────────────────────────────────────────────────

function InspectionTimelineItem({ inspection, beehiveId, canManage, isLast, onDelete }: {
  inspection: Inspection
  beehiveId: number
  canManage: boolean
  isLast: boolean
  onDelete: () => void
}) {
  const dotColor =
    inspection.honeyLevel === HoneyLevel.High   ? 'bg-emerald-500'
    : inspection.honeyLevel === HoneyLevel.Medium ? 'bg-honey-500'
    : inspection.honeyLevel === HoneyLevel.Low    ? 'bg-red-500'
    : 'bg-slate-400'

  return (
    <div className="flex gap-3">
      {/* Timeline gutter */}
      <div className="flex flex-col items-center pt-2">
        <span className={`w-3.5 h-3.5 rounded-full ring-4 ring-white dark:ring-slate-900 shrink-0 ${dotColor}`} />
        {!isLast && <span className="w-0.5 flex-1 bg-honey-100 dark:bg-slate-700 mt-1" />}
      </div>

      {/* Content */}
      <div className={`flex-1 ${isLast ? '' : 'pb-5'}`}>
        <div className="rounded-xl border border-honey-100 dark:border-slate-800 bg-honey-50/40 dark:bg-slate-800/40 p-4 animate-slide-up">
          {/* Header */}
          <div className="flex items-start justify-between gap-3 mb-2">
            <div>
              <p className="font-semibold text-gray-800 dark:text-slate-100">
                {format(new Date(inspection.date), 'EEEE, dd MMMM yyyy')}
              </p>
              <div className="flex flex-wrap items-center gap-2 mt-1.5">
                <HoneyLevelBadge level={inspection.honeyLevel} />
                {inspection.temperature != null && (
                  <span className="badge bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300 flex items-center gap-1">
                    <Thermometer className="w-3 h-3" />
                    {inspection.temperature}°C
                  </span>
                )}
              </div>
            </div>
            {canManage && (
              <div className="flex gap-1 shrink-0">
                <Link
                  to={`/inspections/${inspection.id}/edit?beehiveId=${beehiveId}`}
                  className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-700 transition-colors"
                >
                  <Pencil className="w-3.5 h-3.5" />
                </Link>
                <button
                  onClick={onDelete}
                  className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            )}
          </div>

          {/* Details */}
          {inspection.broodStatus && (
            <div className="flex gap-2 text-sm text-gray-600 dark:text-slate-300 mb-1">
              <span className="shrink-0 text-base">🐛</span>
              <span><strong>Leglo:</strong> {inspection.broodStatus}</span>
            </div>
          )}
          {inspection.notes && (
            <div className="flex gap-2 text-sm text-gray-500 dark:text-slate-400 mt-2 pt-2 border-t border-gray-100 dark:border-slate-700">
              <span className="shrink-0">📝</span>
              <span className="italic">{inspection.notes}</span>
            </div>
          )}

          {/* Photos (SPEC-05) — renders nothing when the inspection has none */}
          <InspectionPhotoStrip inspectionId={inspection.id} canManage={canManage} />
        </div>
      </div>
    </div>
  )
}
