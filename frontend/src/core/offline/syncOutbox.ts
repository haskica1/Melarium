import { isAxiosError } from 'axios'
import { inspectionService } from '../services/beehiveService'
import { readOutbox, removeOutboxItem, updateOutboxItem } from './outbox'

// Sync engine (SPEC-07): flushes the current user's pending outbox items, oldest first, through the
// normal inspectionService.create — so auth refresh, interceptors, and validation behave exactly
// like an online submit. Deliberately not workbox-background-sync (see ADR in decisions.md).

export interface FlushResult {
  synced: number
  failed: number
  /** True when the flush stopped early because the network is (still) down. */
  stoppedOffline: boolean
}

const NOTHING: FlushResult = { synced: 0, failed: 0, stoppedOffline: false }

let inFlight: Promise<FlushResult> | null = null

/**
 * Single-flight: concurrent calls in this tab share one promise; across tabs the Web Locks API
 * ensures only one tab flushes (the other resolves immediately with zeros).
 */
export function flushOutbox(ownerEmail: string): Promise<FlushResult> {
  inFlight ??= run(ownerEmail).finally(() => { inFlight = null })
  return inFlight
}

async function run(ownerEmail: string): Promise<FlushResult> {
  if (typeof navigator !== 'undefined' && navigator.locks?.request) {
    return await navigator.locks.request(
      'beehive-outbox-flush',
      { ifAvailable: true },
      async (lock) => (lock ? flushSequential(ownerEmail) : NOTHING),
    )
  }
  return flushSequential(ownerEmail)
}

async function flushSequential(ownerEmail: string): Promise<FlushResult> {
  const pending = (await readOutbox(ownerEmail)).filter(i => i.status === 'pending')
  const result: FlushResult = { synced: 0, failed: 0, stoppedOffline: false }

  for (const item of pending) {
    if (!navigator.onLine) {
      result.stoppedOffline = true
      return result
    }
    try {
      await inspectionService.create(item.payload)
      await removeOutboxItem(item.localId)
      result.synced++
    } catch (err) {
      if (isNetworkError(err)) {
        // Still offline (or the connection dropped mid-flush) — keep the item pending and stop.
        result.stoppedOffline = true
        return result
      }
      // A real HTTP rejection (400/403/404…): auto-retry would loop forever — park it as failed
      // with the API's message so the user can edit or discard it on the outbox page.
      await updateOutboxItem({ ...item, status: 'failed', error: extractApiMessage(err) })
      result.failed++
    }
  }
  return result
}

/** Network-level failure = axios error without a response (offline, DNS, timeout, aborted). */
export function isNetworkError(err: unknown): boolean {
  return isAxiosError(err) && !err.response
}

export function extractApiMessage(err: unknown): string {
  if (isAxiosError(err) && err.response) {
    const data: unknown = err.response.data
    if (data && typeof data === 'object') {
      const obj = data as Record<string, unknown>
      if (typeof obj.detail === 'string') return obj.detail
      // FluentValidation dictionary: { field: ["message", …] } — possibly nested under `errors`.
      const source = (obj.errors && typeof obj.errors === 'object' ? obj.errors : obj) as Record<string, unknown>
      for (const value of Object.values(source)) {
        if (Array.isArray(value) && typeof value[0] === 'string') return value[0]
      }
    }
    if (err.response.status === 403) return 'Nemate pravo zabilježiti pregled za ovu košnicu.'
    if (err.response.status === 404) return 'Košnica više ne postoji.'
  }
  return 'Server je odbio pregled. Uredite ga i pokušajte ponovo.'
}
