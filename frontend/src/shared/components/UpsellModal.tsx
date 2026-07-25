import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Sparkles } from 'lucide-react'
import { Modal } from './Modal'

/**
 * Global upsell prompt (SPEC-09). Listens for the `plan-limit` CustomEvent emitted by the
 * apiClient 402 interceptor and shows the server's Bosnian message with a link to /plans.
 * Mounted once inside the router.
 */
export default function UpsellModal() {
  const [message, setMessage] = useState<string | null>(null)
  const navigate = useNavigate()

  useEffect(() => {
    const onPlanLimit = (e: Event) => {
      const detail = (e as CustomEvent<string>).detail
      setMessage(detail || 'Ova funkcija zahtijeva nadogradnju paketa.')
    }
    window.addEventListener('plan-limit', onPlanLimit)
    return () => window.removeEventListener('plan-limit', onPlanLimit)
  }, [])

  const close = () => setMessage(null)

  const goToPlans = () => {
    close()
    navigate('/plans')
  }

  return (
    <Modal
      open={message !== null}
      onClose={close}
      title="Potrebna je nadogradnja paketa"
      size="md"
      icon={
        <div className="flex items-center justify-center w-12 h-12 rounded-full bg-honey-100 dark:bg-honey-500/15">
          <Sparkles className="w-6 h-6 text-honey-600 dark:text-honey-400" />
        </div>
      }
      footer={
        <div className="flex gap-3">
          <button type="button" onClick={close} className="btn-secondary flex-1 justify-center text-sm">
            Zatvori
          </button>
          <button type="button" onClick={goToPlans} className="btn-primary flex-1 justify-center text-sm">
            Pogledaj pakete
          </button>
        </div>
      }
    >
      <p className="text-sm text-gray-600 dark:text-slate-300 leading-relaxed">{message}</p>
    </Modal>
  )
}
