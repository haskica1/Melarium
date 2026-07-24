import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { X, AlertCircle, Camera, Upload, Loader2, QrCode, Hash, SearchX, ChevronRight } from 'lucide-react'
import { BrowserQRCodeReader, IScannerControls } from '@zxing/browser'
import { beehiveService } from '../../core/services/beehiveService'
import { downscaleForScan } from '../utils/imageDownscale'
import type { BeehiveNumberMatchResult } from '../../core/models'

interface Props {
  onClose: () => void
}

type Tab = 'qr' | 'number'
type NumberState = 'idle' | 'recognizing' | 'results' | 'error'

// On-device OCR is confident enough to skip the paid Groq fallback above this (0–100).
const LOCAL_CONFIDENCE_THRESHOLD = 60

function extractUniqueId(text: string): string | null {
  // Handle full URLs: https://example.com/scan/abc123
  try {
    const url = new URL(text)
    const match = url.pathname.match(/\/scan\/([^/]+)/)
    if (match) return match[1]
  } catch {}
  // Handle relative paths: /scan/abc123
  const pathMatch = text.match(/\/scan\/([^/]+)/)
  if (pathMatch) return pathMatch[1]
  // Raw UUID
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(text)) return text
  return null
}

// Tesseract.js is loaded lazily — the WASM bundle only downloads when someone scans a number.
async function runDigitOcr(image: Blob): Promise<{ number: string | null; confidence: number }> {
  const { createWorker } = await import('tesseract.js')
  const worker = await createWorker('eng')
  try {
    // Hive numbers are digits — whitelisting them sharply improves accuracy on painted labels.
    await worker.setParameters({ tessedit_char_whitelist: '0123456789' })
    const { data } = await worker.recognize(image)

    // Prefer the tallest digit-only word. "Longest run" picks a small "2024" on a sticker over a
    // big painted "7", and `data.confidence` is the mean across the whole page — any other text in
    // frame drags it to near zero even when the number itself was read perfectly, so score the
    // chosen word on its own confidence instead.
    const digitWords = (data.words ?? [])
      .map(w => ({ text: w.text.trim(), confidence: w.confidence ?? 0, height: w.bbox.y1 - w.bbox.y0 }))
      .filter(w => /^\d+$/.test(w.text))
      .sort((a, b) => b.height - a.height)

    if (digitWords.length > 0)
      return { number: digitWords[0].text, confidence: digitWords[0].confidence }

    // No word-level data — fall back to scanning the flat text.
    const groups = data.text.match(/\d+/g) ?? []
    const number = [...groups].sort((a, b) => b.length - a.length)[0] ?? null
    return { number, confidence: data.confidence ?? 0 }
  } finally {
    await worker.terminate()
  }
}

export default function QrScannerModal({ onClose }: Props) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const controlsRef = useRef<IScannerControls | null>(null)
  const cameraInputRef = useRef<HTMLInputElement>(null)
  const uploadInputRef = useRef<HTMLInputElement>(null)
  const navigate = useNavigate()

  const [tab, setTab] = useState<Tab>('qr')
  const [qrError, setQrError] = useState<string | null>(null)

  const [numberState, setNumberState] = useState<NumberState>('idle')
  const [numberError, setNumberError] = useState<string | null>(null)
  const [result, setResult] = useState<BeehiveNumberMatchResult | null>(null)

  // Live QR decoding — only while the QR tab is active, so the camera is released on the number tab.
  useEffect(() => {
    if (tab !== 'qr') return

    const reader = new BrowserQRCodeReader()
    let cancelled = false
    let controls: IScannerControls | null = null

    reader
      .decodeFromVideoDevice(undefined, videoRef.current!, (res) => {
        if (cancelled || !res) return
        const uniqueId = extractUniqueId(res.getText())
        if (uniqueId) {
          cancelled = true
          controls?.stop()
          onClose()
          navigate(`/scan/${uniqueId}`)
        }
      })
      .then((c) => {
        controls = c
        controlsRef.current = c
        if (cancelled) c.stop()
      })
      .catch(() => {
        if (!cancelled)
          setQrError('Pristup kameri odbijen. Molimo dozvolite pristup kameri i pokušajte ponovo.')
      })

    return () => {
      cancelled = true
      controls?.stop()
      controlsRef.current?.stop()
    }
  }, [tab, navigate, onClose])

  function openHive(id: number) {
    onClose()
    navigate(`/beehives/${id}`)
  }

  async function handleImage(captured: Blob) {
    setNumberError(null)
    setResult(null)
    setNumberState('recognizing')

    try {
      // A raw phone photo is 12 MP and several MB — too slow for Tesseract and over the vision
      // endpoint's size cap. Both passes below work off the bounded re-encode.
      const image = await downscaleForScan(captured)

      // 1. On-device OCR (free, offline-capable). Its WASM core and language data come from a CDN,
      //    so a flaky mobile connection can sink it — that must degrade to the AI pass below, not
      //    fail the whole scan.
      let local = { number: null as string | null, confidence: 0 }
      try {
        local = await runDigitOcr(image)
      } catch {
        // keep the empty result — treated exactly like "read nothing"
      }

      let matched: BeehiveNumberMatchResult | null = null
      let uncertain = false
      if (local.number && local.confidence >= LOCAL_CONFIDENCE_THRESHOLD) {
        matched = await beehiveService.resolveByNumber(local.number)
      }

      // 2. Fall back to the Groq vision model when on-device was unsure or found nothing.
      if (!matched || matched.matches.length === 0) {
        try {
          const groq = await beehiveService.scanByNumber(image)
          if (groq.matches.length > 0 || !matched) matched = groq
        } catch (err) {
          // The AI pass is optional — it is unavailable whenever Groq:ApiKey is unset, the rate
          // limit is hit, or the upstream call fails. A number Tesseract read below the confidence
          // bar still beats an error screen, so resolve it and let the user confirm from the list.
          if (!matched && local.number) {
            matched = await beehiveService.resolveByNumber(local.number)
            uncertain = true
          }
          if (!matched) throw err
        }
      }

      // Exactly one hive → open it straight away (per the "auto-open single match" choice), unless
      // the number came from a reading we did not trust — then the user confirms it first.
      if (matched.matches.length === 1 && !uncertain) {
        openHive(matched.matches[0].id)
        return
      }

      setResult(matched)
      setNumberState('results')
    } catch (err: any) {
      // apiClient's interceptor already reduces a failed call to an Error carrying the server's
      // message, so `err.response` never survives to here.
      setNumberError(err?.message ?? 'Prepoznavanje nije uspjelo. Pokušajte ponovo.')
      setNumberState('error')
    }
  }

  function onFilePicked(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = '' // allow re-picking the same file
    if (file) handleImage(file)
  }

  function resetNumber() {
    setNumberState('idle')
    setNumberError(null)
    setResult(null)
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center bg-black/75"
      onClick={onClose}
    >
      <div
        className="relative w-full max-w-sm mx-4 mb-4 sm:mb-0 rounded-2xl overflow-hidden bg-slate-900 shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Tabs + close */}
        <div className="flex items-center justify-between gap-2 px-3 pt-3 pb-2">
          <div className="flex gap-1 bg-black/40 rounded-full p-1 backdrop-blur-sm">
            <TabButton active={tab === 'qr'} onClick={() => setTab('qr')} icon={<QrCode className="w-4 h-4" />} label="QR kod" />
            <TabButton active={tab === 'number'} onClick={() => setTab('number')} icon={<Hash className="w-4 h-4" />} label="Broj" />
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-full bg-black/60 text-white hover:bg-black/80 transition-colors backdrop-blur-sm"
            aria-label="Zatvori skener"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* ── QR tab ─────────────────────────────────────────────────────────── */}
        {tab === 'qr' && (
          qrError ? (
            <div className="flex flex-col items-center justify-center gap-4 p-10 text-center min-h-[320px]">
              <AlertCircle className="w-10 h-10 text-red-400" />
              <p className="text-white text-sm leading-relaxed">{qrError}</p>
              <button
                onClick={onClose}
                className="mt-2 px-5 py-2 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
              >
                Zatvori
              </button>
            </div>
          ) : (
            <div className="relative">
              <video ref={videoRef} className="w-full aspect-square object-cover bg-black" muted playsInline />

              {/* Scanning frame overlay */}
              <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                <div className="absolute inset-0 bg-black/40" />
                <div className="relative z-10 w-52 h-52 bg-transparent">
                  <div className="absolute top-0 left-0 w-10 h-10 border-t-[3px] border-l-[3px] border-honey-400 rounded-tl" />
                  <div className="absolute top-0 right-0 w-10 h-10 border-t-[3px] border-r-[3px] border-honey-400 rounded-tr" />
                  <div className="absolute bottom-0 left-0 w-10 h-10 border-b-[3px] border-l-[3px] border-honey-400 rounded-bl" />
                  <div className="absolute bottom-0 right-0 w-10 h-10 border-b-[3px] border-r-[3px] border-honey-400 rounded-br" />
                  <div className="absolute inset-x-0 h-0.5 bg-honey-400/70 animate-scan-line" />
                </div>
              </div>

              <div className="absolute bottom-4 inset-x-0 flex justify-center pointer-events-none">
                <span className="bg-black/60 text-white/80 text-xs px-4 py-1.5 rounded-full backdrop-blur-sm">
                  Usmjerite kameru prema QR kodu košnice
                </span>
              </div>
            </div>
          )
        )}

        {/* ── Number tab ─────────────────────────────────────────────────────── */}
        {tab === 'number' && (
          <div className="p-6 min-h-[320px] flex flex-col">
            {/* Hidden inputs: camera capture (mobile) + gallery/upload fallback */}
            <input ref={cameraInputRef} type="file" accept="image/*" capture="environment" className="hidden" onChange={onFilePicked} />
            <input ref={uploadInputRef} type="file" accept="image/*" className="hidden" onChange={onFilePicked} />

            {numberState === 'idle' && (
              <div className="flex-1 flex flex-col items-center justify-center text-center gap-5">
                <div className="w-16 h-16 rounded-full bg-honey-500/15 flex items-center justify-center">
                  <Hash className="w-8 h-8 text-honey-400" />
                </div>
                <div>
                  <p className="text-white font-semibold">Slikaj broj košnice</p>
                  <p className="text-white/60 text-sm mt-1 leading-relaxed">
                    Fotografišite broj naslikan na košnici — otvorićemo košnicu s tim brojem u nazivu.
                  </p>
                </div>
                <div className="w-full space-y-2">
                  <button
                    onClick={() => cameraInputRef.current?.click()}
                    className="w-full flex items-center justify-center gap-2 py-2.5 px-4 rounded-xl bg-honey-500 hover:bg-honey-600 text-white font-semibold text-sm transition-colors"
                  >
                    <Camera className="w-4 h-4" /> Slikaj košnicu
                  </button>
                  <button
                    onClick={() => uploadInputRef.current?.click()}
                    className="w-full flex items-center justify-center gap-2 py-2.5 px-4 rounded-xl bg-white/10 hover:bg-white/20 text-white/90 font-medium text-sm transition-colors"
                  >
                    <Upload className="w-4 h-4" /> Učitaj sliku
                  </button>
                </div>
              </div>
            )}

            {numberState === 'recognizing' && (
              <div className="flex-1 flex flex-col items-center justify-center gap-4 text-center">
                <Loader2 className="w-10 h-10 text-honey-400 animate-spin" />
                <p className="text-white/80 text-sm">Prepoznajem broj…</p>
              </div>
            )}

            {numberState === 'results' && result && (
              <div className="flex-1 flex flex-col">
                {result.matches.length === 0 ? (
                  <div className="flex-1 flex flex-col items-center justify-center gap-4 text-center">
                    <SearchX className="w-10 h-10 text-amber-400" />
                    <p className="text-white text-sm leading-relaxed">
                      {result.recognizedNumber
                        ? `Prepoznat broj „${result.recognizedNumber}", ali nijedna vaša košnica nema taj broj.`
                        : 'Nismo uspjeli pročitati broj sa slike. Pokušajte s jasnijom fotografijom.'}
                    </p>
                    <button
                      onClick={resetNumber}
                      className="mt-1 px-5 py-2 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
                    >
                      Pokušaj ponovo
                    </button>
                  </div>
                ) : (
                  <>
                    <p className="text-white/70 text-xs mb-3">
                      {result.recognizedNumber && <>Broj „{result.recognizedNumber}" — </>}
                      pronađeno {result.matches.length} košnica. Odaberite:
                    </p>
                    <div className="space-y-2 overflow-y-auto max-h-64 -mx-1 px-1">
                      {result.matches.map((m) => (
                        <button
                          key={m.id}
                          onClick={() => openHive(m.id)}
                          className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/5 hover:bg-white/10 border border-white/10 text-left transition-colors"
                        >
                          <span className="shrink-0 w-9 h-9 rounded-lg bg-honey-500/20 text-honey-300 font-bold text-sm flex items-center justify-center">
                            {m.labelNumber ?? '#'}
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className="block text-white text-sm font-semibold truncate">{m.name}</span>
                            {m.apiaryName && <span className="block text-white/50 text-xs truncate">{m.apiaryName}</span>}
                          </span>
                          <ChevronRight className="w-4 h-4 text-white/40 shrink-0" />
                        </button>
                      ))}
                    </div>
                    <button
                      onClick={resetNumber}
                      className="mt-3 w-full py-2 rounded-xl bg-white/10 hover:bg-white/20 text-white/80 text-sm font-medium transition-colors"
                    >
                      Slikaj ponovo
                    </button>
                  </>
                )}
              </div>
            )}

            {numberState === 'error' && (
              <div className="flex-1 flex flex-col items-center justify-center gap-4 text-center">
                <AlertCircle className="w-10 h-10 text-red-400" />
                <p className="text-white text-sm leading-relaxed">{numberError}</p>
                <button
                  onClick={resetNumber}
                  className="mt-1 px-5 py-2 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
                >
                  Pokušaj ponovo
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

function TabButton({ active, onClick, icon, label }: { active: boolean; onClick: () => void; icon: React.ReactNode; label: string }) {
  return (
    <button
      onClick={onClick}
      className={
        'flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium transition-colors ' +
        (active ? 'bg-honey-500 text-white' : 'text-white/70 hover:text-white')
      }
    >
      {icon}
      {label}
    </button>
  )
}
