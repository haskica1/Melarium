import { useRef, useState } from 'react'
import { Loader2, Mic, Send, Square } from 'lucide-react'
import { useVoiceInput } from '../../core/hooks/useVoiceInput'
import { advisorService } from '../../core/services/advisorService'
import { useToast } from '../../core/context/ToastContext'

interface ChatInputProps {
  onSend: (message: string) => Promise<void>
  disabled?: boolean
}

/** Textarea + mic + send. Mic records → transcribes → transcript lands in the textarea for review. */
export function ChatInput({ onSend, disabled }: ChatInputProps) {
  const [text, setText] = useState('')
  const [transcribing, setTranscribing] = useState(false)
  const voice = useVoiceInput()
  const { toast } = useToast()
  const taRef = useRef<HTMLTextAreaElement>(null)

  const isRecording = voice.state === 'recording'
  const busy = disabled || transcribing

  async function submit(messageOverride?: string) {
    const trimmed = (messageOverride ?? text).trim()
    if (!trimmed) return
    // Voice auto-submit passes an explicit message and must not be blocked by the transient
    // `transcribing` flag; a manual send still waits until nothing is busy.
    if (messageOverride === undefined && busy) return
    if (disabled) return
    try {
      await onSend(trimmed)
      setText('') // only clear on success — a failed send keeps the text for retry
    } catch {
      /* keep the text in the input */
    }
  }

  async function toggleMic() {
    if (!isRecording) {
      await voice.startRecording()
      return
    }

    const blob = await voice.stopRecording()
    setTranscribing(true)
    let transcript = ''
    try {
      transcript = await advisorService.transcribe(blob)
    } catch {
      toast.error('Transkripcija nije uspjela. Pokušajte ponovo ili ukucajte poruku.')
    } finally {
      setTranscribing(false)
      voice.reset()
    }

    const spoken = transcript.trim()
    if (!spoken) return
    // A voice question sends itself — no extra tap on the send button needed.
    const message = text.trim() ? `${text.trim()} ${spoken}` : spoken
    setText(message)
    await submit(message)
  }

  return (
    <div className="border-t border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 p-3">
      {isRecording && (
        <div className="flex items-center gap-2 px-2 pb-2 text-sm text-red-500">
          <span className="w-2 h-2 rounded-full bg-red-500 animate-pulse" />
          Snimanje… {voice.liveTranscript && <span className="text-gray-400 dark:text-slate-500 truncate">{voice.liveTranscript}</span>}
        </div>
      )}
      <div className="flex items-end gap-2">
        <textarea
          ref={taRef}
          value={text}
          onChange={e => setText(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); submit() }
          }}
          rows={1}
          placeholder="Napišite pitanje ili snimite glasom…"
          disabled={busy && !transcribing}
          className="flex-1 resize-none max-h-40 px-4 py-2.5 rounded-2xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
        />
        <button
          type="button"
          onClick={toggleMic}
          disabled={transcribing || disabled}
          className={`shrink-0 w-11 h-11 rounded-2xl flex items-center justify-center transition-colors disabled:opacity-50 ${
            isRecording
              ? 'bg-red-500 hover:bg-red-600 text-white'
              : 'bg-gray-100 dark:bg-slate-800 text-gray-600 dark:text-slate-300 hover:bg-honey-100 dark:hover:bg-slate-700 hover:text-honey-700'
          }`}
          aria-label={isRecording ? 'Zaustavi snimanje' : 'Snimi glasom'}
        >
          {transcribing ? <Loader2 className="w-5 h-5 animate-spin" /> : isRecording ? <Square className="w-5 h-5" /> : <Mic className="w-5 h-5" />}
        </button>
        <button
          type="button"
          onClick={() => submit()}
          disabled={busy || !text.trim()}
          className="shrink-0 w-11 h-11 rounded-2xl flex items-center justify-center bg-honey-500 hover:bg-honey-600 text-white transition-colors disabled:opacity-50"
          aria-label="Pošalji"
        >
          <Send className="w-5 h-5" />
        </button>
      </div>
    </div>
  )
}
