import { jsPDF } from 'jspdf'
import type { SeasonReport, CurrencyAmount, ReportSections } from '../../core/models'

// The consolidated season report (SPEC-25), rendered client-side from the same DTO the Excel export
// uses — so the two documents cannot disagree on a number. A4 portrait, unlike the treatment
// register's landscape: this one is narrow tables, not thirteen columns.
//
// Shares the lazy DejaVu Sans chunk with treatmentPdf.ts — jsPDF's built-in fonts are cp1252 and
// would mangle č/ć/đ/š/ž in a document that goes to a municipality.

const FONT = 'DejaVuSans'

const PAGE_W = 210
const PAGE_H = 297
const MARGIN = 14
const CONTENT_W = PAGE_W - MARGIN * 2
const BOTTOM = PAGE_H - 16

const HONEY: [number, number, number] = [245, 232, 200]
const STRIPE: [number, number, number] = [250, 246, 236]

interface Cursor { y: number }

export async function downloadSeasonReportPdf(report: SeasonReport, sections: ReportSections) {
  const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' })

  const { DEJAVU_SANS_BASE64 } = await import('./pdfFont')
  doc.addFileToVFS('DejaVuSans.ttf', DEJAVU_SANS_BASE64)
  doc.addFont('DejaVuSans.ttf', FONT, 'normal')
  doc.setFont(FONT)

  const cur: Cursor = { y: MARGIN }

  drawHeader(doc, report, cur)
  if (sections.yield) drawYield(doc, report, cur, sections)
  if (sections.expenses) drawExpenses(doc, report, cur)
  if (sections.balance) drawBalance(doc, report, cur)
  if (sections.treatments) drawTreatments(doc, report, cur)
  // Notes are never optional: they are what stops a figure reading as more certain than it is.
  drawNotes(doc, report, cur)
  drawSignature(doc, cur)

  addFooters(doc)

  doc.save(`izvjestaj-${report.header.from}_${report.header.to}.pdf`)
}

// ── Sections ───────────────────────────────────────────────────────────────────

function drawHeader(doc: jsPDF, report: SeasonReport, cur: Cursor) {
  const h = report.header

  doc.setFontSize(16)
  doc.text('IZVJEŠTAJ PČELARENJA', MARGIN, cur.y + 6)
  cur.y += 12

  doc.setFontSize(9)
  line(doc, cur, `Organizacija: ${h.organizationName || '—'}`)
  line(doc, cur, `Period: ${fmtDate(h.from)} — ${fmtDate(h.to)}`)
  line(doc, cur, `Pčelinjaci: ${h.apiaryNames.length > 0 ? h.apiaryNames.join(', ') : '—'}`)
  line(doc, cur, `Datum izrade: ${fmtDateTime(h.generatedAt)}`)

  // Deliberately blank (SPEC-25 D9): the organization carries no address or tax id yet, and a
  // subsidy form asks for both. Written as fill-in lines rather than omitted, so the document is
  // still usable — and so they can simply be filled in when those fields arrive.
  cur.y += 2
  line(doc, cur, 'Adresa: ______________________________________________')
  line(doc, cur, 'JIB / ID broj: ________________________________________')

  cur.y += 3
  doc.setDrawColor(200)
  doc.line(MARGIN, cur.y, PAGE_W - MARGIN, cur.y)
  cur.y += 6
}

function drawYield(doc: jsPDF, report: SeasonReport, cur: Cursor, sections: ReportSections) {
  const y = report.yield

  sectionTitle(doc, cur, 'PRINOS')
  keyValues(doc, cur, [
    ['Ukupno vrcano', `${fmtKg(y.totalKg)} kg`],
    ['Broj vrcanja', String(y.harvestCount)],
    ['Sa upisanom cijenom', `${fmtKg(y.pricedKg)} kg`],
    ['Bez upisane cijene', `${fmtKg(y.unpricedKg)} kg`],
  ])

  if (sections.yieldByApiary)
    table(doc, cur, 'Po pčelinjaku', ['Pčelinjak', 'kg'], y.byApiary.map(r => [r.name, fmtKg(r.kg)]))
  if (sections.yieldByHoneyType)
    table(doc, cur, 'Po vrsti meda', ['Vrsta meda', 'kg'], y.byHoneyType.map(r => [r.name, fmtKg(r.kg)]))
  if (sections.yieldByBeehive)
    table(doc, cur, 'Po košnici', ['Košnica', 'kg'], y.byBeehive.map(r => [r.name, fmtKg(r.kg)]))
  if (sections.yieldByPasture && y.byPasture.length > 0)
    table(doc, cur, 'Po pašnjaku', ['Pašnjak', 'kg'], y.byPasture.map(r => [r.name, fmtKg(r.kg)]))
}

function drawExpenses(doc: jsPDF, report: SeasonReport, cur: Cursor) {
  const e = report.expenses

  sectionTitle(doc, cur, 'TROŠKOVI')
  keyValues(doc, cur, [['Broj računa', String(e.count)]])

  // Grouped by currency, never summed across them (SPEC-25 D7).
  table(doc, cur, 'Ukupno po valuti', ['Valuta', 'Iznos'], e.byCurrency.map(c => [c.currency, fmtMoney(c.amount)]))

  table(
    doc, cur, 'Po pčelinjaku', ['Pčelinjak', 'Iznos'],
    e.byApiary.map(a => [a.apiaryName, fmtCurrencies(a.byCurrency)]),
  )
  table(
    doc, cur, 'Zajednički troškovi', ['Valuta', 'Iznos'],
    e.sharedByCurrency.map(c => [c.currency, fmtMoney(c.amount)]),
  )
  if (e.byDiet.length > 0)
    table(
      doc, cur, 'Prehrana po programu', ['Program', 'Iznos'],
      e.byDiet.map(d => [d.dietName, fmtCurrencies(d.byCurrency)]),
    )
}

function drawBalance(doc: jsPDF, report: SeasonReport, cur: Cursor) {
  const b = report.balance

  sectionTitle(doc, cur, 'BILANSA (KM)')
  keyValues(doc, cur, [
    ['Procijenjeni prihod', fmtMoney(b.estimatedRevenueBam)],
    ['Troškovi', fmtMoney(b.totalExpenseBam)],
    ['Razlika', fmtMoney(b.netBam)],
  ])

  table(
    doc, cur, 'Po pčelinjaku', ['Pčelinjak', 'kg', 'Prihod', 'Trošak', 'Razlika'],
    b.byApiary.map(a => [
      a.apiaryName, fmtKg(a.kg), fmtMoney(a.estimatedRevenueBam), fmtMoney(a.expenseBam), fmtMoney(a.netBam),
    ]),
    [62, 22, 30, 30, 30],
  )
}

function drawTreatments(doc: jsPDF, report: SeasonReport, cur: Cursor) {
  const t = report.treatments

  sectionTitle(doc, cur, 'TRETMANI')
  keyValues(doc, cur, [
    ['Broj tretmana', String(t.count)],
    ['Tretirano košnica', String(t.hivesTreated)],
    ['U karenci', String(t.activeKarencaCount)],
  ])

  table(
    doc, cur, 'Po preparatu', ['Preparat', 'Aktivna tvar', 'Tretmana', 'Košnica'],
    t.byProduct.map(p => [p.productName, p.activeSubstanceName, String(p.treatmentCount), String(p.hiveCount)]),
    [64, 56, 28, 26],
  )

  // The register itself is a separate document per apiary and year (SPEC-08); duplicating it here
  // would be a second copy that can disagree with the first.
  note(doc, cur, 'Detaljna evidencija tretmana izvozi se zasebno, po pčelinjaku i godini.')
}

function drawNotes(doc: jsPDF, report: SeasonReport, cur: Cursor) {
  const n = report.notes
  const lines: string[] = []

  // The one number in this document that would otherwise lie by omission (SPEC-25 D6).
  if (n.unpricedKg > 0)
    lines.push(
      `Prihod je procjena: ${fmtKg(n.unpricedKg)} kg nema upisanu cijenu i nije uračunato u prihod.`,
    )
  if (n.unassignedExpenseCount > 0)
    lines.push(
      `${n.unassignedExpenseCount} ${plural(n.unassignedExpenseCount, 'račun', 'računa', 'računa')} ` +
      'nije vezano ni za jedan pčelinjak i prikazano je kao zajednički trošak.',
    )
  if (n.nonBamCurrencies.length > 0)
    lines.push(
      `Bilansa je iskazana samo u KM. Troškovi u drugim valutama (${n.nonBamCurrencies.join(', ')}) ` +
      'prikazani su odvojeno i nisu oduzeti od prihoda.',
    )

  if (lines.length === 0) return

  sectionTitle(doc, cur, 'NAPOMENE')
  doc.setFontSize(8)
  for (const text of lines) {
    const wrapped = doc.splitTextToSize(`• ${text}`, CONTENT_W)
    ensureSpace(doc, cur, wrapped.length * 4 + 2)
    doc.text(wrapped, MARGIN, cur.y + 3)
    cur.y += wrapped.length * 4 + 2
  }
  cur.y += 2
}

// Reserves what the block actually occupies: 8 mm of air, the rule, then the caption baseline
// 4 mm below it — 20 mm with a little headroom, not the 26 mm first guessed. Over-reserving pushed
// the signature onto a page of its own whenever the content happened to end within 6 mm of the
// limit, which is a whole blank page in a document someone prints and hands in.
const SIGNATURE_H = 20

function drawSignature(doc: jsPDF, cur: Cursor) {
  ensureSpace(doc, cur, SIGNATURE_H)
  cur.y += 8
  doc.setFontSize(8)
  doc.setDrawColor(160)
  doc.line(MARGIN, cur.y, MARGIN + 60, cur.y)
  doc.line(PAGE_W - MARGIN - 60, cur.y, PAGE_W - MARGIN, cur.y)
  doc.text('Mjesto i datum', MARGIN, cur.y + 4)
  doc.text('Potpis i pečat', PAGE_W - MARGIN, cur.y + 4, { align: 'right' })
  cur.y += 10
}

// ── Drawing primitives ─────────────────────────────────────────────────────────

function sectionTitle(doc: jsPDF, cur: Cursor, title: string) {
  ensureSpace(doc, cur, 14)
  cur.y += 2
  doc.setFontSize(11)
  doc.setFillColor(...HONEY)
  doc.rect(MARGIN, cur.y, CONTENT_W, 7, 'F')
  doc.text(title, MARGIN + 2, cur.y + 5)
  cur.y += 10
}

function keyValues(doc: jsPDF, cur: Cursor, rows: Array<[string, string]>) {
  doc.setFontSize(9)
  for (const [label, value] of rows) {
    ensureSpace(doc, cur, 6)
    doc.text(label, MARGIN + 1, cur.y + 3)
    doc.text(value, PAGE_W - MARGIN - 1, cur.y + 3, { align: 'right' })
    cur.y += 5.5
  }
  cur.y += 2
}

function table(
  doc: jsPDF,
  cur: Cursor,
  caption: string,
  head: string[],
  rows: string[][],
  widths?: number[],
) {
  const cols = widths ?? defaultWidths(head.length)

  ensureSpace(doc, cur, 18)
  doc.setFontSize(9)
  doc.text(caption, MARGIN + 1, cur.y + 3)
  cur.y += 6

  if (rows.length === 0) {
    doc.setFontSize(8)
    doc.setTextColor(140)
    doc.text('Nema podataka za ovaj period.', MARGIN + 1, cur.y + 3)
    doc.setTextColor(0)
    cur.y += 7
    return
  }

  drawHeadRow(doc, cur, head, cols)

  rows.forEach((row, i) => {
    if (cur.y + 6 > BOTTOM) {
      newPage(doc, cur)
      drawHeadRow(doc, cur, head, cols)
    }
    if (i % 2 === 1) {
      doc.setFillColor(...STRIPE)
      doc.rect(MARGIN, cur.y, sum(cols), 6, 'F')
    }
    doc.setFontSize(8)
    let x = MARGIN
    row.forEach((cell, c) => {
      // Numeric columns are every column but the first — right-aligned so decimals line up.
      const text = doc.splitTextToSize(cell, cols[c] - 2)[0] ?? ''
      if (c === 0) doc.text(text, x + 1, cur.y + 4)
      else doc.text(text, x + cols[c] - 1, cur.y + 4, { align: 'right' })
      x += cols[c]
    })
    cur.y += 6
  })
  cur.y += 4
}

function drawHeadRow(doc: jsPDF, cur: Cursor, head: string[], cols: number[]) {
  doc.setFontSize(8)
  doc.setDrawColor(190)
  doc.line(MARGIN, cur.y, MARGIN + sum(cols), cur.y)
  let x = MARGIN
  head.forEach((h, i) => {
    if (i === 0) doc.text(h, x + 1, cur.y + 4)
    else doc.text(h, x + cols[i] - 1, cur.y + 4, { align: 'right' })
    x += cols[i]
  })
  cur.y += 6
  doc.line(MARGIN, cur.y, MARGIN + sum(cols), cur.y)
}

function note(doc: jsPDF, cur: Cursor, text: string) {
  const wrapped = doc.splitTextToSize(text, CONTENT_W)
  ensureSpace(doc, cur, wrapped.length * 4 + 3)
  doc.setFontSize(7.5)
  doc.setTextColor(120)
  doc.text(wrapped, MARGIN + 1, cur.y + 3)
  doc.setTextColor(0)
  cur.y += wrapped.length * 4 + 3
}

/** One line of header text at the current cursor, advancing it. */
function line(doc: jsPDF, cur: Cursor, text: string) {
  ensureSpace(doc, cur, 6)
  doc.text(text, MARGIN, cur.y + 3)
  cur.y += 5
}

function ensureSpace(doc: jsPDF, cur: Cursor, needed: number) {
  if (cur.y + needed > BOTTOM) newPage(doc, cur)
}

function newPage(doc: jsPDF, cur: Cursor) {
  doc.addPage()
  doc.setFont(FONT)
  cur.y = MARGIN
}

function addFooters(doc: jsPDF) {
  const total = doc.getNumberOfPages()
  for (let p = 1; p <= total; p++) {
    doc.setPage(p)
    doc.setFont(FONT)
    doc.setFontSize(7)
    doc.setTextColor(140)
    doc.text('Melarium', MARGIN, PAGE_H - 8)
    doc.text(`Strana ${p} / ${total}`, PAGE_W - MARGIN, PAGE_H - 8, { align: 'right' })
    doc.setTextColor(0)
  }
}

// ── Helpers ────────────────────────────────────────────────────────────────────

function defaultWidths(count: number): number[] {
  if (count === 2) return [CONTENT_W - 45, 45]
  const first = CONTENT_W - 40 * (count - 1)
  return [first, ...Array(count - 1).fill(40)]
}

const sum = (ns: number[]) => ns.reduce((a, b) => a + b, 0)

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

/** Bosnian has three plural forms; the usual `n === 1 ? a : b` produces "2 računa" wrongly as "2 račun". */
function plural(n: number, one: string, few: string, many: string): string {
  const mod10 = n % 10
  const mod100 = n % 100
  if (mod10 === 1 && mod100 !== 11) return one
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few
  return many
}
