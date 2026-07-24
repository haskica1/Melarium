import { Link } from 'react-router-dom'
import { format } from 'date-fns'
import { Pill } from 'lucide-react'
import { useTreatments } from '../../core/services/treatmentQueries'
import { TreatmentStatus } from '../../core/models'

/** Compact treatment-status card for the beehive detail sidebar (SPEC-08). */
export function HiveTreatmentCard({ beehiveId }: { beehiveId: number }) {
  const { data: treatments = [], isLoading } = useTreatments({ beehiveId })

  if (isLoading) return null

  const latest = [...treatments].sort((a, b) => b.startDate.localeCompare(a.startDate))[0]

  return (
    <div className="card">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Pill className="w-5 h-5 text-honey-500" />
          <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Tretmani</h2>
        </div>
        {treatments.length > 0 && (
          <Link
            to={`/treatments?beehiveId=${beehiveId}`}
            className="text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium"
          >
            Historija ({treatments.length})
          </Link>
        )}
      </div>

      {!latest ? (
        <p className="text-sm text-gray-400 dark:text-slate-500">Još nema tretmana za ovu košnicu.</p>
      ) : (
        <div className="space-y-2">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="font-semibold text-gray-900 dark:text-slate-100">{latest.productName}</span>
            <StatusBadge
              status={latest.status}
              karencaUntil={latest.karencaUntil}
            />
          </div>
          <p className="text-sm text-gray-500 dark:text-slate-400">
            {format(new Date(latest.startDate), 'dd.MM.yyyy')} · {latest.activeSubstanceName} · {latest.methodName}
          </p>
        </div>
      )}
    </div>
  )
}

function StatusBadge({ status, karencaUntil }: { status: TreatmentStatus; karencaUntil: string }) {
  if (status === TreatmentStatus.InProgress) {
    return (
      <span className="text-xs rounded-full px-2 py-0.5 bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300">
        Tretman u toku
      </span>
    )
  }
  if (status === TreatmentStatus.Karenca) {
    return (
      <span className="text-xs rounded-full px-2 py-0.5 bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300">
        Karenca do {format(new Date(karencaUntil), 'dd.MM.yyyy')}
      </span>
    )
  }
  return (
    <span className="text-xs rounded-full px-2 py-0.5 bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300">
      Završen
    </span>
  )
}
