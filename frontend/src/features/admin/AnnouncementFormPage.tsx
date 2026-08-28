import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { AlertCircle, Eye, Loader2, Pencil } from 'lucide-react'
import {
  useAdminAnnouncement,
  useCreateAnnouncement,
  useUpdateAnnouncement,
} from '../../core/services/announcementQueries'
import { AnnouncementType, AnnouncementTypeLabels } from '../../core/models'
import { FormHeader, MarkdownMessage } from '../../shared/components'
import { useFormNavigation } from '../../shared/hooks/useFormNavigation'
import { useToast } from '../../core/context/ToastContext'

const TYPES = Object.values(AnnouncementType).filter(v => typeof v === 'number') as AnnouncementType[]

export default function AnnouncementFormPage() {
  const { id } = useParams<{ id: string }>()
  const announcementId = id ? parseInt(id) : undefined
  const isEdit = announcementId !== undefined

  const { goBack, goAfterSave } = useFormNavigation('/admin/announcements')
  const { toast } = useToast()

  const { data: existing, isLoading: loadingExisting } = useAdminAnnouncement(announcementId ?? 0)
  const createAnnouncement = useCreateAnnouncement()
  const updateAnnouncement = useUpdateAnnouncement(announcementId ?? 0)

  const [title, setTitle] = useState('')
  const [type, setType] = useState<AnnouncementType>(AnnouncementType.New)
  const [body, setBody] = useState('')
  const [showPreview, setShowPreview] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  useEffect(() => {
    if (existing && isEdit) {
      setTitle(existing.title)
      setType(existing.type)
      setBody(existing.bodyMarkdown)
    }
  }, [existing, isEdit])

  const isSaving = createAnnouncement.isPending || updateAnnouncement.isPending

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    if (!title.trim()) { setFormError('Naslov je obavezan.'); return }

    const payload = { title: title.trim(), type, bodyMarkdown: body }

    try {
      if (isEdit && announcementId) {
        await updateAnnouncement.mutateAsync(payload)
        toast.success('Objava ažurirana — banner se ne vraća onima koji su ga već sklonili.')
      } else {
        await createAnnouncement.mutateAsync(payload)
        toast.success('Objava kreirana kao skica — objavite je s liste objava.')
      }
      goAfterSave('/admin/announcements')
    } catch (err: any) {
      const errors = err?.response?.data?.errors
      const first = errors ? (Object.values(errors)[0] as string[])?.[0] : undefined
      setFormError(first ?? err?.response?.data?.detail ?? 'Greška pri čuvanju objave.')
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
        icon="✨"
        title={isEdit ? 'Uredi objavu' : 'Nova objava'}
      />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-8 py-8">
        {formError && (
          <div className="flex items-start gap-2 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm mb-5">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            {formError}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-6">
          {/* Title + type */}
          <div className="grid grid-cols-1 sm:grid-cols-[2fr_1fr] gap-4">
            <div>
              <label className={labelClass}>
                Naslov <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                maxLength={150}
                placeholder="npr. Sastavljanje društava"
                value={title}
                onChange={e => setTitle(e.target.value)}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>Tip</label>
              <select value={type} onChange={e => setType(Number(e.target.value))} className={inputClass}>
                {TYPES.map(t => <option key={t} value={t}>{AnnouncementTypeLabels[t]}</option>)}
              </select>
            </div>
          </div>

          {/* Body markdown + preview toggle */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="text-sm font-medium text-gray-700 dark:text-slate-300">Opis (markdown)</label>
              <button
                type="button"
                onClick={() => setShowPreview(v => !v)}
                className="flex items-center gap-1 text-xs font-medium text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300 transition-colors"
              >
                {showPreview ? <><Pencil className="w-3.5 h-3.5" /> Uređivanje</> : <><Eye className="w-3.5 h-3.5" /> Pregled</>}
              </button>
            </div>
            {showPreview ? (
              <div className="rounded-xl border border-gray-200 dark:border-slate-700 px-4 py-4 min-h-[12rem] bg-gray-50/50 dark:bg-slate-800/40
                              text-[15px] leading-relaxed text-gray-700 dark:text-slate-300">
                {body.trim()
                  ? <MarkdownMessage content={body} />
                  : <p className="text-sm text-gray-400 dark:text-slate-500">Nema sadržaja za pregled.</p>}
              </div>
            ) : (
              <textarea
                rows={12}
                placeholder={'Šta je novo i zašto je korisno…\n\n- prva stavka\n- druga stavka'}
                value={body}
                onChange={e => setBody(e.target.value)}
                className={`${inputClass} font-mono text-[13px] leading-relaxed`}
              />
            )}
            <p className="text-xs text-gray-400 dark:text-slate-500 mt-1.5">
              Opis se vidi tek kad korisnik otvori objavu — u banneru stoje samo tip i naslov.
              Skica se može sačuvati bez opisa; za objavu je opis obavezan.
            </p>
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={goBack} className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors">
              Otkaži
            </button>
            <button type="submit" disabled={isSaving} className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors">
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isEdit ? 'Spremi' : 'Sačuvaj objavu'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
