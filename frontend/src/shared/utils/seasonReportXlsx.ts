import type { CurrencyAmount, ReportSections, SeasonReport } from '../../core/models'
import type { Row } from 'write-excel-file/browser'

// Excel export of the season report (SPEC-25), built from the same DTO the PDF uses so the two
// documents cannot disagree. Four sheets, because a bookkeeper works in sheets, not in a PDF.
//
// write-excel-file (MIT) rather than SheetJS: the npm `xlsx` package has been stuck on 0.18.5 since
// 2022 — SheetJS moved distribution to their own CDN — so it cannot be pinned as an ordinary
// dependency. The library is imported lazily from the page, like the PDF font chunk.


type Cell = Row[number]

const MONEY = '#,##0.00'

const head = (...labels: string[]): Cell[] =>
  labels.map(value => ({ value, type: String, fontWeight: 'bold' as const }))

const text = (value: string): Cell => ({ value, type: String })
const num = (value: number): Cell => ({ value, type: Number, format: MONEY })
const int = (value: number): Cell => ({ value, type: Number })
const blank: Cell = {}

export async function downloadSeasonReportXlsx(report: SeasonReport, sections: ReportSections) {
  const { default: writeXlsxFile } = await import('write-excel-file/browser')

  const h = report.header
  const period = `${fmtDate(h.from)} — ${fmtDate(h.to)}`

  // A deselected section drops its whole sheet rather than leaving an empty one — an empty tab in a
  // workbook reads as "we had no data", which is a different claim from "I did not ask for this".
  // Sažetak always stays: it carries the balance and the notes.
  const sheets = [
    ...(sections.yield ? [{ sheet: 'Prinos', data: sheetYield(report, period, sections) }] : []),
    ...(sections.expenses ? [{ sheet: 'Troškovi', data: sheetExpenses(report) }] : []),
    ...(sections.treatments ? [{ sheet: 'Tretmani', data: sheetTreatments(report) }] : []),
    { sheet: 'Sažetak', data: sheetSummary(report, period, sections) },
  ]

  // v4 returns a { toBlob, toFile } handle rather than downloading as a side effect of options.
  await writeXlsxFile(sheets).toFile(`izvjestaj-${h.from}_${h.to}.xlsx`)
}

// ── Sheets ─────────────────────────────────────────────────────────────────────

function sheetYield(report: SeasonReport, period: string, sections: ReportSections): Cell[][] {
  const y = report.yield
  return [
    [text('IZVJEŠTAJ PČELARENJA — PRINOS')],
    [text('Organizacija'), text(report.header.organizationName)],
    [text('Period'), text(period)],
    [blank],
    [text('Ukupno vrcano (kg)'), num(y.totalKg)],
    [text('Sa upisanom cijenom (kg)'), num(y.pricedKg)],
    [text('Bez upisane cijene (kg)'), num(y.unpricedKg)],
    [text('Broj vrcanja'), int(y.harvestCount)],
    [blank],
    ...(sections.yieldByApiary ? namedKgBlock('Po pčelinjaku', 'Pčelinjak', y.byApiary) : []),
    ...(sections.yieldByHoneyType ? namedKgBlock('Po vrsti meda', 'Vrsta meda', y.byHoneyType) : []),
    ...(sections.yieldByBeehive ? namedKgBlock('Po košnici', 'Košnica', y.byBeehive) : []),
    ...(sections.yieldByPasture && y.byPasture.length > 0
      ? namedKgBlock('Po pašnjaku', 'Pašnjak', y.byPasture) : []),
  ]
}

function sheetExpenses(report: SeasonReport): Cell[][] {
  const e = report.expenses
  return [
    [text('TROŠKOVI')],
    [text('Broj računa'), int(e.count)],
    [blank],
    [text('Ukupno po valuti')],
    head('Valuta', 'Iznos'),
    ...e.byCurrency.map(c => [text(c.currency), num(c.amount)]),
    [blank],
    [text('Po pčelinjaku')],
    head('Pčelinjak', 'Iznos'),
    ...e.byApiary.map(a => [text(a.apiaryName), text(fmtCurrencies(a.byCurrency))]),
    [blank],
    [text('Zajednički troškovi')],
    head('Valuta', 'Iznos'),
    ...e.sharedByCurrency.map(c => [text(c.currency), num(c.amount)]),
    ...(e.byDiet.length > 0
      ? [
          [blank],
          [text('Prehrana po programu')],
          head('Program', 'Iznos'),
          ...e.byDiet.map(d => [text(d.dietName), text(fmtCurrencies(d.byCurrency))]),
        ]
      : []),
  ]
}

function sheetTreatments(report: SeasonReport): Cell[][] {
  const t = report.treatments
  return [
    [text('TRETMANI')],
    [text('Broj tretmana'), int(t.count)],
    [text('Tretirano košnica'), int(t.hivesTreated)],
    [text('U karenci'), int(t.activeKarencaCount)],
    [blank],
    head('Preparat', 'Aktivna tvar', 'Tretmana', 'Košnica'),
    ...t.byProduct.map(p => [text(p.productName), text(p.activeSubstanceName), int(p.treatmentCount), int(p.hiveCount)]),
    [blank],
    [text('Detaljna evidencija tretmana izvozi se zasebno, po pčelinjaku i godini.')],
  ]
}

function sheetSummary(report: SeasonReport, period: string, sections: ReportSections): Cell[][] {
  const { balance, notes, header } = report
  return [
    [text('SAŽETAK')],
    [text('Organizacija'), text(header.organizationName)],
    [text('Period'), text(period)],
    [text('Pčelinjaci'), text(header.apiaryNames.join(', ') || '—')],
    [text('Datum izrade'), text(fmtDateTime(header.generatedAt))],
    [blank],
    ...(sections.balance
      ? [
          [text('BILANSA (KM)')],
          [text('Procijenjeni prihod'), num(balance.estimatedRevenueBam)],
          [text('Troškovi'), num(balance.totalExpenseBam)],
          [text('Razlika'), num(balance.netBam)],
          [blank],
          head('Pčelinjak', 'kg', 'Prihod', 'Trošak', 'Razlika'),
          ...balance.byApiary.map(a => [
            text(a.apiaryName), num(a.kg), num(a.estimatedRevenueBam), num(a.expenseBam), num(a.netBam),
          ]),
          [blank],
        ]
      : []),
    [text('NAPOMENE')],
    // Same clauses as the PDF: a number must not read as more certain in one export than the other.
    ...(notes.unpricedKg > 0
      ? [[text(`Prihod je procjena: ${notes.unpricedKg.toFixed(2)} kg nema upisanu cijenu i nije uračunato.`)]]
      : []),
    ...(notes.unassignedExpenseCount > 0
      ? [[text(`${notes.unassignedExpenseCount} računa nije vezano ni za jedan pčelinjak (zajednički trošak).`)]]
      : []),
    ...(notes.nonBamCurrencies.length > 0
      ? [[text(`Bilansa je samo u KM. Valute izvan bilanse: ${notes.nonBamCurrencies.join(', ')}.`)]]
      : []),
  ]
}

// ── Helpers ────────────────────────────────────────────────────────────────────

function namedKgBlock(caption: string, label: string, rows: Array<{ name: string; kg: number }>): Cell[][] {
  return [
    [text(caption)],
    head(label, 'kg'),
    ...(rows.length > 0
      ? rows.map(r => [text(r.name), num(r.kg)])
      : [[text('Nema podataka za ovaj period.')]]),
    [blank],
  ]
}

function fmtCurrencies(amounts: CurrencyAmount[]): string {
  if (amounts.length === 0) return '—'
  return amounts.map(c => `${c.amount.toFixed(2)} ${c.currency}`).join(' + ')
}

function fmtDate(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${p(d.getDate())}.${p(d.getMonth() + 1)}.${d.getFullYear()}.`
}

function fmtDateTime(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${fmtDate(iso)} ${p(d.getHours())}:${p(d.getMinutes())}`
}
