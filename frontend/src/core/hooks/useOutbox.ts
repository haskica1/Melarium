import { useMemo, useSyncExternalStore } from 'react'
import { getOutboxSnapshot, subscribeOutbox, type OutboxItem } from '../offline/outbox'

/** The current user's outbox items (oldest first), live-updated across tabs (SPEC-07). */
export function useOutbox(ownerEmail: string | undefined): OutboxItem[] {
  const all = useSyncExternalStore(subscribeOutbox, getOutboxSnapshot)
  return useMemo(
    () => (ownerEmail ? all.filter(i => i.ownerEmail === ownerEmail) : []),
    [all, ownerEmail],
  )
}
