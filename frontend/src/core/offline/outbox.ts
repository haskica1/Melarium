import type { CreateInspectionPayload } from '../models'

// Offline outbox for inspections (SPEC-07): IndexedDB (not localStorage — multi-tab safety, size
// limits), hand-rolled wrapper instead of the `idb` package. Items are keyed to the owner's email
// because the client session carries no numeric user id and the spec keeps the backend unchanged;
// email gives the same per-user isolation. Entries are plaintext in the browser profile — the same
// trust level as the stored refresh token (ADR-009).

export type OutboxStatus = 'pending' | 'failed'

export interface OutboxItem {
  localId: string
  ownerEmail: string
  beehiveId: number
  beehiveName: string
  payload: CreateInspectionPayload
  createdAt: string
  status: OutboxStatus
  error?: string
}

const DB_NAME = 'beehive-offline'
const STORE = 'outbox'
const CHANNEL_NAME = 'beehive-outbox'

// ── IndexedDB plumbing ──────────────────────────────────────────────────────────

let dbPromise: Promise<IDBDatabase> | null = null

function openDb(): Promise<IDBDatabase> {
  dbPromise ??= new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1)
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(STORE)) {
        request.result.createObjectStore(STORE, { keyPath: 'localId' })
      }
    }
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('IndexedDB open failed'))
  })
  return dbPromise
}

function asPromise<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('IndexedDB request failed'))
  })
}

async function store(mode: IDBTransactionMode): Promise<IDBObjectStore> {
  const db = await openDb()
  return db.transaction(STORE, mode).objectStore(STORE)
}

// ── In-memory mirror (sync snapshot for useSyncExternalStore) ───────────────────

let mirror: OutboxItem[] = []
const listeners = new Set<() => void>()

// Cross-tab: IndexedDB has no change events, so mutations are announced on a BroadcastChannel
// and every tab refreshes its mirror.
const channel = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel(CHANNEL_NAME) : null
if (channel) channel.onmessage = () => { void refreshMirror() }

async function refreshMirror(): Promise<void> {
  const items = await asPromise((await store('readonly')).getAll() as IDBRequest<OutboxItem[]>)
  mirror = items.sort((a, b) => a.createdAt.localeCompare(b.createdAt))
  for (const listener of listeners) listener()
}

async function mutated(): Promise<void> {
  await refreshMirror()
  channel?.postMessage('changed')
}

// Hydrate the mirror once on module load (badge shows the right count on first paint).
if (typeof indexedDB !== 'undefined') void refreshMirror().catch(() => { /* unsupported browser — outbox stays empty */ })

export function subscribeOutbox(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/** Stable snapshot of every stored item (all owners), oldest first. Filter per user in the caller. */
export function getOutboxSnapshot(): OutboxItem[] {
  return mirror
}

// ── Public API ──────────────────────────────────────────────────────────────────

function newLocalId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(36).slice(2)}`
}

export async function enqueueInspection(
  input: Pick<OutboxItem, 'ownerEmail' | 'beehiveId' | 'beehiveName' | 'payload'>,
): Promise<OutboxItem> {
  const item: OutboxItem = {
    ...input,
    localId: newLocalId(),
    createdAt: new Date().toISOString(),
    status: 'pending',
  }
  await asPromise((await store('readwrite')).add(item))
  await mutated()
  return item
}

export async function updateOutboxItem(item: OutboxItem): Promise<void> {
  await asPromise((await store('readwrite')).put(item))
  await mutated()
}

export async function removeOutboxItem(localId: string): Promise<void> {
  await asPromise((await store('readwrite')).delete(localId))
  await mutated()
}

export async function getOutboxItem(localId: string): Promise<OutboxItem | undefined> {
  return await asPromise((await store('readonly')).get(localId) as IDBRequest<OutboxItem | undefined>)
}

/** Fresh read from the DB (not the mirror) — the sync engine must not act on a stale snapshot. */
export async function readOutbox(ownerEmail: string): Promise<OutboxItem[]> {
  const items = await asPromise((await store('readonly')).getAll() as IDBRequest<OutboxItem[]>)
  return items
    .filter(i => i.ownerEmail === ownerEmail)
    .sort((a, b) => a.createdAt.localeCompare(b.createdAt))
}
