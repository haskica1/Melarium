import { HiveProductTypeLabels } from '../../core/models'
import type { CurrencyAmount, ReportProductKg, ReportSections, SeasonReport } from '../../core/models'
import type { Row } from 'write-excel-file/browser'
import { fmtProductQty, kgToDisplay, productColumns, productKgIn, qtyUnit, type ProductRow } from './hiveProductUnits'

// Excel export of the season report (SPEC-25), built from the same DTO the PDF uses so the two
// documents cannot disagree. Up to four sheets, because a bookkeeper works in sheets, not in a PDF.
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
const numBold = (value: number): Cell => ({ value, type: Number, format: MONEY, fontWeight: 'bold' as const })
const textBold = (value: string): Cell => ({ value, type: String, fontWeight: 'bold' as const })
const int = (value: number): Cell => ({ value, type: Number })
/** General format: 350 stays 350 and 1.234 g of venom keeps its milligrams. */
const qty = (value: number): Cell => ({ value: Math.round(value * 1000) / 1000, type: Number })
const blank: Cell = {}

export async function downloadSeasonReportXlsx(report: SeasonReport, sections: ReportSections) {
  const { default: writeXlsxFile } = await import('write-excel-file/browser')

  const h = report.header
  const period = `${fmtDate(h.from)} — ${fmtDate(h.to)}`

  // A deselected section drops its whole sheet rather than leaving an empty one — an empty tab in a
  // workbook reads as "we had no data", which is a different claim from "I did not ask for this".
  // Sažetak always stays: it carries the balance and the notes.
  const sheets = [
    ...(sections.yield ? [{ sheet: 'Prinosi', data: sheetHarvests(report, period, sections) }] : []),
    ...(sections.expenses ? [{ sheet: 'Troškovi', data: sheetExpenses(report) }] : []),
    ...(sections.treatments ? [{ sheet: 'Tretmani', data: sheetTreatments(report) }] : []),
    { sheet: 'Sažetak', data: sheetSummary(report, period, sections) },
  ]

  // v4 returns a { toBlob, toFile } handle rather than downloading as a side effect of options.
  await writeXlsxFile(sheets).toFile(`izvjestaj-${h.from}_${h.to}.xlsx`)
}

// ── Sheets ─────────────────────────────────────────────────────────────────────

/**
 * "Prinosi" (SPEC-30): every product first — quantity as a number in the product's own unit, the unit
 * in the next column, so a bookkeeper can sum one product's rows and nothing invites summing wax with
 * royal jelly — then honey in detail, then the other products broken down the same way.
 */
function sheetHarvests(report: SeasonReport, period: string, sections: ReportSections): Cell[][] {
  const { harvests, yield: y, products } = report
  const byPasture = sections.yieldByPasture && products.byPasture.length > 0
  const showProducts = products.recordCount > 0 && (sections.yieldByApiary || sections.yieldByBeehive || byPasture)

  return [
    [text('IZVJEŠTAJ PČELARENJA — PRINOSI')],
    [text('Organizacija'), text(report.header.organizationName)],
    [text('Period'), text(period)],
    [blank],
    [text('Po proizvodu')],
    head('Proizvod', 'Količina', 'Jedinica', 'Bez cijene', 'Prihod (KM)', 'Zapisa'),
    ...(harvests.byProduct.length > 0
      ? harvests.byProduct.map(p => [
          text(p.name),
          qty(kgToDisplay(p.kg, p.productType)),
          text(qtyUnit(p.productType)),
          qty(kgToDisplay(p.unpricedKg, p.productType)),
          num(p.estimatedRevenueBam),
          int(p.recordCount),
        ])
      : [[text('Nema podataka za ovaj period.')]]),
    // Revenue is the one column that adds up across products — and it is the balance's revenue.
    ...(harvests.byProduct.length > 1
      ? [[textBold('Ukupno prihod'), blank, blank, blank, numBold(harvests.estimatedRevenueBam)]]
      : []),
    [blank],
    [text('MED')],
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
    ...(showProducts
      ? [
          [text('OSTALI PROIZVODI')],
          ...(sections.yieldByApiary
            ? productBlock('Po pčelinjaku', 'Pčelinjak', products.byApiary.map(a => ({ name: a.apiaryName, items: a.items })))
            : []),
          ...(byPasture ? productBlock('Po pašnjaku', 'Pašnjak', products.byPasture) : []),
          ...(sections.yieldByBeehive ? productBlock('Po košnici', 'Košnica', products.byBeehive) : []),
        ]
      : []),
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
          [text('Procijenjeni prihod — med'), num(balance.estimatedRevenueBam)],
          [text('Procijenjeni prihod — ostali proizvodi'), num(balance.productRevenueBam)],
          [text('Troškovi'), num(balance.totalExpenseBam)],
          [text('Razlika'), num(balance.netBam)],
          [blank],
          head('Pčelinjak', 'kg meda', 'Prihod med', 'Prihod proizvodi', 'Trošak', 'Razlika'),
          ...balance.byApiary.map(a => [
            text(a.apiaryName), num(a.kg), num(a.estimatedRevenueBam), num(a.productRevenueBam),
            num(a.expenseBam), num(a.netBam),
          ]),
          [blank],
        ]
      : []),
    [text('NAPOMENE')],
    // Same clauses as the PDF: a number must not read as more certain in one export than the other.
    ...(notes.unpriced.length > 0
      ? [[text(`Prihod je procjena — bez upisane cijene, pa nije uračunato: ${productList(notes.unpriced)}.`)]]
      : []),
    ...(notes.notPerHive.length > 0
      ? [[text(`Upisano ukupno, bez raspodjele po košnicama: ${productList(notes.notPerHive)} — u svim zbirovima, ali ne u tabelama po košnici.`)]]
      : []),
    ...(notes.sharedHarvestCount > 0
      ? [[text(`Zapisi za cijelu organizaciju (nisu vezani za pčelinjak): ${notes.sharedHarvestCount} — ulaze u ukupnu bilansu, ne u bilansu pojedinog pčelinjaka.`)]]
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

/** One column per product present, the unit in its header, a number in the product's unit per cell. */
function productBlock(caption: string, label: string, rows: ProductRow[]): Cell[][] {
  const columns = productColumns(rows)
  return [
    [text(caption)],
    head(label, ...columns.map(t => `${HiveProductTypeLabels[t]} (${qtyUnit(t)})`)),
    ...(rows.length > 0
      ? rows.map(r => [
          text(r.name),
          ...columns.map(t => {
            const kg = productKgIn(r, t)
            return kg === null ? blank : qty(kgToDisplay(kg, t))
          }),
        ])
      : [[text('Nema podataka za ovaj period.')]]),
    [blank],
  ]
}

/** "Med 81,5 kg, Vosak 4 kg, Matična mliječ 180 g" — each product in its own unit, never one total. */
function productList(items: ReportProductKg[]): string {
  return items.map(p => `${p.name} ${fmtProductQty(p.kg, p.productType)}`).join(', ')
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
