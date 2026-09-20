import { useMemo, useState } from 'react'
import { FileDown, Loader2 } from 'lucide-react'
import clsx from 'clsx'
import { useSeasonReport } from '../../core/services/reportQueries'
import { useApiaries } from '../../core/services/queries'
import { ErrorState, PageSkeleton, VitalCard } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'
import { SECTION_GROUPS, hasAnySection, useReportPreferences } from './useReportPreferences'
import type { CurrencyAmount, NamedKg, ReportFormat, ReportSections, SeasonReport } from '../../core/models'

// ── Period presets (SPEC-25 D3) ───────────────────────────────────────────────
// A free from–to range with presets on top, rather than a fixed year/quarter/month dropdown: the
// period a subsidy application asks for does not have to line up with a calendar quarter.

const iso = (d: Date) => {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

const YEAR = new Date().getFullYear()
const MONTHS = [
  'Januar', 'Februar', 'Mart', 'April', 'Maj', 'Juni',
  'Juli', 'August', 'Septembar', 'Oktobar', 'Novembar', 'Decembar',
]

interface Preset { label: string; from: string; to: string }

const yearRange = (y: number): Preset => ({ label: String(y), from: `${y}-01-01`, to: `${y}-12-31` })

const quarterRange = (y: number, q: number): Preset => ({
  label: `Q${q} ${y}`,
  from: iso(new Date(y, (q - 1) * 3, 1)),
  to: iso(new Date(y, q * 3, 0)),
})

/** March–October: the part of the year a hive actually works. */
const seasonRange = (y: number): Preset => ({ label: `Sezona ${y}`, from: `${y}-03-01`, to: `${y}-10-31` })

const PRESETS: Preset[] = [
  yearRange(YEAR),
  yearRange(YEAR - 1),
  seasonRange(YEAR),
  quarterRange(YEAR, 1),
  quarterRange(YEAR, 2),
  quarterRange(YEAR, 3),
  quarterRange(YEAR, 4),
]

const monthPreset = (y: number, m: number): Preset => ({
  label: `${MONTHS[m]} ${y}`,
  from: iso(new Date(y, m, 1)),
  to: iso(new Date(y, m + 1, 0)),
})

// ── Page ──────────────────────────────────────────────────────────────────────

export default function ReportPage() {
  const { toast } = useToast()
  const { data: apiaries = [] } = useApiaries()

  const [from, setFrom] = useState(PRESETS[0].from)
  const [to, setTo] = useState(PRESETS[0].to)
  const [apiaryId, setApiaryId] = useState<number | ''>('')
  const [exporting, setExporting] = useState(false)

  // Remembered per browser — the same report gets filed every season (SPEC-25).
  const { sections, toggleSection, format, setFormat } = useReportPreferences()
  const anySection = hasAnySection(sections)
  const selectedCount = SECTION_GROUPS.filter(g => sections[g.key]).length

  const rangeValid = from !== '' && to !== '' && from <= to

  const filters = useMemo(
    () => (rangeValid ? { from, to, apiaryId: apiaryId === '' ? undefined : apiaryId } : null),
    [from, to, apiaryId, rangeValid],
  )
  const { data: report, isLoading, isError, refetch } = useSeasonReport(filters)

  const applyPreset = (p: Preset) => { setFrom(p.from); setTo(p.to) }
  const isActivePreset = (p: Preset) => p.from === from && p.to === to

  async function handleExport() {
    if (!report || !anySection) return
    setExporting(true)
    try {
      if (format === 'pdf') {
        const { downloadSeasonReportPdf } = await import('../../shared/utils/seasonReportPdf')
        await downloadSeasonReportPdf(report, sections)
      } else {
        const { downloadSeasonReportXlsx } = await import('../../shared/utils/seasonReportXlsx')
        await downloadSeasonReportXlsx(report, sections)
      }
    } catch {
      toast.error(format === 'pdf' ? 'Greška pri izradi PDF-a.' : 'Greška pri izradi Excel datoteke.')
    } finally {
      setExporting(false)
    }
  }

  return (
    <div className="animate-fade-in">
      {/* ── Hero ────────────────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none mb-6">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center gap-4 min-w-0">
            <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
              📄
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Izvještaji</h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
                Objedinjeni prinos, troškovi i tretmani za bilo koji period — spremno za prijavu na subvencije
              </p>
            </div>
          </div>

          {/* Same shape as the Vrcanja hero: a small select next to the page's primary action. */}
          <div className="flex items-center gap-2 shrink-0">
            <select
              value={format}
              onChange={e => setFormat(e.target.value as ReportFormat)}
              aria-label="Format izvoza"
              className="px-3 py-2 rounded-xl border border-honey-200 dark:border-slate-700 bg-white/70 dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 outline-none focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
            >
              <option value="pdf">PDF</option>
              <option value="xlsx">Excel</option>
            </select>
            <button
              onClick={handleExport}
              disabled={!report || exporting || !anySection}
              className="btn-primary text-sm"
              title={anySection ? 'Izvezi izvještaj' : 'Odaberite barem jednu sekciju'}
            >
              {exporting ? <Loader2 className="w-4 h-4 animate-spin" /> : <FileDown className="w-4 h-4" />}
              <span className="hidden sm:inline">Izvezi</span>
            </button>
          </div>
        </div>
      </div>

      {/* ── Period picker ───────────────────────────────────────────────────── */}
      <div className="card mb-6">
        <div className="flex flex-wrap gap-2 mb-4">
          {PRESETS.map(p => (
            <button
              key={p.label}
              onClick={() => applyPreset(p)}
              className={clsx(
                'px-3 py-1.5 rounded-lg text-sm font-medium transition-all border',
                isActivePreset(p)
                  ? 'bg-honey-100 dark:bg-honey-500/20 border-honey-300 dark:border-honey-500/40 text-honey-800 dark:text-honey-200'
                  : 'bg-white dark:bg-slate-800 border-gray-200 dark:border-slate-700 text-gray-600 dark:text-slate-300 hover:border-honey-300 dark:hover:border-slate-600',
              )}
            >
              {p.label}
            </button>
          ))}
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">Od</label>
            <input type="date" value={from} onChange={e => setFrom(e.target.value)} className={inputCls} />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">Do</label>
            <input type="date" value={to} onChange={e => setTo(e.target.value)} className={inputCls} />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">Mjesec</label>
            <select
              value=""
              onChange={e => { if (e.target.value !== '') applyPreset(monthPreset(YEAR, Number(e.target.value))) }}
              className={inputCls}
            >
              <option value="">Odaberi mjesec…</option>
              {MONTHS.map((m, i) => <option key={m} value={i}>{m} {YEAR}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">Pčelinjak</label>
            <select
              value={apiaryId}
              onChange={e => setApiaryId(e.target.value === '' ? '' : Number(e.target.value))}
              className={inputCls}
            >
              <option value="">Svi pčelinjaci</option>
              {apiaries.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
            </select>
          </div>
        </div>

        {!rangeValid && (
          <p className="mt-3 text-sm text-red-600 dark:text-red-400">
            Početni datum ne može biti poslije krajnjeg.
          </p>
        )}

        <div className="mt-6 pt-5 border-t border-gray-100 dark:border-slate-800">
          <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
            Sekcije u dokumentu
          </label>
          <div className="space-y-2">
            {SECTION_GROUPS.map(group => (
              <div key={group.key}>
                <SectionCheckbox
                  label={group.label}
                  checked={sections[group.key]}
                  onChange={() => toggleSection(group.key)}
                />
                {group.children && sections[group.key] && (
                  <div className="ml-6 mt-2 space-y-2">
                    {group.children.map(child => (
                      <SectionCheckbox
                        key={child.key}
                        label={child.label}
                        checked={sections[child.key]}
                        onChange={() => toggleSection(child.key)}
                      />
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>
          <p className="text-xs text-gray-400 dark:text-slate-500 mt-2">
            Odabrano: {selectedCount} od {SECTION_GROUPS.length}. Napomene o procjeni prihoda i valutama
            uvijek ostaju u dokumentu.
          </p>
          {!anySection && (
            <p className="text-xs text-red-500 dark:text-red-400 mt-1">
              Odaberite barem jednu sekciju da biste izvezli izvještaj.
            </p>
          )}
        </div>
      </div>

      {/* ── Preview ─────────────────────────────────────────────────────────── */}
      {isLoading && <PageSkeleton rows={5} />}
      {isError && <ErrorState onRetry={() => refetch()} />}
      {report && !isLoading && <ReportPreview report={report} sections={sections} />}
    </div>
  )
}

// ── Preview ───────────────────────────────────────────────────────────────────

function ReportPreview({ report, sections }: { report: SeasonReport; sections: ReportSections }) {
  const { yield: y, expenses, balance, treatments, notes } = report

  return (
    <>
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger mb-6">
        <VitalCard icon="🍯" label="Prinos"    value={`${fmtKg(y.totalKg)} kg`}      gradient="from-honey-400 to-honey-600" />
        <VitalCard icon="💰" label="Prihod (procjena)" value={fmtMoney(balance.estimatedRevenueBam)} gradient="from-emerald-400 to-teal-600" />
        <VitalCard icon="🧾" label="Troškovi (KM)" value={fmtMoney(balance.totalExpenseBam)} gradient="from-rose-400 to-red-500" />
        <VitalCard icon="⚖️" label="Razlika (KM)"  value={fmtMoney(balance.netBam)}   gradient="from-violet-400 to-indigo-600" />
      </div>

      {/* The document's honesty clauses, shown on screen too so nothing appears only in the export. */}
      {(notes.unpricedKg > 0 || notes.unassignedExpenseCount > 0 || notes.nonBamCurrencies.length > 0) && (
        <div className="card mb-6 border-amber-200 dark:border-amber-500/30 bg-amber-50/60 dark:bg-amber-500/5">
          <h2 className="font-display text-base font-semibold text-gray-800 dark:text-slate-100 mb-2">Napomene</h2>
          <ul className="space-y-1.5 text-sm text-gray-700 dark:text-slate-300">
            {notes.unpricedKg > 0 && (
              <li>
                Prihod je procjena: <strong>{fmtKg(notes.unpricedKg)} kg</strong> nema upisanu cijenu i nije uračunato.
              </li>
            )}
            {notes.unassignedExpenseCount > 0 && (
              <li>
                <strong>{notes.unassignedExpenseCount}</strong> {plural(notes.unassignedExpenseCount, 'račun', 'računa', 'računa')} nije vezano
                ni za jedan pčelinjak — prikazano kao zajednički trošak.
              </li>
            )}
            {notes.nonBamCurrencies.length > 0 && (
              <li>
                Bilansa je samo u KM. Troškovi u drugim valutama ({notes.nonBamCurrencies.join(', ')}) prikazani su
                odvojeno i nisu oduzeti od prihoda.
              </li>
            )}
          </ul>
        </div>
      )}

      {sections.yield && (
        <Section title="Prinos" icon="🍯">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {sections.yieldByApiary && <KgTable caption="Po pčelinjaku" label="Pčelinjak" rows={y.byApiary} />}
            {sections.yieldByHoneyType && <KgTable caption="Po vrsti meda" label="Vrsta meda" rows={y.byHoneyType} />}
            {sections.yieldByBeehive && <KgTable caption="Po košnici" label="Košnica" rows={y.byBeehive} />}
            {sections.yieldByPasture && y.byPasture.length > 0 && (
              <KgTable caption="Po pašnjaku" label="Pašnjak" rows={y.byPasture} />
            )}
          </div>
        </Section>
      )}

      {sections.expenses && (
      <Section title="Troškovi" icon="🧾">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <SimpleTable
            caption="Ukupno po valuti"
            head={['Valuta', 'Iznos']}
            rows={expenses.byCurrency.map(c => [c.currency, fmtMoney(c.amount)])}
          />
          <SimpleTable
            caption="Po pčelinjaku"
            head={['Pčelinjak', 'Iznos']}
            rows={expenses.byApiary.map(a => [a.apiaryName, fmtCurrencies(a.byCurrency)])}
          />
          <SimpleTable
            caption="Zajednički troškovi"
            head={['Valuta', 'Iznos']}
            rows={expenses.sharedByCurrency.map(c => [c.currency, fmtMoney(c.amount)])}
          />
          {expenses.byDiet.length > 0 && (
            <SimpleTable
              caption="Prehrana po programu"
              head={['Program', 'Iznos']}
              rows={expenses.byDiet.map(d => [d.dietName, fmtCurrencies(d.byCurrency)])}
            />
          )}
        </div>
      </Section>
      )}

      {sections.balance && (
      <Section title="Bilansa po pčelinjaku (KM)" icon="⚖️">
        <SimpleTable
          head={['Pčelinjak', 'kg', 'Prihod', 'Trošak', 'Razlika']}
          rows={balance.byApiary.map(a => [
            a.apiaryName, fmtKg(a.kg), fmtMoney(a.estimatedRevenueBam), fmtMoney(a.expenseBam), fmtMoney(a.netBam),
          ])}
        />
      </Section>
      )}

      {sections.treatments && (
      <Section title="Tretmani" icon="💊">
        <div className="flex flex-wrap gap-6 mb-4 text-sm text-gray-600 dark:text-slate-400">
          <span>Tretmana: <strong className="text-gray-900 dark:text-slate-100">{treatments.count}</strong></span>
          <span>Tretirano košnica: <strong className="text-gray-900 dark:text-slate-100">{treatments.hivesTreated}</strong></span>
          <span>U karenci: <strong className="text-gray-900 dark:text-slate-100">{treatments.activeKarencaCount}</strong></span>
        </div>
        <SimpleTable
          head={['Preparat', 'Aktivna tvar', 'Tretmana', 'Košnica']}
          rows={treatments.byProduct.map(p => [
            p.productName, p.activeSubstanceName, String(p.treatmentCount), String(p.hiveCount),
          ])}
        />
        <p className="mt-3 text-xs text-gray-400 dark:text-slate-500">
          Detaljna evidencija tretmana izvozi se zasebno, sa stranice Tretmani.
        </p>
      </Section>
      )}
    </>
  )
}

// ── Building blocks ───────────────────────────────────────────────────────────

/** Same markup and classes as the hive pickers on the feeding and treatment forms — the app has one
 *  checkbox, and it is honey-coloured (`accent-honey-500`), not the browser default blue. */
function SectionCheckbox({ label, checked, onChange }: {
  label: string; checked: boolean; onChange: () => void
}) {
  return (
    <label className="flex items-center gap-2 cursor-pointer">
      <input
        type="checkbox"
        checked={checked}
        onChange={onChange}
        className="w-4 h-4 rounded border-gray-300 dark:border-slate-600 text-honey-500 focus:ring-honey-400 accent-honey-500"
      />
      <span className="text-sm text-gray-700 dark:text-slate-200 truncate">{label}</span>
    </label>
  )
}

function Section({ title, icon, children }: { title: string; icon: string; children: React.ReactNode }) {
  return (
    <div className="card mb-6">
      <div className="flex items-center gap-2 mb-5">
        <span className="text-xl">{icon}</span>
        <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">{title}</h2>
      </div>
      {children}
    </div>
  )
}

function KgTable({ caption, label, rows }: { caption: string; label: string; rows: NamedKg[] }) {
  return <SimpleTable caption={caption} head={[label, 'kg']} rows={rows.map(r => [r.name, fmtKg(r.kg)])} />
}

function SimpleTable({ caption, head, rows }: { caption?: string; head: string[]; rows: string[][] }) {
  return (
    <div>
      {caption && (
        <h3 className="text-sm font-medium text-gray-600 dark:text-slate-400 mb-2">{caption}</h3>
      )}
      {rows.length === 0 ? (
        <p className="text-sm text-gray-400 dark:text-slate-500 py-3">Nema podataka za ovaj period.</p>
      ) : (
        // Wide tables scroll inside their own container — the page body must never scroll sideways.
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-200 dark:border-slate-700">
                {head.map((h, i) => (
                  <th
                    key={h}
                    className={clsx(
                      'py-2 font-medium text-gray-500 dark:text-slate-400 whitespace-nowrap',
                      i === 0 ? 'text-left' : 'text-right pl-4',
                    )}
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, r) => (
                <tr key={r} className="border-b border-gray-100 dark:border-slate-800 last:border-0">
                  {row.map((cell, c) => (
                    <td
                      key={c}
                      className={clsx(
                        'py-2 text-gray-700 dark:text-slate-300',
                        c === 0 ? 'text-left' : 'text-right pl-4 tabular-nums whitespace-nowrap',
                      )}
                    >
                      {cell}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

// ── Styles + formatting ───────────────────────────────────────────────────────

const inputCls =
  'w-full px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all'

function fmtKg(n: number): string {
  return n.toLocaleString('bs-BA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function fmtMoney(n: number): string {
  return n.toLocaleString('bs-BA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function fmtCurrencies(amounts: CurrencyAmount[]): string {
  if (amounts.length === 0) return '—'
  return amounts.map(c => `${fmtMoney(c.amount)} ${c.currency}`).join(' + ')
}

/** Bosnian has three plural forms — `n === 1 ? a : b` produces "2 račun". */
function plural(n: number, one: string, few: string, many: string): string {
  const mod10 = n % 10
  const mod100 = n % 100
  if (mod10 === 1 && mod100 !== 11) return one
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few
  return many
}
