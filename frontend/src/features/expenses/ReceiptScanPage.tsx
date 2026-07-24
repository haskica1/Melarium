import { useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { AlertCircle, Camera, CheckCircle2, Loader2, Plus, ScanLine, Trash2, Upload } from 'lucide-react'
import { ExpenseSource } from '../../core/models'
import type { CreateExpenseItemPayload } from '../../core/models'
import { FormHeader } from '../../shared/components'

// Tesseract.js is loaded lazily to avoid pulling the WASM bundle unless the user visits this page
async function runOcr(imageFile: File): Promise<string> {
  const { createWorker } = await import('tesseract.js')
  // 'hrv' reads š/č/ž/đ on local receipts correctly; the 'eng' model mangles them.
  const worker = await createWorker('hrv')
  const { data } = await worker.recognize(imageFile)
  await worker.terminate()
  return data.text
}

// ── Simple receipt text parser ────────────────────────────────────────────────
// Looks for lines that contain a product name followed by a decimal price.
// Not perfect — designed to be edited by the user after scanning.
function parseReceiptText(text: string): CreateExpenseItemPayload[] {
  const lines = text.split('\n').map(l => l.trim()).filter(Boolean)
  const items: CreateExpenseItemPayload[] = []

  // Pattern: anything followed by optional qty info and a price like 1.23 or 1,23
  const pricePattern = /(\d+[.,]\d{2})\s*(?:BAM|KM|€|\$|EUR)?$/i

  lines.forEach((line, index) => {
    const match = line.match(pricePattern)
    if (!match) return

    const rawPrice = parseFloat(match[1].replace(',', '.'))
    if (isNaN(rawPrice) || rawPrice <= 0) return

    // Everything before the price is the product name
    const name = line.slice(0, line.lastIndexOf(match[0])).trim().replace(/\s+/g, ' ')
    if (!name || name.length < 2) return

    // Attempt to detect quantity pattern like "25 kg" or "3x" before the price
    let quantity = 1
    let unit = ''
    const qtyMatch = name.match(/(\d+(?:[.,]\d+)?)\s*(kg|g|l|ml|pcs|kom|pc|x)\b/i)
    if (qtyMatch) {
      quantity = parseFloat(qtyMatch[1].replace(',', '.'))
      unit = qtyMatch[2].toLowerCase()
    }

    items.push({
      name: name.replace(/^\d+\s*[x×]\s*/i, '').trim() || name,
      quantity,
      unit: unit || undefined,
      unitPrice: quantity > 1 ? parseFloat((rawPrice / quantity).toFixed(4)) : rawPrice,
      totalPrice: rawPrice,
      sortOrder: index,
    })
  })

  return items.slice(0, 30) // Cap at 30 items to avoid noise
}

type Phase = 'capture' | 'processing' | 'review'

export default function ReceiptScanPage() {
  const navigate = useNavigate()
  const fileInputRef = useRef<HTMLInputElement>(null)

  const [phase, setPhase] = useState<Phase>('capture')
  const [imagePreview, setImagePreview] = useState<string | null>(null)
  const [ocrError, setOcrError] = useState<string | null>(null)
  const [reviewItems, setReviewItems] = useState<CreateExpenseItemPayload[]>([])

  async function handleFileSelected(file: File) {
    setOcrError(null)
    setImagePreview(URL.createObjectURL(file))
    setPhase('processing')

    try {
      const text = await runOcr(file)
      const parsed = parseReceiptText(text)

      if (parsed.length === 0) {
        // No items found — start with one empty row so the user can fill in manually
        setReviewItems([emptyItem(0)])
      } else {
        setReviewItems(parsed)
      }
      setPhase('review')
    } catch {
      setOcrError('OCR nije uspio. Možete ručno dodati stavke.')
      setReviewItems([emptyItem(0)])
      setPhase('review')
    }
  }

  function updateItem(index: number, field: keyof CreateExpenseItemPayload, value: string | number | undefined) {
    setReviewItems(prev => {
      const next = [...prev]
      next[index] = { ...next[index], [field]: value }
      return next
    })
  }

  function addItem() {
    setReviewItems(prev => [...prev, emptyItem(prev.length)])
  }

  function removeItem(index: number) {
    setReviewItems(prev => prev.filter((_, i) => i !== index))
  }

  function handleConfirm() {
    const validItems = reviewItems
      .filter(i => i.name.trim())
      .map((item, idx) => ({ ...item, sortOrder: idx }))

    navigate('/expenses/new', {
      state: { items: validItems, source: ExpenseSource.ReceiptScan },
    })
  }

  return (
    <div className="max-w-2xl mx-auto">
      <FormHeader
        icon="📷"
        title="Skeniraj račun"
        subtitle="Fotografirajte ili učitajte račun — stavke se automatski prepoznaju."
        onBack={() => navigate('/expenses')}
        backLabel="Nazad na troškove"
      />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-8 py-8 space-y-6">

        {/* ── Phase: capture ───────────────────────────────────────────── */}
        {phase === 'capture' && (
          <div className="flex flex-col items-center gap-4 py-8">
            <div className="w-20 h-20 rounded-2xl bg-honey-50 dark:bg-honey-500/15 flex items-center justify-center">
              <ScanLine className="w-10 h-10 text-honey-500" />
            </div>
            <p className="text-sm text-gray-500 dark:text-slate-400 text-center max-w-xs">
              Koristite kameru za fotografisanje računa ili učitajte postojeću sliku.
            </p>
            <div className="flex items-center gap-3">
              <button
                onClick={() => {
                  if (fileInputRef.current) {
                    fileInputRef.current.accept = 'image/*'
                    fileInputRef.current.capture = 'environment'
                    fileInputRef.current.click()
                  }
                }}
                className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
              >
                <Camera className="w-4 h-4" />
                Otvori kameru
              </button>
              <button
                onClick={() => {
                  if (fileInputRef.current) {
                    fileInputRef.current.removeAttribute('capture')
                    fileInputRef.current.accept = 'image/*'
                    fileInputRef.current.click()
                  }
                }}
                className="flex items-center gap-2 px-5 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-gray-700 dark:text-slate-200 text-sm font-medium hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
              >
                <Upload className="w-4 h-4" />
                Učitaj sliku
              </button>
            </div>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              className="hidden"
              onChange={e => {
                const file = e.target.files?.[0]
                if (file) handleFileSelected(file)
              }}
            />
          </div>
        )}

        {/* ── Phase: processing ────────────────────────────────────────── */}
        {phase === 'processing' && (
          <div className="flex flex-col items-center gap-4 py-10">
            {imagePreview && (
              <img
                src={imagePreview}
                alt="Receipt preview"
                className="max-h-48 rounded-xl object-contain border border-gray-200 dark:border-slate-700"
              />
            )}
            <div className="flex items-center gap-2 text-gray-500 dark:text-slate-400">
              <Loader2 className="w-5 h-5 animate-spin text-honey-500" />
              <span className="text-sm">Čitanje računa…</span>
            </div>
            <p className="text-xs text-gray-400 dark:text-slate-500 text-center max-w-xs">
              Ovo može potrajati nekoliko sekundi. Prva skeniranja učitavaju OCR engine.
            </p>
          </div>
        )}

        {/* ── Phase: review ────────────────────────────────────────────── */}
        {phase === 'review' && (
          <div className="space-y-5">
            {imagePreview && (
              <div className="flex items-center gap-3">
                <img
                  src={imagePreview}
                  alt="Receipt preview"
                  className="w-14 h-14 rounded-xl object-cover border border-gray-200 dark:border-slate-700"
                />
                <div>
                  {ocrError ? (
                    <div className="flex items-center gap-1.5 text-amber-600 text-sm">
                      <AlertCircle className="w-4 h-4" />
                      {ocrError}
                    </div>
                  ) : (
                    <div className="flex items-center gap-1.5 text-green-600 text-sm">
                      <CheckCircle2 className="w-4 h-4" />
                      Račun skeniran — pregledajte stavke ispod.
                    </div>
                  )}
                  <button
                    onClick={() => setPhase('capture')}
                    className="text-xs text-gray-400 dark:text-slate-500 hover:text-gray-600 dark:hover:text-slate-300 mt-0.5 transition-colors"
                  >
                    Skeniraj ponovo
                  </button>
                </div>
              </div>
            )}

            {/* Column headers */}
            <div className="grid grid-cols-[2fr_1fr_0.8fr_1fr_1fr_auto] gap-2 px-1">
              {['Proizvod', 'Kol.', 'Jed.', 'Jed. cijena', 'Ukupno', ''].map(h => (
                <span key={h} className="text-xs font-medium text-gray-400 dark:text-slate-500">{h}</span>
              ))}
            </div>

            <div className="space-y-2">
              {reviewItems.map((item, index) => (
                <div key={index} className="grid grid-cols-[2fr_1fr_0.8fr_1fr_1fr_auto] gap-2 items-center">
                  <input
                    type="text"
                    value={item.name}
                    onChange={e => updateItem(index, 'name', e.target.value)}
                    placeholder="Naziv proizvoda"
                    className="px-3 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all"
                  />
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={item.quantity}
                    onChange={e => updateItem(index, 'quantity', parseFloat(e.target.value) || 0)}
                    className="px-3 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all"
                  />
                  <input
                    type="text"
                    value={item.unit ?? ''}
                    onChange={e => updateItem(index, 'unit', e.target.value || undefined)}
                    placeholder="kg"
                    className="px-3 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all"
                  />
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={item.unitPrice}
                    onChange={e => updateItem(index, 'unitPrice', parseFloat(e.target.value) || 0)}
                    className="px-3 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all"
                  />
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={item.totalPrice}
                    onChange={e => updateItem(index, 'totalPrice', parseFloat(e.target.value) || 0)}
                    className="px-3 py-2 rounded-lg border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-1 focus:ring-honey-100 transition-all"
                  />
                  <button
                    type="button"
                    onClick={() => removeItem(index)}
                    disabled={reviewItems.length === 1}
                    className="p-1.5 rounded-lg text-gray-300 dark:text-slate-600 hover:text-red-400 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-30"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              ))}
            </div>

            <button
              type="button"
              onClick={addItem}
              className="flex items-center gap-1.5 text-sm text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300 font-medium transition-colors"
            >
              <Plus className="w-4 h-4" />
              Dodaj stavku
            </button>

            {/* Actions */}
            <div className="flex gap-3 pt-2 border-t border-gray-100 dark:border-slate-800">
              <button
                type="button"
                onClick={() => navigate('/expenses')}
                className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
              >
                Otkaži
              </button>
              <button
                type="button"
                onClick={handleConfirm}
                disabled={reviewItems.filter(i => i.name.trim()).length === 0}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors"
              >
                Nastavi na čuvanje
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

function emptyItem(sortOrder: number): CreateExpenseItemPayload {
  return { name: '', quantity: 1, unit: undefined, unitPrice: 0, totalPrice: 0, sortOrder }
}
