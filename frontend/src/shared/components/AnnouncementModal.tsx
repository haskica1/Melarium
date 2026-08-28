import { Sparkles } from 'lucide-react'
import { Modal } from './Modal'
import { MarkdownMessage } from './MarkdownMessage'
import { ANNOUNCEMENT_TYPE_CLASS } from './announcementType'
import type { AnnouncementDetail } from '../../core/models'

interface AnnouncementModalProps {
  announcement: AnnouncementDetail | null
  open: boolean
  /**
   * Closing *is* dismissing (SPEC-21 D2) — the caller marks the announcement seen here. There is no
   * separate "read" and "dismissed" state to keep in sync.
   */
  onClose: () => void
}

/** Full text of one announcement. Title + markdown body only — no image, no CTA (D6). */
export default function AnnouncementModal({ announcement, open, onClose }: AnnouncementModalProps) {
  if (!announcement) return null

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={announcement.title}
      size="lg"
      icon={
        <div className="w-10 h-10 bg-honey-100 dark:bg-honey-500/15 rounded-full flex items-center justify-center text-honey-600 dark:text-honey-400">
          <Sparkles className="w-5 h-5" />
        </div>
      }
      footer={
        <button onClick={onClose} className="btn-primary text-sm ml-auto">
          Zatvori
        </button>
      }
    >
      <div className="space-y-4">
        <div className="flex items-center gap-2 flex-wrap">
          <span className={`text-xs font-medium rounded-full px-2 py-0.5 ${ANNOUNCEMENT_TYPE_CLASS[announcement.type]}`}>
            {announcement.typeName}
          </span>
          {announcement.publishedAt && (
            <span className="text-xs text-gray-400 dark:text-slate-500">
              {new Date(announcement.publishedAt).toLocaleDateString('bs-BA')}
            </span>
          )}
        </div>

        <div className="text-[15px] leading-relaxed text-gray-700 dark:text-slate-300 break-words">
          <MarkdownMessage content={announcement.bodyMarkdown} />
        </div>
      </div>
    </Modal>
  )
}
