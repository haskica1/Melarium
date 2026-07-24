import { jsPDF } from 'jspdf'
import type { Treatment } from '../../core/models'

// The legally-required treatment register (evidencija tretmana), rendered client-side. jsPDF's
// built-in fonts are cp1252 and lack č/ć/đ, so we embed DejaVu Sans (dynamic-imported ~1 MB base64
// module — kept out of the main bundle). A legal document with garbled diacritics is unacceptable.

export interface TreatmentPdfMeta {
  organizationName: string
  apiaryName: string
  year: number
  ownerName: string
}

const FONT = 'DejaVuSans'

interface Column { title: string; width: number; get: (t: Treatment, i: number) => string }

const COLUMNS: Column[] = [
  { title: '#',              width: 7,  get: (_t, i) => String(i + 1) },
  { title: 'Početak',        width: 18, get: t => fmtDate(t.startDate) },
  { title: 'Kraj',           width: 18, get: t => (t.endDate ? fmtDate(t.endDate) : 'u toku') },
  { title: 'Preparat',       width: 26, get: t => t.productName },
  { title: 'Aktivna tvar',   width: 24, get: t => t.activeSubstanceName },
  { title: 'Namjena',        width: 18, get: t => t.purposeName },
  { title: 'Način',          width: 22, get: t => t.methodName },
  { title: 'Doza',           width: 30, get: t => t.dosePerHive },
  { title: 'Košnice',        width: 30, get: t => (t.hiveCount === 0 ? '—' : `${t.hiveCount}: ${t.hiveNames.join(', ')}`) },
  { title: 'LOT',            width: 18, get: t => t.batchNumber || '—' },
  { title: 'Dobavljač',      width: 20, get: t => t.supplier || '—' },
  { title: 'Karenca (dana)', width: 12, get: t => String(t.withdrawalDays) },
  { title: 'Karenca ističe', width: 20, get: t => (t.endDate && t.withdrawalDays > 0 ? fmtDate(t.karencaUntil) : '—') },
]

const MARGIN = 10
const ROW_H = 12
const HEADER_ROW_H = 8
const PAGE_W = 297
const PAGE_H = 210
const BODY_FONT = 6.5

export async function downloadTreatmentRegisterPdf(treatments: Treatment[], meta: TreatmentPdfMeta) {
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })

  // Embed the BCS-capable font (lazy chunk).
  const { DEJAVU_SANS_BASE64 } = await import('./pdfFont')
  doc.addFileToVFS('DejaVuSans.ttf', DEJAVU_SANS_BASE64)
  doc.addFont('DejaVuSans.ttf', FONT, 'normal')
  doc.setFont(FONT)

  // Chronologically ascending (the register is a timeline).
  const rows = [...treatments].sort((a, b) => a.startDate.localeCompare(b.startDate))

  const totalCols = COLUMNS.reduce((s, c) => s + c.width, 0)
  let y = drawHeader(doc, meta)
  y = drawTableHead(doc, y)

  const bottom = PAGE_H - 12
  for (let i = 0; i < rows.length; i++) {
    if (y + ROW_H > bottom) {
      addFooter(doc)
      doc.addPage()
      doc.setFont(FONT)
      y = MARGIN
      y = drawTableHead(doc, y)
    }
    drawRow(doc, rows[i], i, y)
    y += ROW_H
  }
  if (rows.length === 0) {
    doc.setFontSize(9)
    doc.text('Nema zabilježenih tretmana za odabranu godinu.', MARGIN, y + 6)
  }
  addFooter(doc)

  // Faint outer border of the table block for a register look.
  void totalCols

  doc.save(`evidencija-tretmana-${slug(meta.apiaryName)}-${meta.year}.pdf`)
}

// ── Drawing ─────────────────────────────────────────────────────────────────────

function drawHeader(doc: jsPDF, meta: TreatmentPdfMeta): number {
  doc.setFontSize(14)
  doc.text('EVIDENCIJA TRETMANA', MARGIN, 14)
  doc.setFontSize(9)
  doc.text(`Pčelinjak: ${meta.apiaryName}      Godina: ${meta.year}`, MARGIN, 21)
  doc.text(`Organizacija: ${meta.organizationName}      Vlasnik: ${meta.ownerName}`, MARGIN, 26)
  doc.text(`Datum izrade: ${fmtDate(new Date().toISOString())}`, MARGIN, 31)
  return 36
}

function drawTableHead(doc: jsPDF, y: number): number {
  doc.setFontSize(7)
  doc.setFillColor(245, 232, 200)
  doc.rect(MARGIN, y, COLUMNS.reduce((s, c) => s + c.width, 0), HEADER_ROW_H, 'F')
  let x = MARGIN
  for (const col of COLUMNS) {
    doc.text(wrap(doc, col.title, col.width - 2, 7), x + 1, y + 5)
    doc.setDrawColor(210)
    doc.line(x, y, x, y + HEADER_ROW_H)
    x += col.width
  }
  doc.line(x, y, x, y + HEADER_ROW_H)
  doc.setDrawColor(180)
  doc.line(MARGIN, y, x, y)
  doc.line(MARGIN, y + HEADER_ROW_H, x, y + HEADER_ROW_H)
  return y + HEADER_ROW_H
}

function drawRow(doc: jsPDF, t: Treatment, i: number, y: number) {
  doc.setFontSize(BODY_FONT)
  const totalW = COLUMNS.reduce((s, c) => s + c.width, 0)
  if (i % 2 === 1) {
    doc.setFillColor(250, 246, 236)
    doc.rect(MARGIN, y, totalW, ROW_H, 'F')
  }
  let x = MARGIN
  for (const col of COLUMNS) {
    const lines = wrap(doc, col.get(t, i), col.width - 2, BODY_FONT).slice(0, 3)
    doc.text(lines, x + 1, y + 4)
    doc.setDrawColor(225)
    doc.line(x, y, x, y + ROW_H)
    x += col.width
  }
  doc.line(x, y, x, y + ROW_H)
  doc.setDrawColor(225)
  doc.line(MARGIN, y + ROW_H, x, y + ROW_H)
}

function addFooter(doc: jsPDF) {
  const page = doc.getNumberOfPages()
  doc.setFontSize(7)
  doc.setTextColor(140)
  doc.text(`Strana ${page}`, PAGE_W - MARGIN, PAGE_H - 6, { align: 'right' })
  doc.setTextColor(0)
}

// ── Helpers ─────────────────────────────────────────────────────────────────────

function wrap(doc: jsPDF, text: string, widthMm: number, fontSize: number): string[] {
  doc.setFontSize(fontSize)
  return doc.splitTextToSize(text ?? '', widthMm)
}

function fmtDate(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${p(d.getDate())}.${p(d.getMonth() + 1)}.${d.getFullYear()}.`
}

function slug(s: string): string {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '').toLowerCase() || 'pcelinjak'
}
