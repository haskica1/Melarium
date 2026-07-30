import { useState, useRef, useCallback, useEffect } from 'react'

export type VoiceInputState = 'idle' | 'recording' | 'done' | 'error'

interface UseVoiceInputReturn {
  state: VoiceInputState
  liveTranscript: string
  errorMessage: string | null
  hasSpeechSupport: boolean
  /** Milliseconds since `startRecording`, 0 when not recording. */
  elapsedMs: number
  /** Current microphone loudness, 0–1. Works on every browser, unlike `liveTranscript`. */
  level: number
  startRecording: () => Promise<void>
  stopRecording: () => Promise<Blob>
  resetTranscript: () => void
  reset: () => void
}

/** Level/timer sampling period. 10 Hz is smooth enough for a meter without re-rendering at 60 fps. */
const METER_INTERVAL_MS = 100

export function useVoiceInput(): UseVoiceInputReturn {
  const [state, setState]                   = useState<VoiceInputState>('idle')
  const [liveTranscript, setLiveTranscript] = useState('')
  const [errorMessage, setErrorMessage]     = useState<string | null>(null)
  const [elapsedMs, setElapsedMs]           = useState(0)
  const [level, setLevel]                   = useState(0)

  const recorderRef    = useRef<MediaRecorder | null>(null)
  const streamRef      = useRef<MediaStream | null>(null)
  const chunksRef      = useRef<Blob[]>([])
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const recognitionRef = useRef<any>(null)
  const isRecordingRef = useRef(false)
  const finalTextRef   = useRef('')
  const langIdxRef     = useRef(0)
  const audioCtxRef    = useRef<AudioContext | null>(null)
  const analyserRef    = useRef<AnalyserNode | null>(null)
  const meterTimerRef  = useRef<ReturnType<typeof setInterval> | null>(null)

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const SR = (window as any).SpeechRecognition ?? (window as any).webkitSpeechRecognition
  const hasSpeechSupport = Boolean(SR)

  // Fallback chain: Bosnian → Croatian → Serbian → English
  const LANG_CHAIN = ['bs-BA', 'hr-HR', 'sr-RS', 'en-US']

  // ── Speech recognition helpers ────────────────────────────────────────────

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function buildRecognition(): any | null {
    if (!SR) return null

    langIdxRef.current = 0

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const rec: any = new SR()
    rec.continuous     = true
    rec.interimResults = true
    rec.lang           = LANG_CHAIN[0]

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    rec.onresult = (event: any) => {
      // A result means recognition recovered from any earlier transient error (e.g. a network
      // blip) — clear the warning so it doesn't linger once live text is flowing again.
      setErrorMessage(null)
      let interim = ''
      for (let i = event.resultIndex; i < event.results.length; i++) {
        const t = event.results[i][0].transcript
        if (event.results[i].isFinal) {
          finalTextRef.current += t + ' '
        } else {
          interim += t
        }
      }
      setLiveTranscript(finalTextRef.current + interim)
    }

    // Browser stops recognition after silence — restart while still recording
    rec.onend = () => {
      if (isRecordingRef.current) {
        rec.lang = LANG_CHAIN[langIdxRef.current]
        try { rec.start() } catch { /* already starting */ }
      }
    }

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    rec.onerror = (e: any) => {
      if (e.error === 'language-not-supported') {
        // Try next language in chain; onend will restart with it
        if (langIdxRef.current < LANG_CHAIN.length - 1) {
          langIdxRef.current++
        }
      } else if (e.error !== 'no-speech' && e.error !== 'aborted') {
        // Non-fatal for the recording itself (MediaRecorder keeps running independently), but this
        // used to only console.warn — on a flaky mobile connection (very common in the field) the
        // live transcript would just silently stay blank with no indication why. 'network' is by
        // far the most common real-world case here, since Chrome's SpeechRecognition streams audio
        // to a remote server to transcribe it live.
        console.warn('SpeechRecognition error:', e.error)
        setErrorMessage(
          'Prikaz teksta uživo nije uspio (slaba mreža?) — snimak će ipak biti obrađen kad završite snimanje.',
        )
      }
    }

    return rec
  }

  // ── Meter (elapsed time + microphone level) ───────────────────────────────

  /**
   * Live text is unavailable on most phones (see `hasSpeechSupport` / the `network` error path), so
   * without this the recording screen gave a beekeeper no evidence the phone was hearing anything.
   * An elapsed counter plus a real input level is engine-independent feedback.
   */
  function startMeter(stream: MediaStream) {
    const startedAt = Date.now()
    // Explicit ArrayBuffer (not ArrayBufferLike) — getByteTimeDomainData rejects a possibly-shared buffer.
    let data: Uint8Array<ArrayBuffer> | null = null

    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const Ctx = window.AudioContext ?? (window as any).webkitAudioContext
      if (Ctx) {
        const ctx: AudioContext = new Ctx()
        const analyser = ctx.createAnalyser()
        analyser.fftSize = 512
        ctx.createMediaStreamSource(stream).connect(analyser)
        audioCtxRef.current = ctx
        analyserRef.current  = analyser
        data = new Uint8Array(new ArrayBuffer(analyser.fftSize))
      }
    } catch {
      /* Level meter is a nice-to-have — the timer alone still proves the recording is running. */
    }

    meterTimerRef.current = setInterval(() => {
      setElapsedMs(Date.now() - startedAt)
      const analyser = analyserRef.current
      if (!analyser || !data) return
      analyser.getByteTimeDomainData(data)
      let sum = 0
      for (let i = 0; i < data.length; i++) {
        const v = (data[i] - 128) / 128
        sum += v * v
      }
      // RMS of speech at a normal distance sits around 0.05–0.2, so scale up before clamping.
      setLevel(Math.min(1, Math.sqrt(sum / data.length) * 4))
    }, METER_INTERVAL_MS)
  }

  function stopMeter() {
    if (meterTimerRef.current) clearInterval(meterTimerRef.current)
    meterTimerRef.current = null
    analyserRef.current = null
    void audioCtxRef.current?.close().catch(() => {})
    audioCtxRef.current = null
    setLevel(0)
  }

  // ── Public API ────────────────────────────────────────────────────────────

  const startRecording = useCallback(async () => {
    setErrorMessage(null)
    finalTextRef.current = ''
    setLiveTranscript('')
    setElapsedMs(0)
    setLevel(0)

    let stream: MediaStream
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    } catch {
      setState('error')
      setErrorMessage('Pristup mikrofonu je odbijen. Dozvolite pristup u postavkama pretraživača.')
      return
    }

    streamRef.current = stream
    chunksRef.current = []

    const mimeType =
      MediaRecorder.isTypeSupported('audio/webm;codecs=opus') ? 'audio/webm;codecs=opus' :
      MediaRecorder.isTypeSupported('audio/webm')             ? 'audio/webm' :
      MediaRecorder.isTypeSupported('audio/mp4')              ? 'audio/mp4' :
      ''

    const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined)
    recorder.ondataavailable = (e) => { if (e.data.size > 0) chunksRef.current.push(e.data) }
    recorderRef.current = recorder
    recorder.start(250)

    isRecordingRef.current = true
    startMeter(stream)
    const rec = buildRecognition()
    if (rec) {
      recognitionRef.current = rec
      try { rec.start() } catch { /* ignore */ }
    }

    setState('recording')
  }, [])

  const stopRecording = useCallback((): Promise<Blob> => {
    isRecordingRef.current = false

    try { recognitionRef.current?.stop() } catch { /* ignore */ }
    recognitionRef.current = null
    stopMeter()

    return new Promise((resolve) => {
      const recorder = recorderRef.current
      if (!recorder) { resolve(new Blob()); return }

      recorder.onstop = () => {
        const blob = new Blob(chunksRef.current, { type: recorder.mimeType || 'audio/webm' })
        streamRef.current?.getTracks().forEach(t => t.stop())
        setState('done')
        resolve(blob)
      }

      recorder.stop()
    })
  }, [])

  const resetTranscript = useCallback(() => {
    finalTextRef.current = ''
    setLiveTranscript('')
    setErrorMessage(null)
    setElapsedMs(0)
    setState('idle')
  }, [])

  const reset = useCallback(() => {
    isRecordingRef.current = false
    try { recognitionRef.current?.stop() } catch { /* ignore */ }
    stopMeter()
    // stop() on an already-inactive recorder throws InvalidStateError, which would abort the rest
    // of the teardown and leak the microphone track (the red status bar stays on).
    try { if (recorderRef.current?.state !== 'inactive') recorderRef.current?.stop() } catch { /* ignore */ }
    streamRef.current?.getTracks().forEach(t => t.stop())
    recorderRef.current    = null
    streamRef.current      = null
    recognitionRef.current = null
    chunksRef.current      = []
    finalTextRef.current   = ''
    setLiveTranscript('')
    setErrorMessage(null)
    setElapsedMs(0)
    setState('idle')
  }, [])

  // Releasing the microphone must not depend on the component remembering to call reset().
  useEffect(() => () => {
    isRecordingRef.current = false
    if (meterTimerRef.current) clearInterval(meterTimerRef.current)
    void audioCtxRef.current?.close().catch(() => {})
    try { recognitionRef.current?.stop() } catch { /* ignore */ }
    try { if (recorderRef.current?.state !== 'inactive') recorderRef.current?.stop() } catch { /* ignore */ }
    streamRef.current?.getTracks().forEach(t => t.stop())
  }, [])

  return {
    state, liveTranscript, errorMessage, hasSpeechSupport, elapsedMs, level,
    startRecording, stopRecording, resetTranscript, reset,
  }
}
