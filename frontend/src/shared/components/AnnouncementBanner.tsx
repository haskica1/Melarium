import { useState } from 'react'
import { Sparkles, X } from 'lucide-react'
import AnnouncementModal from './AnnouncementModal'
import { ANNOUNCEMENT_TYPE_CLASS } from './announcementType'
import { useAnnouncementBanner, useMarkAnnouncementRead } from '../../core/services/announcementQueries'

/**
 * The "Šta je novo" banner (SPEC-21). Shows the newest published announcement and only that one —
 * never a queue (D1). The server omits it once this user has seen it, so there is no visibility
 * logic here beyond the optimistic hide.
 *
 * Type + title + "Pročitaj više", and nothing else: the body belongs in the modal (D6). A teaser
 * here only made the banner taller while still not saying enough to skip opening it.
 */
export default function AnnouncementBanner() {
  const { data } = useAnnouncementBanner()
  const markRead = useMarkAnnouncementRead()
  const [modalOpen, setModalOpen] = useState(false)
  // Hide on click rather than waiting for the round trip — the banner is the one thing on screen
  // whose whole purpose is to go away when asked.
  const [dismissed, setDismissed] = useState(false)

  const announcement = data?.announcement ?? null
  if (!announcement || dismissed) return null

  function dismiss() {
    setDismissed(true)
    markRead.mutate(announcement!.id)
  }

  return (
    <>
      <div className="group relative mb-5 flex items-stretch rounded-2xl border border-honey-200 dark:border-honey-500/30
                      bg-gradient-to-r from-honey-50 to-white dark:from-honey-500/10 dark:to-slate-900
                      shadow-sm dark:shadow-none overflow-hidden">
        {/* The hover tint is one overlay across the whole card, not a background on the text button.
            On the button it stopped at the button's own box, leaving the strip beside and below the
            "x" untinted — a visible unhovered column on the right. `bg-*` on the container itself is
            not an option either: the gradient is a background-image and paints over it. */}
        <div className="pointer-events-none absolute inset-0 bg-honey-100/50 dark:bg-honey-500/10
                        opacity-0 group-hover:opacity-100 transition-opacity" />

        {/* The body and the "x" are siblings, not nested — a button inside a button is invalid and
            the dismiss tap would bubble into "open the modal". */}
        <button
          type="button"
          onClick={() => setModalOpen(true)}
          className="relative flex-1 min-w-0 flex items-start gap-3 text-left px-4 py-3"
        >
          <div className="w-9 h-9 shrink-0 rounded-xl bg-honey-100 dark:bg-honey-500/15 text-honey-600 dark:text-honey-300 flex items-center justify-center">
            <Sparkles className="w-4 h-4" />
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2 flex-wrap">
              <span className={`text-xs font-medium rounded-full px-2 py-0.5 ${ANNOUNCEMENT_TYPE_CLASS[announcement.type]}`}>
                {announcement.typeName}
              </span>
              <h2 className="font-semibold text-sm text-gray-900 dark:text-slate-100 truncate min-w-0">
                {announcement.title}
              </h2>
            </div>
            <span className="mt-1 inline-block text-xs font-medium text-honey-700 dark:text-honey-300 underline">
              Pročitaj više
            </span>
          </div>
        </button>

        <button
          type="button"
          onClick={dismiss}
          className="relative shrink-0 self-start p-3 text-gray-400 dark:text-slate-500 hover:text-gray-600 dark:hover:text-slate-300 transition-colors"
          aria-label="Sakrij ovo obavještenje"
          title="Ne prikazuj više"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      <AnnouncementModal
        announcement={announcement}
        open={modalOpen}
        onClose={() => {
          setModalOpen(false)
          dismiss()
        }}
      />
    </>
  )
}
