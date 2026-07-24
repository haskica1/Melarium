import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { AlertCircle, Eye, Loader2, Paperclip, Pencil, Sparkles, Video } from 'lucide-react'
import {
  useAdminLearningTopic,
  useCreateLearningTopic,
  useUpdateLearningTopic,
  useGenerateDraft,
} from '../../core/services/learningQueries'
import { LearningCategory, LearningCategoryLabels, MonthLabels } from '../../core/models'
import { FormHeader } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'
import { MarkdownArticle } from '../learning/MarkdownArticle'

const CATEGORIES = Object.values(LearningCategory).filter(v => typeof v === 'number') as LearningCategory[]

export default function LearningTopicFormPage() {
  const { id } = useParams<{ id: string }>()
  const topicId = id ? parseInt(id) : undefined
  const isEdit = topicId !== undefined

  const navigate = useNavigate()
  const { toast } = useToast()

  const { data: existing, isLoading: loadingExisting } = useAdminLearningTopic(topicId ?? 0)
  const createTopic = useCreateLearningTopic()
  const updateTopic = useUpdateLearningTopic(topicId ?? 0)
  const generateDraft = useGenerateDraft()

  const [title, setTitle] = useState('')
  const [category, setCategory] = useState<LearningCategory>(LearningCategory.Osnove)
  const [months, setMonths] = useState<number[]>([])
  const [summary, setSummary] = useState('')
  const [body, setBody] = useState('')
  const [videoUrl, setVideoUrl] = useState('')
  const [fileUrl, setFileUrl] = useState('')
  const [fileName, setFileName] = useState('')
  const [outline, setOutline] = useState('')
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

  const isSaving = createTopic.isPending || updateTopic.isPending

  function toggleMonth(m: number) {
    setMonths(prev => prev.includes(m) ? prev.filter(x => x !== m) : [...prev, m].sort((a, b) => a - b))
  }

  async function handleGenerateDraft() {
    setFormError(null)
    if (!title.trim()) { setFormError('Unesite naslov prije AI nacrta.'); return }
    try {
      const draft = await generateDraft.mutateAsync({ title: title.trim(), outline: outline.trim() || null })
      setBody(draft.bodyMarkdown)
      setSummary(draft.summary)
      setShowPreview(false)
      toast.success('Nacrt generisan — pregledajte i doradite prije objave.')
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? 'AI servis trenutno nije dostupan.')
    }
  }

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    if (!title.trim()) { setFormError('Naslov je obavezan.'); return }
    if (!summary.trim()) { setFormError('Sažetak je obavezan.'); return }
    if (summary.trim().length > 300) { setFormError('Sažetak može imati najviše 300 znakova.'); return }

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
      if (isEdit && topicId) {
        await updateTopic.mutateAsync(payload)
        toast.success('Tema ažurirana.')
      } else {
        await createTopic.mutateAsync(payload)
        toast.success('Tema kreirana kao skica — objavite je s liste tema.')
      }
      navigate('/admin/learning-topics')
    } catch (err: any) {
      const errors = err?.response?.data?.errors
      const first = errors ? (Object.values(errors)[0] as string[])?.[0] : undefined
      setFormError(first ?? err?.response?.data?.detail ?? 'Greška pri čuvanju teme.')
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
        title={isEdit ? 'Uredi temu' : 'Nova tema'}
        onBack={() => navigate('/admin/learning-topics')}
        backLabel="Nazad na teme"
      />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-8 py-8">
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

          {/* AI draft assist */}
          <div className="bg-honey-50 dark:bg-slate-800/60 border border-honey-100 dark:border-slate-700 rounded-xl px-4 py-4 space-y-3">
            <div className="flex items-center gap-2">
              <Sparkles className="w-4 h-4 text-honey-600 dark:text-honey-400" />
              <span className="text-sm font-medium text-gray-700 dark:text-slate-200">AI nacrt (opcionalno)</span>
            </div>
            <textarea
              rows={2}
              placeholder="Smjernice za AI (opcionalno) — npr. naglasi rezerve hrane i utopljavanje…"
              value={outline}
              onChange={e => setOutline(e.target.value)}
              className={inputClass}
            />
            <button
              type="button"
              onClick={handleGenerateDraft}
              disabled={generateDraft.isPending}
              className="flex items-center gap-1.5 px-3 py-2 rounded-xl border border-honey-300 dark:border-honey-500/40 bg-white dark:bg-slate-800 text-sm font-medium text-honey-700 dark:text-honey-300 hover:bg-honey-100 dark:hover:bg-slate-700 transition-colors disabled:opacity-60"
            >
              {generateDraft.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Sparkles className="w-4 h-4" />}
              Generiši nacrt iz naslova
            </button>
            <p className="text-xs text-gray-500 dark:text-slate-400">
              Nacrt popunjava tekst i sažetak — AI nikad ne objavljuje. Postojeći tekst se zamjenjuje.
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
              <label className="text-sm font-medium text-gray-700 dark:text-slate-300">Tekst teme (markdown)</label>
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
            <p className="text-xs text-gray-400 dark:text-slate-500 mt-1.5">
              Skica se može sačuvati bez teksta; za objavu je tekst obavezan.
            </p>
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
              <p className="text-xs text-gray-400 dark:text-slate-500 mt-1">
                YouTube, Vimeo ili direktan link ka video fajlu — prikazuje se na stranici teme.
              </p>
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
              <p className="text-xs text-gray-400 dark:text-slate-500 mt-1">
                Prikazuje se kao dugme za preuzimanje/otvaranje na stranici teme.
              </p>
            </div>
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={() => navigate('/admin/learning-topics')} className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors">
              Otkaži
            </button>
            <button type="submit" disabled={isSaving} className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors">
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isEdit ? 'Spremi promjene' : 'Sačuvaj temu'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
