import { useCallback, useEffect, useRef, useState } from 'react'

export type SpeechStatus = 'idle' | 'speaking' | 'paused'

/**
 * Browser text-to-speech via `speechSynthesis` (SPEC-06 "Poslušaj") — no backend, no cost.
 * Voice pick order: bs-* → hr-* → sr-* → browser default (quality then depends on the device).
 * Long texts are queued as paragraph-sized utterances (single long utterances get cut off in
 * some Chrome versions). Speech is cancelled on unmount so navigation always stops playback.
 */
export function useSpeech() {
  const isSupported = typeof window !== 'undefined' && 'speechSynthesis' in window
  const [status, setStatus] = useState<SpeechStatus>('idle')
  const queueRef = useRef<SpeechSynthesisUtterance[]>([])
  const voicesRef = useRef<SpeechSynthesisVoice[]>([])
  const keepAliveRef = useRef<number | null>(null)

  // Voices load asynchronously; cache them and refresh on `voiceschanged` so the very first
  // "Poslušaj" tap already has a bs/hr/sr voice available. Without this, the first play often
  // falls back to the browser default reading Bosnian text with an English accent.
  useEffect(() => {
    if (!isSupported) return
    const load = () => { voicesRef.current = window.speechSynthesis.getVoices() }
    load()
    window.speechSynthesis.addEventListener('voiceschanged', load)
    return () => window.speechSynthesis.removeEventListener('voiceschanged', load)
  }, [isSupported])

  const clearKeepAlive = () => {
    if (keepAliveRef.current !== null) {
      clearInterval(keepAliveRef.current)
      keepAliveRef.current = null
    }
  }

  const stop = useCallback(() => {
    if (!isSupported) return
    clearKeepAlive()
    queueRef.current = []
    window.speechSynthesis.cancel()
    setStatus('idle')
  }, [isSupported])

  // Navigation away must stop the audio.
  useEffect(() => stop, [stop])

  const speak = useCallback((text: string) => {
    if (!isSupported) return
    window.speechSynthesis.cancel()
    clearKeepAlive()

    const available = voicesRef.current.length ? voicesRef.current : window.speechSynthesis.getVoices()
    const voice = pickVoice(available)
    const chunks = toChunks(text)
    if (chunks.length === 0) return

    const utterances = chunks.map(chunk => {
      const u = new SpeechSynthesisUtterance(chunk)
      if (voice) {
        u.voice = voice
        u.lang = voice.lang
      } else {
        // No regional voice installed — tag as Bosnian so the engine makes its best guess.
        u.lang = 'bs-BA'
      }
      u.rate = 0.95
      u.pitch = 1
      return u
    })

    const last = utterances[utterances.length - 1]
    last.onend = () => { clearKeepAlive(); setStatus('idle') }
    last.onerror = () => { clearKeepAlive(); setStatus('idle') }

    queueRef.current = utterances
    for (const u of utterances) window.speechSynthesis.speak(u)

    // Chrome halts long speech after ~15 s of a synthesis session; a periodic pause/resume nudge
    // keeps multi-paragraph articles playing to the end. Guarded so a user pause is respected.
    keepAliveRef.current = window.setInterval(() => {
      const s = window.speechSynthesis
      if (s.speaking && !s.paused) { s.pause(); s.resume() }
    }, 10000)

    setStatus('speaking')
  }, [isSupported])

  const pause = useCallback(() => {
    if (!isSupported || status !== 'speaking') return
    window.speechSynthesis.pause()
    setStatus('paused')
  }, [isSupported, status])

  const resume = useCallback(() => {
    if (!isSupported || status !== 'paused') return
    window.speechSynthesis.resume()
    setStatus('speaking')
  }, [isSupported, status])

  return { isSupported, status, speak, pause, resume, stop }
}

/** bs → hr → sr → null (browser default); prefers a local (offline) voice, usually higher quality. */
function pickVoice(voices: SpeechSynthesisVoice[]): SpeechSynthesisVoice | null {
  for (const prefix of ['bs', 'hr', 'sr']) {
    const matches = voices.filter(v => v.lang.toLowerCase().startsWith(prefix))
    if (matches.length) return matches.find(v => v.localService) ?? matches[0]
  }
  return null
}

/** Paragraph-sized utterance chunks; very long paragraphs are split on sentence boundaries. */
function toChunks(text: string): string[] {
  const chunks: string[] = []
  for (const paragraph of text.split(/\n{2,}/)) {
    const p = paragraph.trim()
    if (!p) continue
    if (p.length <= 400) {
      chunks.push(p)
      continue
    }
    let current = ''
    for (const sentence of p.split(/(?<=[.!?])\s+/)) {
      if (current && current.length + sentence.length > 400) {
        chunks.push(current)
        current = sentence
      } else {
        current = current ? `${current} ${sentence}` : sentence
      }
    }
    if (current) chunks.push(current)
  }
  return chunks
}
