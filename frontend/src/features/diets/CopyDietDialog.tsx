import { useEffect, useMemo, useState } from 'react'
import { Copy, Search, X, Loader2, Leaf, Check } from 'lucide-react'
import { useAllBeehives, useApiaries, useCopyDiet } from '../../core/services/queries'
import { useToast } from '../../core/context/ToastContext'
import { LoadingSpinner } from '../../shared/components'
import type { Diet } from '../../core/models'

/** Bosnian noun agreement for "košnica" after a count (accusative, as in "na N košnic…"). */
function hiveWord(n: number): string {
  const m10 = n % 10
  const m100 = n % 100
  if (m10 === 1 && m100 !== 11) return 'košnicu'
  if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return 'košnice'
  return 'košnica'
}

interface HiveOption { id: number; name: string; labelNumber?: string }
interface Group { apiaryId: number; apiaryName: string; hives: HiveOption[] }

/**
 * Lets the user copy an existing diet's programme onto other beehives they can access.
 * The hive list is role-scoped by the server (`/beehives/all`); the backend independently
 * re-checks access for every target, so this picker is a convenience, not the security boundary.
 */
export default function CopyDietDialog({ diet, onClose }: { diet: Diet; onClose: () => void }) {
  const { toast } = useToast()
  const { data: beehives = [], isLoading: loadingHives } = useAllBeehives()
  const { data: apiaries = [], isLoading: loadingApiaries } = useApiaries()
  const copyMutation = useCopyDiet(diet.id)

  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<Set<number>>(new Set())

  // Close on Escape
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [onClose])

  const apiaryNameById = useMemo(
    () => new Map(apiaries.map(a => [a.id, a.name])),
    [apiaries],
  )

  const totalAvailable = useMemo(
    () => beehives.filter(b => b.id !== diet.beehiveId).length,
    [beehives, diet.beehiveId],
  )

  // Candidate hives (all accessible except the source), filtered by search and grouped by apiary.
  const groups = useMemo<Group[]>(() => {
    const q = search.trim().toLowerCase()
    const candidates = beehives
      .filter(b => b.id !== diet.beehiveId)
      .filter(b =>
        !q ||
        b.name.toLowerCase().includes(q) ||
        (b.labelNumber ?? '').toLowerCase().includes(q))

    const byApiary = new Map<number, Group>()
    for (const b of candidates) {
      let g = byApiary.get(b.apiaryId)
      if (!g) {
        g = { apiaryId: b.apiaryId, apiaryName: apiaryNameById.get(b.apiaryId) ?? 'Ostale košnice', hives: [] }
        byApiary.set(b.apiaryId, g)
      }
      g.hives.push({ id: b.id, name: b.name, labelNumber: b.labelNumber })
    }

    const list = [...byApiary.values()]
    list.forEach(g => g.hives.sort((a, b) => a.name.localeCompare(b.name)))
    list.sort((a, b) => a.apiaryName.localeCompare(b.apiaryName))
    return list
  }, [beehives, apiaryNameById, diet.beehiveId, search])

  function toggle(id: number) {
    setSelected(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function toggleGroup(g: Group) {
    const ids = g.hives.map(h => h.id)
    const allSelected = ids.every(id => selected.has(id))
    setSelected(prev => {
      const next = new Set(prev)
      if (allSelected) ids.forEach(id => next.delete(id))
      else ids.forEach(id => next.add(id))
      return next
    })
  }

  async function handleCopy() {
    if (selected.size === 0) return
    try {
      const created = await copyMutation.mutateAsync({ targetBeehiveIds: [...selected] })
      const n = created.length
      toast.success(`Prehrana kopirana na ${n} ${hiveWord(n)}.`)
      onClose()
    } catch {
      toast.error('Kopiranje nije uspjelo. Pokušajte ponovo.')
    }
  }

  const loading = loadingHives || loadingApiaries

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4"
      onClick={onClose}
    >
      <div
        className="bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl w-full max-w-lg animate-fade-in flex flex-col max-h-[85vh]"
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="p-5 sm:p-6 border-b border-gray-100 dark:border-slate-800">
          <div className="flex items-start gap-3">
            <div className="w-10 h-10 rounded-full bg-honey-100 dark:bg-honey-500/15 flex items-center justify-center shrink-0">
              <Copy className="w-5 h-5 text-honey-600 dark:text-honey-400" />
            </div>
            <div className="min-w-0 flex-1">
              <h2 className="font-semibold text-gray-800 dark:text-slate-100">Kopiraj prehranu na druge košnice</h2>
              <p className="text-sm text-gray-500 dark:text-slate-400 truncate">
                „{diet.name}" — isti raspored i datum početka
              </p>
            </div>
            <button
              onClick={onClose}
              className="shrink-0 text-gray-400 hover:text-gray-600 dark:hover:text-slate-200 transition-colors"
              aria-label="Zatvori"
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          {totalAvailable > 0 && (
            <div className="relative mt-4">
              <Search className="w-4 h-4 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" />
              <input
                className="form-input pl-9"
                placeholder="Pretraži košnice…"
                value={search}
                onChange={e => setSearch(e.target.value)}
              />
            </div>
          )}
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto p-4 sm:p-5">
          {loading ? (
            <LoadingSpinner message="Učitavanje košnica…" />
          ) : totalAvailable === 0 ? (
            <div className="text-center py-10 text-gray-400 dark:text-slate-500">
              <Leaf className="w-10 h-10 mx-auto mb-3 text-gray-200 dark:text-slate-700" />
              <p className="text-sm">Nema drugih košnica kojima možete pristupiti.</p>
            </div>
          ) : groups.length === 0 ? (
            <div className="text-center py-10 text-gray-400 dark:text-slate-500 text-sm">
              Nijedna košnica ne odgovara pretrazi.
            </div>
          ) : (
            <div className="space-y-5">
              {groups.map(g => {
                const ids = g.hives.map(h => h.id)
                const allSelected = ids.every(id => selected.has(id))
                return (
                  <div key={g.apiaryId}>
                    <div className="flex items-center justify-between mb-2">
                      <p className="text-xs font-semibold text-gray-400 dark:text-slate-500 uppercase tracking-wide truncate">
                        {g.apiaryName}
                      </p>
                      <button
                        onClick={() => toggleGroup(g)}
                        className="shrink-0 text-xs font-medium text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300"
                      >
                        {allSelected ? 'Poništi sve' : 'Označi sve'}
                      </button>
                    </div>
                    <div className="space-y-2">
                      {g.hives.map(h => {
                        const checked = selected.has(h.id)
                        return (
                          <button
                            key={h.id}
                            onClick={() => toggle(h.id)}
                            className={`w-full flex items-center gap-3 py-2.5 px-3 rounded-xl border text-left transition-colors ${
                              checked
                                ? 'bg-honey-50 border-honey-300 dark:bg-honey-500/10 dark:border-honey-500/40'
                                : 'bg-white border-gray-100 hover:border-honey-200 dark:bg-slate-900 dark:border-slate-800 dark:hover:border-honey-500/30'
                            }`}
                          >
                            <span className={`w-5 h-5 rounded-md border flex items-center justify-center shrink-0 transition-colors ${
                              checked
                                ? 'bg-honey-500 border-honey-500 text-white'
                                : 'border-gray-300 dark:border-slate-600'
                            }`}>
                              {checked && <Check className="w-3.5 h-3.5" />}
                            </span>
                            <span className="flex-1 min-w-0 text-sm font-medium text-gray-800 dark:text-slate-100 truncate">
                              {h.name}
                            </span>
                            {h.labelNumber && (
                              <span className="badge bg-gray-100 text-gray-500 dark:bg-slate-700 dark:text-slate-300 shrink-0">
                                #{h.labelNumber}
                              </span>
                            )}
                          </button>
                        )
                      })}
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-4 sm:p-5 border-t border-gray-100 dark:border-slate-800 flex items-center justify-between gap-3">
          <span className="text-sm text-gray-500 dark:text-slate-400">
            Odabrano: <span className="font-semibold text-gray-700 dark:text-slate-200">{selected.size}</span>
          </span>
          <div className="flex gap-3">
            <button onClick={onClose} className="btn-secondary text-sm" disabled={copyMutation.isPending}>
              Otkaži
            </button>
            <button
              onClick={handleCopy}
              className="btn-primary text-sm"
              disabled={selected.size === 0 || copyMutation.isPending}
            >
              {copyMutation.isPending
                ? <><Loader2 className="w-4 h-4 animate-spin" /> Kopiram…</>
                : <><Copy className="w-4 h-4" /> Kopiraj{selected.size > 0 ? ` (${selected.size})` : ''}</>}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
