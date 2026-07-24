import { useEffect, useState } from 'react'
import { ChevronLeft, ChevronRight, Loader2, Sparkles, Trash2, X } from 'lucide-react'
import { inspectionPhotoService, parsePhotoAnalysis } from '../../core/services/inspectionPhotoService'
import {
  useAnalyzeInspectionPhoto,
  useDeleteInspectionPhoto,
  useInspectionPhotos,
} from '../../core/services/queries'
import { useToast } from '../../core/context/ToastContext'
import type { InspectionPhoto, PhotoAnalysis } from '../../core/models'

/**
 * Fetches a photo through apiClient (Bearer header) and renders it via an object URL —
 * a plain <img src> can't authenticate. The URL is revoked on unmount (SPEC-05).
 */
function AuthImage({ photoId, alt, className }: { photoId: number; alt: string; className?: string }) {
  const [url, setUrl] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let objectUrl: string | null = null
    let cancelled = false
    inspectionPhotoService
      .fetchImageBlob(photoId)
      .then(blob => {
        objectUrl = URL.createObjectURL(blob)
        if (!cancelled) setUrl(objectUrl)
      })
      .catch(() => { if (!cancelled) setFailed(true) })
    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [photoId])

  if (failed) {
    return (
      <div className={`flex items-center justify-center bg-gray-100 dark:bg-slate-800 text-gray-400 dark:text-slate-500 text-xs ${className ?? ''}`}>
        ⚠️
      </div>
    )
  }
  if (!url) {
    return (
      <div className={`flex items-center justify-center bg-gray-100 dark:bg-slate-800 ${className ?? ''}`}>
        <Loader2 className="w-4 h-4 animate-spin text-gray-400 dark:text-slate-500" />
      </div>
    )
  }
  return <img src={url} alt={alt} className={className} />
}

/**
 * Thumbnail strip for an inspection's photos + lightbox. Renders nothing while the
 * inspection has no photos, so it is safe to mount on every timeline item.
 */
export function InspectionPhotoStrip({ inspectionId, canManage }: {
  inspectionId: number
  canManage: boolean
}) {
  const { data: photos } = useInspectionPhotos(inspectionId)
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null)

  if (!photos?.length) return null

  return (
    <>
      <div className="flex flex-wrap gap-2 mt-2 pt-2 border-t border-gray-100 dark:border-slate-700">
        {photos.map((photo, i) => (
          <button
            key={photo.id}
            type="button"
            onClick={() => setLightboxIndex(i)}
            className="w-16 h-16 rounded-lg overflow-hidden border border-honey-100 dark:border-slate-700
              hover:ring-2 hover:ring-honey-400 transition-shadow"
            title={photo.caption ?? 'Fotografija pregleda'}
          >
            <AuthImage photoId={photo.id} alt={photo.caption ?? 'Fotografija pregleda'} className="w-16 h-16 object-cover" />
          </button>
        ))}
      </div>

      {lightboxIndex != null && photos[lightboxIndex] && (
        <PhotoLightbox
          photos={photos}
          index={lightboxIndex}
          canManage={canManage}
          inspectionId={inspectionId}
          onNavigate={setLightboxIndex}
          onClose={() => setLightboxIndex(null)}
        />
      )}
    </>
  )
}

/** Simple full-screen modal (no new deps): image, caption, prev/next, delete. */
export function PhotoLightbox({ photos, index, canManage, inspectionId, onNavigate, onClose }: {
  photos: InspectionPhoto[]
  index: number
  canManage: boolean
  inspectionId: number
  onNavigate: (index: number) => void
  onClose: () => void
}) {
  const photo = photos[index]
  const { toast } = useToast()
  const deleteMutation = useDeleteInspectionPhoto(inspectionId)
  const analyzeMutation = useAnalyzeInspectionPhoto(inspectionId)
  const analysis = parsePhotoAnalysis(photo.analysisJson)

  const handleAnalyze = async () => {
    try {
      await analyzeMutation.mutateAsync(photo.id)
    } catch (e: any) {
      toast.error(
        e?.response?.data?.errors?.detail?.[0]
          ?? e?.response?.data?.detail
          ?? 'AI analiza nije uspjela. Pokušajte ponovo.',
      )
    }
  }

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
      if (e.key === 'ArrowLeft' && index > 0) onNavigate(index - 1)
      if (e.key === 'ArrowRight' && index < photos.length - 1) onNavigate(index + 1)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [index, photos.length, onNavigate, onClose])

  const handleDelete = async () => {
    if (!window.confirm('Obrisati ovu fotografiju?')) return
    try {
      await deleteMutation.mutateAsync(photo.id)
      toast.success('Fotografija obrisana.')
      if (photos.length <= 1) onClose()
      else if (index >= photos.length - 1) onNavigate(index - 1)
    } catch {
      toast.error('Greška pri brisanju fotografije.')
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4 animate-fade-in"
      onClick={onClose}
    >
      <div
        className="relative max-w-3xl w-full max-h-[90vh] flex flex-col"
        onClick={e => e.stopPropagation()}
      >
        {/* Toolbar */}
        <div className="flex items-center justify-between mb-2 text-white">
          <span className="text-sm text-white/70">
            {index + 1} / {photos.length}
          </span>
          <div className="flex items-center gap-2">
            {canManage && (
              <button
                type="button"
                onClick={handleDelete}
                disabled={deleteMutation.isPending}
                className="p-2 rounded-lg hover:bg-white/10 text-white/80 hover:text-red-400 transition-colors"
                title="Obriši fotografiju"
              >
                {deleteMutation.isPending
                  ? <Loader2 className="w-5 h-5 animate-spin" />
                  : <Trash2 className="w-5 h-5" />}
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              className="p-2 rounded-lg hover:bg-white/10 text-white/80 transition-colors"
              title="Zatvori"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Image */}
        <div className="relative flex-1 min-h-0 flex items-center justify-center">
          {index > 0 && (
            <button
              type="button"
              onClick={() => onNavigate(index - 1)}
              className="absolute left-2 z-10 p-2 rounded-full bg-black/50 hover:bg-black/70 text-white transition-colors"
              title="Prethodna"
            >
              <ChevronLeft className="w-6 h-6" />
            </button>
          )}
          <AuthImage
            photoId={photo.id}
            alt={photo.caption ?? 'Fotografija pregleda'}
            className="max-h-[70vh] max-w-full object-contain rounded-xl"
          />
          {index < photos.length - 1 && (
            <button
              type="button"
              onClick={() => onNavigate(index + 1)}
              className="absolute right-2 z-10 p-2 rounded-full bg-black/50 hover:bg-black/70 text-white transition-colors"
              title="Sljedeća"
            >
              <ChevronRight className="w-6 h-6" />
            </button>
          )}
        </div>

        {/* Caption */}
        {photo.caption && (
          <p className="mt-2 text-sm text-white/80 text-center">{photo.caption}</p>
        )}

        {/* AI analysis (SPEC-05 Phase 2) */}
        <div className="mt-3">
          {canManage && (
            <button
              type="button"
              onClick={handleAnalyze}
              disabled={analyzeMutation.isPending}
              className="w-full flex items-center justify-center gap-2 rounded-xl px-4 py-2.5 text-sm font-medium
                bg-honey-500 hover:bg-honey-600 text-white transition-colors
                disabled:opacity-60 disabled:cursor-not-allowed"
            >
              {analyzeMutation.isPending ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Analiziram fotografiju…
                </>
              ) : (
                <>
                  <Sparkles className="w-4 h-4" />
                  {analysis ? 'Ponovo analiziraj (AI)' : 'Analiziraj (AI)'}
                </>
              )}
            </button>
          )}

          {analysis && !analyzeMutation.isPending && <AnalysisPanel analysis={analysis} />}
        </div>
      </div>
    </div>
  )
}

/** Result panel for the AI frame analysis: pattern stars, chips, summary + guardrail footer. */
function AnalysisPanel({ analysis }: { analysis: PhotoAnalysis }) {
  return (
    <div className="mt-3 rounded-xl bg-slate-900/90 border border-white/10 p-4 text-white max-h-[30vh] overflow-y-auto">
      {!analysis.isFramePhoto ? (
        <p className="text-sm text-amber-300">
          Fotografija ne izgleda kao okvir saća iz košnice, pa procjena nije moguća.
          {analysis.summary && <span className="block mt-1 text-white/70">{analysis.summary}</span>}
        </p>
      ) : (
        <div className="space-y-2.5">
          {analysis.broodPattern != null && (
            <div className="flex items-center gap-2 text-sm">
              <span className="text-white/70">Obrazac legla:</span>
              <span className="text-honey-400 tracking-wider" aria-label={`${analysis.broodPattern} od 5`}>
                {'★'.repeat(analysis.broodPattern)}
                <span className="text-white/25">{'★'.repeat(5 - analysis.broodPattern)}</span>
              </span>
              <span className="text-white/50 text-xs">({analysis.broodPattern}/5)</span>
            </div>
          )}

          <div className="flex flex-wrap gap-1.5">
            {analysis.queenCellsVisible != null && (
              <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                analysis.queenCellsVisible
                  ? 'bg-red-500/20 text-red-300'
                  : 'bg-emerald-500/20 text-emerald-300'
              }`}>
                {analysis.queenCellsVisible ? 'Matičnjaci uočeni' : 'Matičnjaci nisu uočeni'}
              </span>
            )}
            {analysis.anomalies.map(a => (
              <span key={a} className="px-2 py-0.5 rounded-full text-xs font-medium bg-amber-500/20 text-amber-300">
                {a}
              </span>
            ))}
          </div>

          {analysis.summary && (
            <p className="text-sm text-white/80 leading-relaxed">{analysis.summary}</p>
          )}
        </div>
      )}

      <p className="mt-3 pt-2 border-t border-white/10 text-[11px] text-white/50">
        Analiza je informativna i ne zamjenjuje pregled stručnjaka.
      </p>
    </div>
  )
}
