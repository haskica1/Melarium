import { useState } from 'react'
import { CircleHelp, Layers, Sparkles } from 'lucide-react'
import { Modal } from './Modal'

interface Slide {
  icon: React.ReactNode
  title: string
  body: string
}

const SLIDES: Slide[] = [
  {
    icon: <Sparkles className="w-6 h-6" />,
    title: 'Dobro došli u Melarium',
    body:
      'Melarium je vaša evidencija pčelarstva: pregledi, matice, prehrana, tretmani i prinos na jednom mjestu — i na telefonu, i bez signala na terenu.',
  },
  {
    icon: <Layers className="w-6 h-6" />,
    title: 'Tri nivoa, i to je sve',
    body:
      'Pčelinjak je lokacija. U pčelinjaku su košnice. U košnici su pregledi. Skoro sve u aplikaciji visi na toj jednoj hijerarhiji — kad je shvatite, ostalo je lako.',
  },
  {
    icon: <CircleHelp className="w-6 h-6" />,
    title: 'Pomoć je uvijek tu',
    body:
      'Ikona znaka pitanja u gornjem meniju objašnjava stranicu na kojoj se nalazite — na svakoj stranici, kad god vam treba. Na tastaturi je prečica „?“.',
  },
]

interface WelcomeModalProps {
  open: boolean
  /**
   * `skipAutoOpen` is true when the user chose to skip the intro, which also suppresses the
   * per-page auto-open — see `useHelp.finishWelcome`.
   */
  onFinish: (skipAutoOpen: boolean) => void
}

/**
 * Shown once per user, per browser. It doubles as the announcement of the in-app help for people who
 * were already using Melarium before it existed. Open state is owned by `useHelp` so this can never
 * overlap the help panel.
 */
export default function WelcomeModal({ open, onFinish }: WelcomeModalProps) {
  const [index, setIndex] = useState(0)

  const slide = SLIDES[index]
  const isLast = index === SLIDES.length - 1

  if (!open) return null

  return (
    <Modal
      open={open}
      onClose={() => onFinish(true)}
      title={slide.title}
      size="md"
      closeOnBackdropClick={false}
      icon={
        <div className="w-10 h-10 bg-honey-100 dark:bg-honey-500/15 rounded-full flex items-center justify-center text-honey-600 dark:text-honey-400">
          {slide.icon}
        </div>
      }
      footer={
        <div className="flex items-center gap-3">
          <div className="flex gap-1.5" aria-hidden="true">
            {SLIDES.map((_, i) => (
              <span
                key={i}
                className={`w-1.5 h-1.5 rounded-full ${
                  i === index ? 'bg-honey-500' : 'bg-gray-200 dark:bg-slate-700'
                }`}
              />
            ))}
          </div>
          {isLast ? (
            <button onClick={() => onFinish(false)} className="btn-primary text-sm ml-auto">
              Počnimo
            </button>
          ) : (
            <>
              <button
                onClick={() => onFinish(true)}
                className="text-xs text-gray-500 dark:text-slate-400 hover:underline ml-auto"
              >
                Preskoči uvod
              </button>
              <button onClick={() => setIndex(i => i + 1)} className="btn-primary text-sm">
                Dalje
              </button>
            </>
          )}
        </div>
      }
    >
      <p className="text-sm text-gray-700 dark:text-slate-200 leading-relaxed">{slide.body}</p>
    </Modal>
  )
}
