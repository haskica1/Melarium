import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { format } from 'date-fns'
import { FileDown, Loader2, PencilLine, Pill, Plus, Trash2, X } from 'lucide-react'
import { useTreatments, useDeleteTreatment } from '../../core/services/treatmentQueries'
import { TreatmentStatus } from '../../core/models'
import type { Treatment } from '../../core/models'
import { VitalCard, VitalsSkeleton, ConfirmDialog, EmptyState } from '../../shared/components'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useToast } from '../../core/context/ToastContext'
import { useAuth } from '../../core/context/AuthContext'
import { downloadTreatmentRegisterPdf } from '../../shared/utils/treatmentPdf'

const CURRENT_YEAR = new Date().getFullYear()
const YEARS = [CURRENT_YEAR, CURRENT_YEAR - 1, CURRENT_YEAR - 2, CURRENT_YEAR - 3, CURRENT_YEAR - 4]

const STATUS_STYLE: Record<TreatmentStatus, string> = {
  [TreatmentStatus.InProgress]: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  [TreatmentStatus.Karenca]:    'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300',
  [TreatmentStatus.Completed]:  'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300',
}

export default function TreatmentsPage() {
  const navigate = useNavigate()
  const { canEditDelete } = usePermissions()
  const { toast } = useToast()
  const { user } = useAuth()

  const [searchParams, setSearchParams] = useSearchParams()
  const beehiveId = Number(searchParams.get('beehiveId')) || undefined

  const [year, setYear] = useState<number>(CURRENT_YEAR)
  const { data: treatments = [], isLoading } = useTreatments({ year, beehiveId })
  const deleteTreatment = useDeleteTreatment()

  const [confirmTarget, setConfirmTarget] = useState<Treatment | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const [pdfBusy, setPdfBusy] = useState<string | null>(null)

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    setIsDeleting(true)
    try {
      await deleteTreatment.mutateAsync(confirmTarget.id)
      toast.success('Tretman obrisan.')
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'Greška pri brisanju tretmana.')
    } finally {
      setIsDeleting(false)
    }
  }

  const activeKarenca = treatments.filter(t => t.status !== TreatmentStatus.Completed).length

  const grouped = useMemo(() => {
    const map = new Map<string, Treatment[]>()
    for (const t of treatments) {
      const key = t.apiaryName ?? `Pčelinjak #${t.apiaryId}`
      const arr = map.get(key) ?? []
      arr.push(t)
      map.set(key, arr)
    }
    return [...map.entries()].sort((a, b) => a[0].localeCompare(b[0]))
  }, [treatments])

  async function handleDownloadPdf(apiaryName: string, items: Treatment[]) {
    setPdfBusy(apiaryName)
    try {
      await downloadTreatmentRegisterPdf(items, {
        organizationName: user?.organizationName ?? '—',
        apiaryName,
        year,
        ownerName: user ? `${user.firstName} ${user.lastName}` : '—',
      })
    } catch {
      toast.error('Greška pri izradi PDF-a.')
    } finally {
      setPdfBusy(null)
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
            <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
              💊
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Tretmani</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">Zakonska evidencija primijenjenih lijekova (varoa i dr.).</p>
            </div>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <select
              value={year}
              onChange={e => setYear(Number(e.target.value))}
              className="px-3 py-2 rounded-xl border border-honey-200 dark:border-slate-700 bg-white/70 dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 outline-none focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
            >
              {YEARS.map(y => <option key={y} value={y}>{y}.</option>)}
            </select>
            {canEditDelete && (
              <button onClick={() => navigate('/treatments/new')} className="btn-primary text-sm">
                <Plus className="w-4 h-4" /> Dodaj tretman
              </button>
            )}
          </div>
        </div>
      </div>

      {beehiveId && (
        <div className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-honey-50 dark:bg-slate-800/60 border border-honey-200 dark:border-slate-700 text-sm text-gray-700 dark:text-slate-200">
          <Pill className="w-4 h-4 text-honey-600 dark:text-honey-400 shrink-0" />
          Prikazana je historija tretmana jedne košnice.
          <button
            onClick={() => setSearchParams({}, { replace: true })}
            className="ml-auto flex items-center gap-1 text-xs font-medium text-honey-700 dark:text-honey-300 hover:underline"
          >
            <X className="w-3.5 h-3.5" /> Prikaži sve
          </button>
        </div>
      )}

      {isLoading && <VitalsSkeleton />}

      {!isLoading && treatments.length === 0 && (
        <EmptyState
          title="Još nema evidencije tretmana."
          description={`Za ${year}. godinu nema zabilježenih tretmana.`}
          action={canEditDelete ? (
            <button onClick={() => navigate('/treatments/new')} className="btn-primary text-sm">
              <Plus className="w-4 h-4" /> Dodaj tretman
            </button>
          ) : undefined}
        />
      )}

      {!isLoading && treatments.length > 0 && (
        <>
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
            <VitalCard icon="💊" label="Tretmani"    value={String(treatments.length)} sub={`${year}.`}  gradient="from-honey-400 to-honey-600" />
            <VitalCard icon="⏳" label="Aktivni/karenca" value={String(activeKarenca)}  sub="u toku"     gradient="from-amber-400 to-orange-500" />
            <VitalCard icon="🏡" label="Pčelinjaci"  value={String(grouped.length)}    sub="s tretmanom" gradient="from-emerald-400 to-teal-600" />
            <VitalCard icon="📄" label="Registar"    value="PDF"                        sub="po pčelinjaku" gradient="from-violet-400 to-indigo-600" />
          </div>

          {grouped.map(([apiaryName, items]) => (
            <div key={apiaryName} className="space-y-3">
              <div className="flex items-center justify-between px-1">
                <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">{apiaryName}</h2>
                <button
                  onClick={() => handleDownloadPdf(apiaryName, items)}
                  disabled={pdfBusy === apiaryName}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl border border-honey-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-700 transition-colors disabled:opacity-60"
                >
                  {pdfBusy === apiaryName ? <Loader2 className="w-4 h-4 animate-spin" /> : <FileDown className="w-4 h-4" />}
                  Preuzmi evidenciju (PDF)
                </button>
              </div>
              <div className="space-y-3">
                {items.map(t => (
                  <TreatmentCard
                    key={t.id}
                    treatment={t}
                    canEdit={canEditDelete}
                    isDeleting={confirmTarget?.id === t.id && isDeleting}
                    onEdit={() => navigate(`/treatments/${t.id}/edit`)}
                    onDelete={() => setConfirmTarget(t)}
                  />
                ))}
              </div>
            </div>
          ))}
        </>
      )}

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title="Obriši tretman"
        message={confirmTarget
          ? `Obrisati tretman "${confirmTarget.productName}" od ${format(new Date(confirmTarget.startDate), 'dd.MM.yyyy')}? Zakonska obaveza je čuvanje evidencije 5 godina — briši samo greške.`
          : ''}
        confirmLabel="Obriši"
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}

// ── Treatment card ──────────────────────────────────────────────────────────────

interface TreatmentCardProps {
  treatment: Treatment
  canEdit: boolean
  isDeleting: boolean
  onEdit: () => void
  onDelete: () => void
}

function TreatmentCard({ treatment: t, canEdit, isDeleting, onEdit, onDelete }: TreatmentCardProps) {
  const karencaText = t.status === TreatmentStatus.Karenca
    ? `Karenca do ${format(new Date(t.karencaUntil), 'dd.MM.yyyy')}`
    : t.statusName

  return (
    <div className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-5 py-4 flex items-center gap-4 hover:border-honey-200 dark:hover:border-slate-700 transition-colors">
      <div className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 bg-honey-50 text-honey-600 dark:bg-honey-500/15 dark:text-honey-300">
        <Pill className="w-5 h-5" />
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-semibold text-gray-900 dark:text-slate-100">{t.productName}</span>
          <span className="text-xs text-gray-500 dark:text-slate-400 bg-gray-100 dark:bg-slate-800 rounded-full px-2 py-0.5">{t.purposeName}</span>
          <span className={`text-xs rounded-full px-2 py-0.5 ${STATUS_STYLE[t.status]}`}>{karencaText}</span>
        </div>
        <div className="flex items-center gap-3 mt-0.5 text-sm text-gray-500 dark:text-slate-400">
          <span>{format(new Date(t.startDate), 'dd.MM.yyyy')}</span>
          <span>·</span>
          <span>{t.activeSubstanceName}</span>
          <span>·</span>
          <span>{t.hiveCount} {t.hiveCount === 1 ? 'košnica' : 'košnica'}</span>
        </div>
      </div>
      {canEdit && (
        <div className="flex items-center gap-1 shrink-0">
          <button onClick={onEdit} className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors" aria-label="Uredi tretman">
            <PencilLine className="w-4 h-4" />
          </button>
          <button onClick={onDelete} disabled={isDeleting} className="p-2 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-50" aria-label="Obriši tretman">
            {isDeleting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
          </button>
        </div>
      )}
    </div>
  )
}
