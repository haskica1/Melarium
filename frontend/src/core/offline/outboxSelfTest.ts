import { HoneyLevel } from '../models'
import {
  enqueueInspection,
  getOutboxItem,
  readOutbox,
  removeOutboxItem,
  updateOutboxItem,
} from './outbox'

// Focused unit tests for the outbox wrapper (SPEC-07) as a manual dev harness — the frontend has
// no test runner and adding vitest+fake-indexeddb was out of scope (packages need approval).
// Runs against the real browser IndexedDB with sentinel owners and cleans up after itself.
// Usage: dev build → browser console → `await __outboxSelfTest()`.

const OWNER = 'selftest-a@outbox.local'
const OTHER = 'selftest-b@outbox.local'

const sleep = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms))

function payload(date: string) {
  return { date, honeyLevel: HoneyLevel.Medium, beehiveId: 999_999, notes: 'selftest' }
}

export async function runOutboxSelfTest(): Promise<boolean> {
  const results: { test: string; ok: boolean }[] = []
  const check = (test: string, ok: boolean) => { results.push({ test, ok }) }

  const cleanup = async () => {
    for (const owner of [OWNER, OTHER]) {
      for (const item of await readOutbox(owner)) await removeOutboxItem(item.localId)
    }
  }

  try {
    await cleanup() // start from a clean slate in case a previous run crashed

    // enqueue → item lands with pending status and a fresh localId
    const first = await enqueueInspection({
      ownerEmail: OWNER, beehiveId: 999_999, beehiveName: 'Selftest 1', payload: payload('2026-07-01'),
    })
    await sleep(10) // distinct createdAt so the ordering assertion is deterministic
    const second = await enqueueInspection({
      ownerEmail: OWNER, beehiveId: 999_999, beehiveName: 'Selftest 2', payload: payload('2026-07-02'),
    })
    await enqueueInspection({
      ownerEmail: OTHER, beehiveId: 999_999, beehiveName: 'Tuđi item', payload: payload('2026-07-03'),
    })

    check('enqueue sets pending status + localId', first.status === 'pending' && first.localId.length > 0)

    // readOutbox → only the owner's items, oldest first
    const mine = await readOutbox(OWNER)
    check('readOutbox returns only the owner\'s items', mine.length === 2 && mine.every(i => i.ownerEmail === OWNER))
    check('readOutbox orders oldest first', mine[0]?.localId === first.localId && mine[1]?.localId === second.localId)

    const theirs = await readOutbox(OTHER)
    check('another owner sees only their own item', theirs.length === 1 && theirs[0]?.beehiveName === 'Tuđi item')

    // update → status/error persist
    await updateOutboxItem({ ...first, status: 'failed', error: 'Testna greška' })
    const reloaded = await getOutboxItem(first.localId)
    check('update persists failed status + error', reloaded?.status === 'failed' && reloaded?.error === 'Testna greška')

    // remove → gone; a second remove is a harmless no-op
    await removeOutboxItem(first.localId)
    check('remove deletes the item', (await getOutboxItem(first.localId)) === undefined)
    let doubleRemoveOk = true
    try { await removeOutboxItem(first.localId) } catch { doubleRemoveOk = false }
    check('double remove does not throw', doubleRemoveOk)
  } finally {
    await cleanup()
  }

  console.table(results)
  const allOk = results.every(r => r.ok)
  console.log(allOk ? '✅ Outbox self-test: sve prošlo' : '❌ Outbox self-test: ima padova')
  return allOk
}
