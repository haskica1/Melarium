import { useEffect } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, Loader2, Paperclip, Pause, Play, Square, Volume2 } from 'lucide-react'
import { useLearningTopic, useMarkTopicRead } from '../../core/services/learningQueries'
import { MonthLabels } from '../../core/models'
import { useSpeech } from '../../core/hooks/useSpeech'
import { ErrorMessage } from '../../shared/components'
import { MarkdownArticle } from './MarkdownArticle'
import { stripMarkdown } from './stripMarkdown'

const READ_AFTER_MS = 5_000

export default function LearningTopicPage() {
  const { id } = useParams<{ id: string }>()
  const topicId = id ? parseInt(id) : 0

  const { data: topic, isLoading, isError } = useLearningTopic(topicId)
  const markRead = useMarkTopicRead()
  const { isSupported, status, speak, pause, resume, stop } = useSpeech()

  // "Read" means the topic stayed open for ~5 s — a misclick isn't a read.
  useEffect(() => {
    if (!topic || topic.isRead) return
    const timer = setTimeout(() => markRead.mutate(topic.id), READ_AFTER_MS)
    return () => clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [topic?.id, topic?.isRead])

  if (isLoading) {
    return (
      <div className="flex justify-center py-20">
        <Loader2 className="w-6 h-6 animate-spin text-honey-500" />
      </div>
    )
  }

  if (isError || !topic) {
    return <ErrorMessage message="Tema nije pronađena ili više nije objavljena." />
  }

  function handleListen() {
    if (status === 'idle') speak(`${topic!.title}. ${stripMarkdown(topic!.bodyMarkdown)}`)
    else if (status === 'speaking') pause()
    else resume()
  }

  return (
    <div className="max-w-3xl mx-auto animate-fade-in space-y-5">
      <Link
        to="/learning"
        className="inline-flex items-center gap-1.5 text-sm font-medium text-gray-500 dark:text-slate-400 hover:text-honey-600 dark:hover:text-honey-400 transition-colors"
      >
        <ArrowLeft className="w-4 h-4" /> Nazad na edukaciju
      </Link>

      <div className="bg-white dark:bg-slate-900 rounded-2xl border border-honey-100 dark:border-slate-800 shadow-sm dark:shadow-none px-6 sm:px-8 py-7">
        {/* Header */}
        <div className="flex items-start justify-between gap-3 flex-wrap">
          <div className="min-w-0">
            <h1 className="font-display text-2xl font-bold text-gray-900 dark:text-slate-50">{topic.title}</h1>
            <div className="mt-2 flex items-center gap-2 flex-wrap">
              <span className="text-xs text-honey-700 dark:text-honey-300 bg-honey-100 dark:bg-honey-500/15 rounded-full px-2 py-0.5">
                {topic.categoryName}
              </span>
              {topic.months && topic.months.length > 0 && (
                <span className="text-xs text-gray-500 dark:text-slate-400 bg-gray-100 dark:bg-slate-800 rounded-full px-2 py-0.5">
                  {topic.months.map(m => MonthLabels[m - 1]).join(', ')}
                </span>
              )}
              {topic.isRead && (
                <span className="inline-flex items-center gap-1 text-xs text-emerald-600 dark:text-emerald-400">
                  <CheckCircle2 className="w-3.5 h-3.5" /> Pročitano
                </span>
              )}
            </div>
          </div>

          {/* Listen controls */}
          {isSupported && (
            <div className="flex items-center gap-2 shrink-0">
              <button
                onClick={handleListen}
                className="flex items-center gap-1.5 px-3 py-2 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
              >
                {status === 'speaking'
                  ? <><Pause className="w-4 h-4" /> Pauziraj</>
                  : status === 'paused'
                    ? <><Play className="w-4 h-4" /> Nastavi</>
                    : <><Volume2 className="w-4 h-4" /> Poslušaj</>}
              </button>
              {status !== 'idle' && (
                <button
                  onClick={stop}
                  className="p-2 rounded-xl border border-gray-200 dark:border-slate-700 text-gray-500 dark:text-slate-300 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
                  aria-label="Zaustavi čitanje"
                >
                  <Square className="w-4 h-4" />
                </button>
              )}
            </div>
          )}
        </div>
        {isSupported && (
          <p className="mt-2 text-xs text-gray-400 dark:text-slate-500">Kvalitet glasa zavisi od uređaja.</p>
        )}

        {/* Video / file attachment */}
        {(topic.videoUrl || topic.fileUrl) && (
          <div className="mt-6 border-t border-gray-100 dark:border-slate-800 pt-6 space-y-4">
            {topic.videoUrl && <VideoAttachment url={topic.videoUrl} title={topic.title} />}
            {topic.fileUrl && (
              <a
                href={topic.fileUrl}
                target="_blank"
                rel="noreferrer"
                className="flex items-center gap-2.5 px-4 py-3 rounded-xl border border-honey-200 dark:border-slate-700 bg-honey-50 dark:bg-slate-800/60 text-sm font-medium text-honey-800 dark:text-honey-300 hover:bg-honey-100 dark:hover:bg-slate-800 transition-colors"
              >
                <Paperclip className="w-4 h-4 shrink-0" />
                {topic.fileName || 'Otvori prilog'}
              </a>
            )}
          </div>
        )}

        {/* Article — react-markdown with default escaping (no raw HTML) as the XSS guard */}
        <div className="mt-6 border-t border-gray-100 dark:border-slate-800 pt-6">
          <MarkdownArticle markdown={topic.bodyMarkdown} />
        </div>
      </div>
    </div>
  )
}

// ── Video attachment ───────────────────────────────────────────────────────────

/** YouTube/Vimeo links render as an embedded player; anything else is treated as a direct video file. */
function VideoAttachment({ url, title }: { url: string; title: string }) {
  const embedUrl = toEmbedUrl(url)
  return (
    <div className="relative w-full rounded-xl overflow-hidden bg-black" style={{ paddingTop: '56.25%' }}>
      {embedUrl ? (
        <iframe
          src={embedUrl}
          title={title}
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
          allowFullScreen
          className="absolute inset-0 w-full h-full border-0"
        />
      ) : (
        <video src={url} controls className="absolute inset-0 w-full h-full" />
      )}
    </div>
  )
}

/** YouTube/Vimeo URL → embeddable player URL; null means "not a known provider" (render as `<video>`). */
function toEmbedUrl(url: string): string | null {
  let u: URL
  try {
    u = new URL(url)
  } catch {
    return null
  }
  const host = u.hostname.replace(/^www\.|^m\./, '')

  if (host === 'youtube.com') {
    const id =
      u.pathname === '/watch' ? u.searchParams.get('v')
      : u.pathname.startsWith('/embed/') ? u.pathname.split('/')[2]
      : u.pathname.startsWith('/shorts/') ? u.pathname.split('/')[2]
      : null
    return id ? `https://www.youtube.com/embed/${id}` : null
  }
  if (host === 'youtu.be') {
    const id = u.pathname.slice(1)
    return id ? `https://www.youtube.com/embed/${id}` : null
  }
  if (host === 'vimeo.com') {
    const id = u.pathname.slice(1).split('/')[0]
    return id ? `https://player.vimeo.com/video/${id}` : null
  }
  return null
}
