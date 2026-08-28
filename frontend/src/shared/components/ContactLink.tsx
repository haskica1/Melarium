import { useState } from 'react'
import { Headset } from 'lucide-react'
import clsx from 'clsx'
import ContactModal from './ContactModal'

/**
 * Self-contained contact trigger for the signed-out screens (SPEC-20). It owns its own modal state
 * because those pages have no `Layout` above them to hold it — inside the app, `Layout` renders a
 * single `ContactModal` and opens it from the footer and the profile menus instead.
 */
export default function ContactLink({ className }: { className?: string }) {
  const [open, setOpen] = useState(false)

  return (
    <>
      <div className={clsx('text-center', className)}>
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium
            text-gray-600 dark:text-slate-300 bg-white/70 dark:bg-slate-900/70 backdrop-blur
            border border-honey-200 dark:border-slate-700
            hover:text-honey-700 dark:hover:text-honey-300 hover:border-honey-300 dark:hover:border-honey-500/40
            transition-colors"
        >
          <Headset className="w-4 h-4" />
          Trebate pomoć? Kontaktirajte nas
        </button>
      </div>

      <ContactModal open={open} onClose={() => setOpen(false)} />
    </>
  )
}
