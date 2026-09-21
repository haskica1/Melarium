import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { AlertCircle, Eye, Info, Loader2, Paperclip, Pencil, Send, Video } from 'lucide-react'
import {
  useMySubmission,
  useSubmitTopic,
  useUpdateMySubmission,
} from '../../core/services/learningQueries'
import { LearningCategory, LearningCategoryLabels, MonthLabels, TopicReviewStatus } from '../../core/models'
import { FormHeader } from '../../shared/components'
import { useFormNavigation } from '../../shared/hooks/useFormNavigation'
import { useToast } from '../../core/context/ToastContext'
import { MarkdownArticle } from './MarkdownArticle'

const CATEGORIES = Object.values(LearningCategory).filter(v => typeof v === 'number') as LearningCategory[]

/** Mirrors the backend floor — an article, not a sentence. */
const MIN_BODY_LENGTH = 200

export default function TopicSubmissionFormPage() {
  const { id } = useParams<{ id: string }>()
  const submissionId = id ? parseInt(id) : undefined
  const isEdit = submissionId !== undefined

  const { goBack, goAfterSave } = useFormNavigation('/learning/moje-teme')
  const { toast } = useToast()

  const { data: existing, isLoading: loadingExisting } = useMySubmission(submissionId ?? 0)
  const submitTopic = useSubmitTopic()
  const updateSubmission = useUpdateMySubmission(submissionId ?? 0)

  const [title, setTitle] = useState('')
  const [category, setCategory] = useState<LearningCategory>(LearningCategory.Osnove)
  const [months, setMonths] = useState<number[]>([])
  const [summary, setSummary] = useState('')
  const [body, setBody] = useState('')
  const [videoUrl, setVideoUrl] = useState('')
  const [fileUrl, setFileUrl] = useState('')
  const [fileName, setFileName] = useState('')
  const [showPreview, setShowPreview] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  useEffect(() => {
    if (existing && isEdit) {
      setTitle(existing.title)
      setCategory(existing.category)
      setMonths(existing.months ?? [])
      setSummary(existing.summary)
      setBody(existing.bodyMarkdown)
      setVideoUrl(existing.videoUrl ?? '')
      setFileUrl(existing.fileUrl ?? '')
      setFileName(existing.fileName ?? '')
    }
  }, [existing, isEdit])

  const isSaving = submitTopic.isPending || updateSubmission.isPending
  const wasRejected = existing?.reviewStatus === TopicReviewStatus.Rejected

  function toggleMonth(m: number) {
    setMonths(prev => prev.includes(m) ? prev.filter(x => x !== m) : [...prev, m].sort((a, b) => a - b))
  }

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    if (!title.trim()) { setFormError('Naslov je obavezan.'); return }
    if (!summary.trim()) { setFormError('Sažetak je obavezan.'); return }
    if (body.trim().length < MIN_BODY_LENGTH) {
      setFormError(`Tekst teme mora imati najmanje ${MIN_BODY_LENGTH} znakova.`)
      return
    }

    const payload = {
      title: title.trim(),
      category,
      months: months.length > 0 ? months : null,
      summary: summary.trim(),
      bodyMarkdown: body,
      videoUrl: videoUrl.trim() || null,
      fileUrl: fileUrl.trim() || null,
      fileName: fileName.trim() || null,
    }

    try {
      if (isEdit && submissionId) {
        await updateSubmission.mutateAsync(payload)
        toast.success(wasRejected
          ? 'Tema je ponovo poslana na odobrenje.'
          : 'Izmjene su sačuvane — tema i dalje čeka odobrenje.')
      } else {
        await submitTopic.mutateAsync(payload)
        toast.success('Tema je poslana administratoru na odobrenje.')
      }
      goAfterSave('/learning/moje-teme')
    } catch (err: any) {
      const errors = err?.response?.data?.errors
      const first = errors ? (Object.values(errors)[0] as string[])?.[0] : undefined
      setFormError(first ?? err?.response?.data?.detail ?? 'Greška pri slanju teme.')
    }
  }

  if (isEdit && loadingExisting) {
    return (
      <div className="flex justify-center py-20">
        <Loader2 className="w-6 h-6 animate-spin text-honey-500" />
      </div>
    )
  }

  const inputClass =
    'w-full px-4 py-2.5 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all'
  const labelClass = 'block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5'

  return (
    <div className="max-w-3xl mx-auto">
      <FormHeader
        icon="🎓"
        title={isEdit ? 'Uredi temu' : 'Predloži temu'}
      />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-6 sm:px-8 py-8">
        {/* What happens after sending — said before the form, not after it. */}
        <div className="flex items-start gap-2 bg-honey-50 dark:bg-slate-800/60 border border-honey-100 dark:border-slate-700 text-gray-600 dark:text-slate-300 rounded-xl px-4 py-3 text-sm mb-5">
          <Info className="w-4 h-4 mt-0.5 shrink-0 text-honey-600 dark:text-honey-400" />
          <span>
            Tema ide administratoru na pregled i pojavljuje se u Edukaciji tek kad je odobri.
            Do tada je vide samo administrator i vi. Ako ne bude odobrena, dobit ćete razlog i
            možete je doraditi i poslati ponovo.
          </span>
        </div>

        {wasRejected && existing?.rejectionReason && (
          <div className="flex items-start gap-2 bg-amber-50 dark:bg-amber-500/10 border border-amber-200 dark:border-amber-500/30 text-amber-800 dark:text-amber-300 rounded-xl px-4 py-3 text-sm mb-5">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            <span><strong>Razlog odbijanja:</strong> {existing.rejectionReason}</span>
          </div>
        )}

        {formError && (
          <div className="flex items-start gap-2 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm mb-5">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            {formError}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-6">
          {/* Title + category */}
          <div className="grid grid-cols-1 sm:grid-cols-[2fr_1fr] gap-4">
            <div>
              <label className={labelClass}>
                Naslov <span className="text-red-500">*</span>
              </label>
              <input type="text" maxLength={150} placeholder="npr. Priprema zajednica za zimu" value={title} onChange={e => setTitle(e.target.value)} className={inputClass} />
            </div>
            <div>
              <label className={labelClass}>Kategorija</label>
              <select value={category} onChange={e => setCategory(Number(e.target.value))} className={inputClass}>
                {CATEGORIES.map(c => <option key={c} value={c}>{LearningCategoryLabels[c]}</option>)}
              </select>
            </div>
          </div>

          {/* Months */}
          <div>
            <label className={labelClass}>Aktuelno u mjesecima</label>
            <div className="flex items-center gap-1.5 flex-wrap">
              {MonthLabels.map((label, i) => {
                const m = i + 1
                const active = months.includes(m)
                return (
                  <button
                    key={m}
                    type="button"
                    onClick={() => toggleMonth(m)}
                    className={`px-2.5 py-1.5 rounded-lg text-xs font-medium border transition-colors ${
                      active
                        ? 'bg-honey-500 border-honey-500 text-white'
                        : 'bg-white dark:bg-slate-800 border-gray-200 dark:border-slate-700 text-gray-600 dark:text-slate-300 hover:bg-honey-50 dark:hover:bg-slate-700'
                    }`}
                  >
                    {label.slice(0, 3)}
                  </button>
                )
              })}
            </div>
            <p className="text-xs text-gray-400 dark:text-slate-500 mt-1.5">
              Ništa označeno = tema je uvijek aktuelna (ne veže se za sezonu).
            </p>
          </div>

          {/* Summary */}
          <div>
            <label className={labelClass}>
              Sažetak (za karticu) <span className="text-red-500">*</span>
            </label>
            <textarea rows={2} maxLength={300} placeholder="Jedna do dvije rečenice o čemu je tema…" value={summary} onChange={e => setSummary(e.target.value)} className={inputClass} />
            <p className="text-xs text-gray-400 dark:text-slate-500 mt-1 text-right">{summary.length}/300</p>
          </div>

          {/* Body markdown + preview toggle */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="text-sm font-medium text-gray-700 dark:text-slate-300">
                Tekst teme <span className="text-red-500">*</span>
              </label>
              <button
                type="button"
                onClick={() => setShowPreview(v => !v)}
                className="flex items-center gap-1 text-xs font-medium text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300 transition-colors"
              >
                {showPreview ? <><Pencil className="w-3.5 h-3.5" /> Uređivanje</> : <><Eye className="w-3.5 h-3.5" /> Pregled</>}
              </button>
            </div>
            {showPreview ? (
              <div className="rounded-xl border border-gray-200 dark:border-slate-700 px-4 py-4 min-h-[16rem] bg-gray-50/50 dark:bg-slate-800/40">
                {body.trim()
                  ? <MarkdownArticle markdown={body} />
                  : <p className="text-sm text-gray-400 dark:text-slate-500">Nema sadržaja za pregled.</p>}
              </div>
            ) : (
              <textarea
                rows={16}
                placeholder={'## Podnaslov\n\nTekst pasusa…\n\n- stavka liste'}
                value={body}
                onChange={e => setBody(e.target.value)}
                className={`${inputClass} font-mono text-[13px] leading-relaxed`}
              />
            )}
            <div className="flex items-center justify-between mt-1.5">
              <p className="text-xs text-gray-400 dark:text-slate-500">
                Podnaslov se piše kao <code>## Naslov</code>, stavka liste kao <code>- tekst</code>.
              </p>
              <p className={`text-xs ${body.trim().length < MIN_BODY_LENGTH ? 'text-gray-400 dark:text-slate-500' : 'text-emerald-600 dark:text-emerald-400'}`}>
                {body.trim().length}/{MIN_BODY_LENGTH} znakova
              </p>
            </div>
          </div>

          {/* Video / file attachment (optional) */}
          <div className="space-y-4">
            <div>
              <label className={labelClass}>
                <Video className="w-3.5 h-3.5 inline -mt-0.5 mr-1 text-honey-500" />
                Video (opcionalno)
              </label>
              <input
                type="url"
                maxLength={500}
                placeholder="npr. https://youtu.be/XXXXXXXXXXX"
                value={videoUrl}
                onChange={e => setVideoUrl(e.target.value)}
                className={inputClass}
              />
            </div>

            <div>
              <label className={labelClass}>
                <Paperclip className="w-3.5 h-3.5 inline -mt-0.5 mr-1 text-honey-500" />
                Fajl (opcionalno)
              </label>
              <div className="grid grid-cols-1 sm:grid-cols-[2fr_1fr] gap-3">
                <input
                  type="url"
                  maxLength={500}
                  placeholder="link ka fajlu (npr. PDF)"
                  value={fileUrl}
                  onChange={e => setFileUrl(e.target.value)}
                  className={inputClass}
                />
                <input
                  type="text"
                  maxLength={150}
                  placeholder="naziv za prikaz (opcionalno)"
                  value={fileName}
                  onChange={e => setFileName(e.target.value)}
                  className={inputClass}
                />
              </div>
            </div>
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={goBack} className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors">
              Otkaži
            </button>
            <button type="submit" disabled={isSaving} className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors">
              {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
              {isEdit
                ? (wasRejected ? 'Pošalji ponovo' : 'Spremi izmjene')
                : 'Pošalji na odobrenje'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
