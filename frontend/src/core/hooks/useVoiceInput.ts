import { useState, useRef, useCallback } from 'react'

export type VoiceInputState = 'idle' | 'recording' | 'done' | 'error'

interface UseVoiceInputReturn {
  state: VoiceInputState
  liveTranscript: string
  errorMessage: string | null
  hasSpeechSupport: boolean
  startRecording: () => Promise<void>
  stopRecording: () => Promise<Blob>
  resetTranscript: () => void
  reset: () => void
}

export function useVoiceInput(): UseVoiceInputReturn {
  const [state, setState]                   = useState<VoiceInputState>('idle')
  const [liveTranscript, setLiveTranscript] = useState('')
  const [errorMessage, setErrorMessage]     = useState<string | null>(null)

  const recorderRef    = useRef<MediaRecorder | null>(null)
  const streamRef      = useRef<MediaStream | null>(null)
  const chunksRef      = useRef<Blob[]>([])
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const recognitionRef = useRef<any>(null)
  const isRecordingRef = useRef(false)
  const finalTextRef   = useRef('')
  const langIdxRef     = useRef(0)

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
        // non-fatal — audio recording continues unaffected
        console.warn('SpeechRecognition error:', e.error)
      }
    }

    return rec
  }

  // ── Public API ────────────────────────────────────────────────────────────

  const startRecording = useCallback(async () => {
    setErrorMessage(null)
    finalTextRef.current = ''
    setLiveTranscript('')

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
    setState('idle')
  }, [])

  const reset = useCallback(() => {
    isRecordingRef.current = false
    try { recognitionRef.current?.stop() } catch { /* ignore */ }
    recorderRef.current?.stop()
    streamRef.current?.getTracks().forEach(t => t.stop())
    recorderRef.current    = null
    streamRef.current      = null
    recognitionRef.current = null
    chunksRef.current      = []
    finalTextRef.current   = ''
    setLiveTranscript('')
    setErrorMessage(null)
    setState('idle')
  }, [])

  return { state, liveTranscript, errorMessage, hasSpeechSupport, startRecording, stopRecording, resetTranscript, reset }
}
