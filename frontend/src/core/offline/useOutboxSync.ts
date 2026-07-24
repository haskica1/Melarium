import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../context/AuthContext'
import { useToast } from '../context/ToastContext'
import { flushOutbox } from './syncOutbox'

/**
 * Mounts the outbox sync triggers (SPEC-07): one flush when the app (re)gains an authenticated
 * user, and one on every `window 'online'` event. Mounted once in Layout — which only renders
 * behind ProtectedRoute, so auth is already resolved.
 */
export function useOutboxSync(): void {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const { toast } = useToast()
  const email = user?.email

  useEffect(() => {
    if (!email) return
    let disposed = false

    const flush = async () => {
      const result = await flushOutbox(email)
      if (disposed || result.synced === 0) return
      toast.success(`Sinhronizovano ${result.synced} pregleda.`)
      void queryClient.invalidateQueries({ queryKey: ['inspections'] })
      void queryClient.invalidateQueries({ queryKey: ['beehives'] })
    }

    void flush()
    const onOnline = () => { void flush() }
    window.addEventListener('online', onOnline)
    return () => {
      disposed = true
      window.removeEventListener('online', onOnline)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [email])
}
