import { useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { ImagePlus, Loader2, MessageSquarePlus, X } from 'lucide-react'
import clsx from 'clsx'
import { Modal } from './Modal'
import { ErrorMessage } from './index'
import { useSubmitFeedback } from '../../core/services/feedbackQueries'
import { useToast } from '../../core/context/ToastContext'
import {
  FeedbackSeverity,
  FeedbackSeverityLabels,
  FeedbackType,
  FeedbackTypeEmojis,
  FeedbackTypeHints,
  FeedbackTypeLabels,
} from '../../core/models'

const MAX_SCREENSHOT_BYTES = 5 * 1024 * 1024

/** Severity only means something for these two — a compliment has no severity. */
const SEVERITY_TYPES: FeedbackType[] = [FeedbackType.Bug, FeedbackType.Complaint]

const TYPE_ORDER: FeedbackType[] = [
  FeedbackType.Bug,
  FeedbackType.Complaint,
  FeedbackType.Compliment,
  FeedbackType.FeatureRequest,
  FeedbackType.Question,
  FeedbackType.Other,
]

interface FormValues {
  subject: string
  message: string
  severity: string
}

interface FeedbackFormModalProps {
  open: boolean
  onClose: () => void
}

export default function FeedbackFormModal({ open, onClose }: FeedbackFormModalProps) {
  const { pathname } = useLocation()
  const { toast } = useToast()
  const submit = useSubmitFeedback()
  const fileRef = useRef<HTMLInputElement>(null)

  const [type, setType] = useState<FeedbackType>(FeedbackType.Bug)
  const [screenshot, setScreenshot] = useState<File | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)

  const {
    register, handleSubmit, reset, setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ defaultValues: { subject: '', message: '', severity: '' } })

  const showSeverity = SEVERITY_TYPES.includes(type)

  function closeAndReset() {
    reset()
    setType(FeedbackType.Bug)
    setScreenshot(null)
    setFileError(null)
    onClose()
  }

  function handleFilePick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0] ?? null
    setFileError(null)

    if (file && file.size > MAX_SCREENSHOT_BYTES) {
      setFileError('Slika ne smije biti veća od 5 MB.')
      setScreenshot(null)
      if (fileRef.current) fileRef.current.value = ''
      return
    }
    setScreenshot(file)
  }

  async function onSubmit(values: FormValues) {
    try {
      await submit.mutateAsync({
        payload: {
          type,
          severity: showSeverity && values.severity ? (Number(values.severity) as FeedbackSeverity) : null,
          subject: values.subject.trim(),
          message: values.message.trim(),
          // Captured silently — the user should never have to explain which page they were on.
          pageContext: pathname,
          userAgent: navigator.userAgent.slice(0, 300),
        },
        screenshot,
      })
      toast.success('Hvala! Vaša povratna informacija je poslana.')
      closeAndReset()
    } catch (e) {
      // apiClient flattens every backend error to a single Error.message, so there is no
      // field-level dictionary to map here (see ProfilePage for the same reasoning).
      const message = e instanceof Error ? e.message : 'Greška pri slanju. Pokušajte ponovo.'
      setError('root', { message })
    }
  }

  return (
    <Modal
      open={open}
      onClose={closeAndReset}
      title="Pošaljite nam povratnu informaciju"
      description="Prijavite problem, pošaljite pohvalu ili predložite nešto novo. Čitamo sve."
      size="lg"
      closeOnBackdropClick={false}
      icon={
        <div className="w-10 h-10 bg-honey-100 dark:bg-honey-500/15 rounded-full flex items-center justify-center">
          <MessageSquarePlus className="w-5 h-5 text-honey-600 dark:text-honey-400" />
        </div>
      }
      footer={
        <div className="flex gap-3">
          <button type="button" onClick={closeAndReset} className="btn-secondary flex-1 justify-center text-sm">
            Otkaži
          </button>
          <button
            type="submit"
            form="feedback-form"
            disabled={isSubmitting}
            className="btn-primary flex-1 justify-center text-sm"
          >
            {isSubmitting ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Pošalji'}
          </button>
        </div>
      }
    >
      <form id="feedback-form" onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        {errors.root && <ErrorMessage message={errors.root.message ?? 'Greška pri slanju.'} />}

        {/* Type — buttons rather than a select, so the hint for each option is visible up front */}
        <div>
          <span className="form-label">O čemu se radi?</span>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 mt-1">
            {TYPE_ORDER.map(t => (
              <button
                key={t}
                type="button"
                onClick={() => setType(t)}
                aria-pressed={type === t}
                className={clsx(
                  'flex items-start gap-2.5 p-3 rounded-xl border text-left transition-colors',
                  type === t
                    ? 'border-honey-400 bg-honey-50 dark:border-honey-500/50 dark:bg-honey-500/10'
                    : 'border-gray-200 dark:border-slate-700 hover:bg-gray-50 dark:hover:bg-slate-800',
                )}
              >
                <span className="text-lg leading-none mt-0.5">{FeedbackTypeEmojis[t]}</span>
                <span className="min-w-0">
                  <span className="block text-sm font-semibold text-gray-800 dark:text-slate-100">
                    {FeedbackTypeLabels[t]}
                  </span>
                  <span className="block text-xs text-gray-500 dark:text-slate-400 mt-0.5">
                    {FeedbackTypeHints[t]}
                  </span>
                </span>
              </button>
            ))}
          </div>
        </div>

        {showSeverity && (
          <div>
            <label htmlFor="feedback-severity" className="form-label">
              Koliko vas ovo koči? <span className="font-normal text-gray-400">(nije obavezno)</span>
            </label>
            <select id="feedback-severity" {...register('severity')} className="form-input">
              <option value="">Ne znam / nije važno</option>
              {[FeedbackSeverity.Low, FeedbackSeverity.Medium, FeedbackSeverity.High, FeedbackSeverity.Critical].map(s => (
                <option key={s} value={s}>{FeedbackSeverityLabels[s]}</option>
              ))}
            </select>
          </div>
        )}

        <div>
          <label htmlFor="feedback-subject" className="form-label">Naslov</label>
          <input
            id="feedback-subject"
            {...register('subject', {
              required: 'Naslov je obavezan.',
              minLength: { value: 3, message: 'Naslov mora imati najmanje 3 znaka.' },
              maxLength: { value: 150, message: 'Naslov može imati najviše 150 znakova.' },
            })}
            className="form-input"
            placeholder="Ukratko — npr. „Datum se ne prikazuje ispravno“"
          />
          {errors.subject && <p className="form-error">{errors.subject.message}</p>}
        </div>

        <div>
          <label htmlFor="feedback-message" className="form-label">Opis</label>
          <textarea
            id="feedback-message"
            rows={5}
            {...register('message', {
              required: 'Opis je obavezan.',
              minLength: { value: 10, message: 'Opišite malo detaljnije — najmanje 10 znakova.' },
              maxLength: { value: 2000, message: 'Opis može imati najviše 2000 znakova.' },
            })}
            className="form-input"
            placeholder={
              type === FeedbackType.Bug
                ? 'Šta ste radili, šta se dogodilo i šta ste očekivali da se dogodi?'
                : 'Napišite nam detaljnije…'
            }
          />
          {errors.message && <p className="form-error">{errors.message.message}</p>}
        </div>

        {/* Screenshot */}
        <div>
          <span className="form-label">
            Slika ekrana <span className="font-normal text-gray-400">(nije obavezno)</span>
          </span>
          {screenshot ? (
            <div className="flex items-center gap-3 p-3 rounded-xl border border-gray-200 dark:border-slate-700">
              <ImagePlus className="w-4 h-4 text-honey-600 dark:text-honey-400 shrink-0" />
              <span className="text-sm text-gray-700 dark:text-slate-200 truncate flex-1">{screenshot.name}</span>
              <button
                type="button"
                onClick={() => { setScreenshot(null); if (fileRef.current) fileRef.current.value = '' }}
                className="p-1 rounded-lg text-gray-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
                aria-label="Ukloni sliku"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          ) : (
            <button
              type="button"
              onClick={() => fileRef.current?.click()}
              className="w-full flex items-center justify-center gap-2 p-3 rounded-xl border border-dashed border-gray-300 dark:border-slate-700 text-sm text-gray-500 dark:text-slate-400 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
            >
              <ImagePlus className="w-4 h-4" />
              Dodaj sliku ekrana
            </button>
          )}
          <input
            ref={fileRef}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            onChange={handleFilePick}
            className="hidden"
          />
          {fileError && <p className="form-error">{fileError}</p>}
          <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">
            JPEG, PNG ili WebP, do 5 MB. Slika najbrže objasni problem.
          </p>
        </div>

        <p className="text-xs text-gray-400 dark:text-slate-500">
          Uz poruku šaljemo i stranicu na kojoj ste sada ({pathname}) te podatke o pregledniku, da
          lakše pronađemo problem.
        </p>
      </form>
    </Modal>
  )
}
