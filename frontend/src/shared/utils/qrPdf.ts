import { jsPDF } from 'jspdf'

/** Minimal data a QR card needs — satisfied by both BeehiveDetail and BeehiveQr. */
export interface QrCardData {
  name: string
  uniqueId?: string
  qrCodeBase64?: string
}

// ── QR PDF — one beehive = one self-contained card that fills the whole page ───
//
// Layout (top → bottom), everything centered, each on a single line:
//   1. Name      (bold, font auto-scaled to card width & height)
//   2. QR code   (square, centered)
//   3. Unique ID (monospace, font auto-scaled to card width & height)
//
// The PDF page itself is sized to the dimensions the user enters, so the page
// *is* the card — ideal for printing labels. Fonts shrink/grow so the text
// always fits the card and never wraps to a second line.

const PT_TO_MM = 25.4 / 72

/** Largest font size (pt) at which `text` fits within both `maxWidthMm` and `maxHeightMm`. */
function fitFontSize(doc: jsPDF, text: string, maxWidthMm: number, maxHeightMm: number, minPt = 2): number {
  if (!text) return minPt
  const byHeight = maxHeightMm / PT_TO_MM
  // getTextWidth scales linearly with font size, so measure once at a reference size.
  const ref = 20
  doc.setFontSize(ref)
  const widthAtRef = doc.getTextWidth(text)
  const byWidth = widthAtRef > 0 ? ref * (maxWidthMm / widthAtRef) : byHeight
  return Math.max(minPt, Math.min(byHeight, byWidth))
}

/** Draws a single beehive card filling the current (already card-sized) page. */
function drawCard(doc: jsPDF, beehive: QrCardData) {
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()

  const pad = Math.max(2, Math.min(W, H) * 0.06)
  const iw = W - pad * 2 // inner width
  const ih = H - pad * 2 // inner height

  // Vertical budget as fractions of the inner height.
  const nameH = ih * 0.16
  const idH = ih * 0.12
  const gap = ih * 0.05
  const qrBand = ih - nameH - idH - gap * 2
  const qrSize = Math.max(0, Math.min(iw, qrBand)) // QR is square

  // Subtle rounded card outline so the label boundary is visible when printed.
  doc.setDrawColor(225, 210, 180)
  doc.setLineWidth(0.3)
  const r = Math.min(W, H) * 0.04
  doc.roundedRect(pad * 0.4, pad * 0.4, W - pad * 0.8, H - pad * 0.8, r, r, 'S')

  let y = pad

  // 1 ── Name ──
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(fitFontSize(doc, beehive.name, iw, nameH))
  doc.setTextColor(40, 40, 40)
  doc.text(beehive.name, W / 2, y + nameH / 2, { align: 'center', baseline: 'middle' })
  y += nameH + gap

  // 2 ── QR code ──
  if (beehive.qrCodeBase64) {
    const qrX = (W - qrSize) / 2
    const qrY = y + (qrBand - qrSize) / 2
    doc.addImage(`data:image/png;base64,${beehive.qrCodeBase64}`, 'PNG', qrX, qrY, qrSize, qrSize)
  }
  y += qrBand + gap

  // 3 ── Unique ID ──
  const uid = beehive.uniqueId ?? ''
  doc.setFont('courier', 'normal')
  doc.setFontSize(fitFontSize(doc, uid, iw, idH))
  doc.setTextColor(90, 90, 90)
  doc.text(uid, W / 2, y + idH / 2, { align: 'center', baseline: 'middle' })
}

/** Page size + orientation that yield exactly `w × h` mm (jsPDF reorders to match orientation). */
function pageSpec(sizeMm: { w: number; h: number }) {
  return {
    format: [sizeMm.w, sizeMm.h] as [number, number],
    orientation: (sizeMm.w > sizeMm.h ? 'landscape' : 'portrait') as 'landscape' | 'portrait',
  }
}

/** Download a single-beehive QR card PDF. */
export function downloadBeehiveQrPdf(beehive: QrCardData, sizeMm: { w: number; h: number }) {
  if (!beehive.qrCodeBase64 || !beehive.uniqueId) return
  const { format, orientation } = pageSpec(sizeMm)
  const doc = new jsPDF({ unit: 'mm', format, orientation })
  drawCard(doc, beehive)
  doc.save(`beehive-${(beehive.name || 'qr').replace(/\s+/g, '-')}-qr.pdf`)
}

/** Download one QR card per beehive — one card = one page, all pages the same card size. */
export function downloadBeehivesQrPdf(filename: string, beehives: QrCardData[], sizeMm: { w: number; h: number }) {
  const withQr = beehives.filter(b => b.qrCodeBase64 && b.uniqueId)
  if (!withQr.length) return
  const { format, orientation } = pageSpec(sizeMm)
  const doc = new jsPDF({ unit: 'mm', format, orientation })
  withQr.forEach((b, i) => {
    if (i > 0) doc.addPage(format, orientation)
    drawCard(doc, b)
  })
  doc.save(`${filename.replace(/\s+/g, '-')}-qr-kodovi.pdf`)
}
