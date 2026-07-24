import { useState } from 'react'
import { Crown, History, Pencil, Plus, RefreshCw, Trash2 } from 'lucide-react'
import { format } from 'date-fns'
import {
  useCreateQueen,
  useDeleteQueen,
  useQueensByBeehive,
  useUpdateQueen,
} from '../../core/services/queries'
import { ConfirmDialog } from '../../shared/components'
import {
  QueenMarkColor,
  QueenMarkColorLabels,
  QueenOrigin,
  QueenOriginLabels,
  QueenStatus,
  QueenStatusLabels,
} from '../../core/models'
import type { Queen } from '../../core/models'
import { queenColorDotClass, queenColorForYear, queenSeason } from '../../shared/utils/queen'

const statusBadgeClass: Record<QueenStatus, string> = {
  [QueenStatus.Active]:   'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  [QueenStatus.Replaced]: 'bg-slate-100 text-slate-600 dark:bg-slate-500/15 dark:text-slate-300',
  [QueenStatus.Died]:     'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300',
  [QueenStatus.Missing]:  'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
}

interface QueenFormState {
  year: number
  markColor: QueenMarkColor
  colorTouched: boolean
  isMarked: boolean
  isClipped: boolean
  origin: QueenOrigin
  introducedDate: string
  status: QueenStatus
  endDate: string
  notes: string
}

const emptyForm = (): QueenFormState => {
  const year = new Date().getFullYear()
  return {
    year,
    markColor: queenColorForYear(year),
    colorTouched: false,
    isMarked: false,
    isClipped: false,
    origin: QueenOrigin.Purchased,
    introducedDate: format(new Date(), 'yyyy-MM-dd'),
    status: QueenStatus.Active,
    endDate: '',
    notes: '',
  }
}

const formFromQueen = (q: Queen): QueenFormState => ({
  year: q.year,
  markColor: q.markColor,
  colorTouched: true,
  isMarked: q.isMarked,
  isClipped: q.isClipped,
  origin: q.origin,
  introducedDate: q.introducedDate.slice(0, 10),
  status: q.status,
  endDate: q.endDate ? q.endDate.slice(0, 10) : '',
  notes: q.notes ?? '',
})

/** Extracts a readable message from Problem Details (`errors` map) or a validation dictionary. */
const extractApiError = (err: any): string => {
  const data = err?.response?.data
  if (typeof data?.detail === 'string') return data.detail
  const dict = data?.errors ?? data
  if (dict && typeof dict === 'object') {
    const first = Object.values(dict).find(v => Array.isArray(v) && v.length > 0) as string[] | undefined
    if (first) return first[0]
  }
  return 'Greška pri spremanju. Pokušajte ponovo.'
}

// ── Component ─────────────────────────────────────────────────────────────────

export function QueenSection({ beehiveId, canManage }: { beehiveId: number; canManage: boolean }) {
  const { data: queens = [], isLoading } = useQueensByBeehive(beehiveId)
  const createQueen = useCreateQueen(beehiveId)
  const updateQueen = useUpdateQueen(beehiveId)
  const deleteQueen = useDeleteQueen(beehiveId)

  const [editTarget, setEditTarget] = useState<Queen | 'new' | null>(null)
  const [form, setForm] = useState<QueenFormState>(emptyForm)
  const [formError, setFormError] = useState<string | null>(null)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<Queen | null>(null)

  const active = queens.find(q => q.status === QueenStatus.Active)
  const season = active ? queenSeason(active.year) : 0
  const isSaving = createQueen.isPending || updateQueen.isPending

  const openCreate = () => {
    setForm(emptyForm())
    setFormError(null)
    setEditTarget('new')
  }

  const openEdit = (q: Queen) => {
    setForm(formFromQueen(q))
    setFormError(null)
    setEditTarget(q)
  }

  const handleSubmit = async () => {
    setFormError(null)
    try {
      if (editTarget === 'new') {
        await createQueen.mutateAsync({
          year: form.year,
          markColor: form.markColor,
          isMarked: form.isMarked,
          isClipped: form.isClipped,
          origin: form.origin,
          introducedDate: form.introducedDate,
          notes: form.notes.trim() || undefined,
        })
      } else if (editTarget) {
        await updateQueen.mutateAsync({
          id: editTarget.id,
          payload: {
            year: form.year,
            markColor: form.markColor,
            isMarked: form.isMarked,
            isClipped: form.isClipped,
            origin: form.origin,
            status: form.status,
            introducedDate: form.introducedDate,
            endDate: form.status !== QueenStatus.Active && form.endDate ? form.endDate : null,
            notes: form.notes.trim() || undefined,
          },
        })
      }
      setEditTarget(null)
    } catch (err) {
      setFormError(extractApiError(err))
    }
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    await deleteQueen.mutateAsync(deleteTarget.id)
    setDeleteTarget(null)
  }

  return (
    <div className="card">
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Crown className="w-5 h-5 text-honey-500" />
          <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Matica</h2>
        </div>
        {queens.length > 0 && (
          <button
            onClick={() => setHistoryOpen(true)}
            className="inline-flex items-center gap-1.5 text-xs text-gray-500 dark:text-slate-400 hover:text-honey-600 dark:hover:text-honey-400 transition-colors"
          >
            <History className="w-3.5 h-3.5" /> Historija ({queens.length})
          </button>
        )}
      </div>

      {isLoading ? (
        <div className="animate-pulse space-y-2">
          <div className="h-4 bg-honey-50 dark:bg-slate-800 rounded w-2/3" />
          <div className="h-3 bg-honey-50 dark:bg-slate-800 rounded w-1/2" />
        </div>
      ) : active ? (
        <>
          <div className="flex items-start gap-3">
            <span
              title={`${active.markColorName} oznaka`}
              className={`mt-0.5 w-5 h-5 rounded-full shrink-0 shadow ${queenColorDotClass[active.markColor]}`}
            />
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-semibold text-gray-800 dark:text-slate-100">
                  Godište {active.year}.
                </span>
                <span
                  className={`badge ${
                    season >= 3
                      ? 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300'
                      : 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300'
                  }`}
                >
                  {season}. sezona
                </span>
              </div>
              <p className="mt-1 text-sm text-gray-600 dark:text-slate-300">
                {active.originName} · {active.markColorName} oznaka
              </p>
              <div className="mt-2 flex flex-wrap gap-1.5">
                {active.isMarked && (
                  <span className="badge bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300">Označena</span>
                )}
                {active.isClipped && (
                  <span className="badge bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300">Podrezana krila</span>
                )}
              </div>
              <p className="mt-2 text-xs text-gray-400 dark:text-slate-500">
                U košnici od {format(new Date(active.introducedDate), 'dd MMM yyyy')}
              </p>
              {season >= 3 && (
                <p className="mt-1 text-xs text-amber-600 dark:text-amber-400">
                  ⚠️ Matica je u {season}. sezoni — razmisli o zamjeni.
                </p>
              )}
              {active.notes && (
                <p className="mt-2 pt-2 border-t border-honey-100 dark:border-slate-800 text-xs text-gray-500 dark:text-slate-400 italic">
                  📝 {active.notes}
                </p>
              )}
            </div>
          </div>

          {canManage && (
            <div className="flex justify-end gap-2 mt-4">
              <button onClick={() => openEdit(active)} className="btn-secondary text-sm">
                <Pencil className="w-4 h-4" /> Uredi
              </button>
              <button onClick={openCreate} className="btn-secondary text-sm">
                <RefreshCw className="w-4 h-4" /> Zamijeni
              </button>
            </div>
          )}
        </>
      ) : (
        <div className="text-center py-2">
          <p className="text-sm text-gray-500 dark:text-slate-400 mb-3">
            {queens.length > 0 ? 'Košnica trenutno nema aktivnu maticu.' : 'Još nema evidencije o matici.'}
          </p>
          {canManage && (
            <button onClick={openCreate} className="btn-primary text-sm">
              <Plus className="w-4 h-4" /> Dodaj maticu
            </button>
          )}
        </div>
      )}

      {/* ── Add / edit modal ──────────────────────────────────────────────────── */}
      {editTarget !== null && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setEditTarget(null)} />
          <div className="relative bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl p-6 max-w-md w-full max-h-[90vh] overflow-y-auto animate-fade-in">
            <h2 className="font-display text-lg font-bold text-gray-800 dark:text-slate-100 mb-4">
              {editTarget === 'new'
                ? active ? 'Zamijeni maticu' : 'Dodaj maticu'
                : 'Uredi maticu'}
            </h2>

            {editTarget === 'new' && active && (
              <p className="mb-4 text-xs rounded-lg bg-honey-50 dark:bg-slate-800 border border-honey-100 dark:border-slate-700 text-gray-600 dark:text-slate-300 p-3">
                Trenutna matica (godište {active.year}.) bit će automatski označena kao zamijenjena.
              </p>
            )}

            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="form-label text-xs">Godište</label>
                  <input
                    type="number"
                    min={2000}
                    max={new Date().getFullYear()}
                    className="form-input text-sm"
                    value={form.year}
                    onChange={e => {
                      const year = Number(e.target.value)
                      setForm(f => ({
                        ...f,
                        year,
                        markColor: f.colorTouched ? f.markColor : queenColorForYear(year),
                      }))
                    }}
                  />
                </div>
                <div>
                  <label className="form-label text-xs">Boja oznake</label>
                  <div className="flex items-center gap-2">
                    <select
                      className="form-input text-sm flex-1"
                      value={form.markColor}
                      onChange={e =>
                        setForm(f => ({ ...f, markColor: Number(e.target.value), colorTouched: true }))
                      }
                    >
                      {Object.entries(QueenMarkColorLabels).map(([value, label]) => (
                        <option key={value} value={value}>{label}</option>
                      ))}
                    </select>
                    <span className={`w-5 h-5 rounded-full shrink-0 ${queenColorDotClass[form.markColor]}`} />
                  </div>
                </div>
              </div>

              <div>
                <label className="form-label text-xs">Porijeklo</label>
                <select
                  className="form-input text-sm"
                  value={form.origin}
                  onChange={e => setForm(f => ({ ...f, origin: Number(e.target.value) }))}
                >
                  {Object.entries(QueenOriginLabels).map(([value, label]) => (
                    <option key={value} value={value}>{label}</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="form-label text-xs">U košnici od</label>
                <input
                  type="date"
                  className="form-input text-sm"
                  max={format(new Date(), 'yyyy-MM-dd')}
                  value={form.introducedDate}
                  onChange={e => setForm(f => ({ ...f, introducedDate: e.target.value }))}
                />
              </div>

              {editTarget !== 'new' && (
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="form-label text-xs">Status</label>
                    <select
                      className="form-input text-sm"
                      value={form.status}
                      onChange={e => setForm(f => ({ ...f, status: Number(e.target.value) }))}
                    >
                      {Object.entries(QueenStatusLabels).map(([value, label]) => (
                        <option key={value} value={value}>{label}</option>
                      ))}
                    </select>
                  </div>
                  {form.status !== QueenStatus.Active && (
                    <div>
                      <label className="form-label text-xs">Do datuma</label>
                      <input
                        type="date"
                        className="form-input text-sm"
                        min={form.introducedDate}
                        value={form.endDate}
                        onChange={e => setForm(f => ({ ...f, endDate: e.target.value }))}
                      />
                    </div>
                  )}
                </div>
              )}

              <div className="flex gap-4">
                <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-slate-300 cursor-pointer">
                  <input
                    type="checkbox"
                    className="accent-honey-500 w-4 h-4"
                    checked={form.isMarked}
                    onChange={e => setForm(f => ({ ...f, isMarked: e.target.checked }))}
                  />
                  Označena
                </label>
                <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-slate-300 cursor-pointer">
                  <input
                    type="checkbox"
                    className="accent-honey-500 w-4 h-4"
                    checked={form.isClipped}
                    onChange={e => setForm(f => ({ ...f, isClipped: e.target.checked }))}
                  />
                  Podrezana krila
                </label>
              </div>

              <div>
                <label className="form-label text-xs">Napomene</label>
                <textarea
                  className="form-input text-sm"
                  rows={2}
                  maxLength={500}
                  value={form.notes}
                  onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
                />
              </div>

              {formError && (
                <p className="text-sm text-red-600 dark:text-red-400">{formError}</p>
              )}

              <div className="flex gap-3 pt-1">
                <button onClick={() => setEditTarget(null)} className="btn-secondary flex-1 text-sm py-2">
                  Odustani
                </button>
                <button
                  onClick={handleSubmit}
                  disabled={isSaving || !form.introducedDate || !form.year}
                  className="btn-primary flex-1 text-sm py-2 disabled:opacity-50"
                >
                  {isSaving ? 'Spremanje…' : 'Sačuvaj'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ── History modal ─────────────────────────────────────────────────────── */}
      {historyOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setHistoryOpen(false)} />
          <div className="relative bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl p-6 max-w-md w-full max-h-[85vh] overflow-y-auto animate-fade-in">
            <div className="flex items-center gap-2 mb-4">
              <History className="w-5 h-5 text-honey-500" />
              <h2 className="font-display text-lg font-bold text-gray-800 dark:text-slate-100">Historija matica</h2>
            </div>

            <div className="space-y-3">
              {queens.map(q => (
                <div
                  key={q.id}
                  className="rounded-xl border border-honey-100 dark:border-slate-800 bg-honey-50/40 dark:bg-slate-800/40 p-3"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-center gap-2.5 min-w-0">
                      <span className={`w-4 h-4 rounded-full shrink-0 ${queenColorDotClass[q.markColor]}`} />
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-semibold text-sm text-gray-800 dark:text-slate-100">
                            Godište {q.year}.
                          </span>
                          <span className={`badge ${statusBadgeClass[q.status]}`}>{q.statusName}</span>
                        </div>
                        <p className="text-xs text-gray-500 dark:text-slate-400 mt-0.5">
                          {q.originName} · {format(new Date(q.introducedDate), 'dd.MM.yyyy')}
                          {' – '}
                          {q.endDate ? format(new Date(q.endDate), 'dd.MM.yyyy') : 'danas'}
                        </p>
                        {q.notes && (
                          <p className="text-xs text-gray-400 dark:text-slate-500 italic mt-1">📝 {q.notes}</p>
                        )}
                      </div>
                    </div>
                    <div className="flex gap-1 shrink-0">
                      {canManage && (
                        <>
                          <button
                            onClick={() => { setHistoryOpen(false); openEdit(q) }}
                            title="Uredi"
                            className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-700 transition-colors"
                          >
                            <Pencil className="w-3.5 h-3.5" />
                          </button>
                          <button
                            onClick={() => { setHistoryOpen(false); setDeleteTarget(q) }}
                            title="Obriši"
                            className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        </>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <button onClick={() => setHistoryOpen(false)} className="btn-secondary w-full text-sm py-2 mt-4">
              Zatvori
            </button>
          </div>
        </div>
      )}

      {/* Delete confirmation */}
      <ConfirmDialog
        isOpen={!!deleteTarget}
        title="Obriši maticu"
        message="Jeste li sigurni da želite obrisati ovaj zapis matice? Ova radnja se ne može poništiti."
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
        isLoading={deleteQueen.isPending}
      />
    </div>
  )
}
