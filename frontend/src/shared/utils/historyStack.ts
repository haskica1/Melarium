/**
 * A mirror of the in-app history stack, keyed by the index React Router stamps on every entry
 * (`window.history.state.idx`).
 *
 * The History API deliberately hides *where* Back would take you, but that is exactly what a page
 * needs to know before it leaves: replacing the current entry with a page that is already the
 * previous entry leaves two adjacent entries for the same URL, and then the first Back press looks
 * like it did nothing.
 *
 * Persisted in sessionStorage because a reload — or the PWA service worker activating a new build —
 * keeps the browser's history but wipes ours. A stack that silently disagreed with the real history
 * would be worse than no stack: every read is treated as "unknown" and callers fall back to the
 * always-safe behaviour.
 */

const STORAGE_KEY = 'melarium:history-stack'

let stack: (string | undefined)[] = load()

function load(): (string | undefined)[] {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    const parsed = raw ? JSON.parse(raw) : null
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return [] // Private mode, disabled storage, or corrupt JSON — degrade to "unknown".
  }
}

function persist() {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(stack))
  } catch {
    /* Quota or disabled storage — the in-memory stack still works for this page load. */
  }
}

/** React Router's index for the current entry. 0 is the first entry of the browsing session. */
function currentIndex(): number {
  const idx = (window.history.state as { idx?: number } | null)?.idx
  return typeof idx === 'number' && idx >= 0 ? idx : 0
}

/** Record the entry the user just landed on. Called for every navigation by `HistoryTracker`. */
export function recordEntry(pathname: string) {
  const idx = currentIndex()
  stack[idx] = pathname
  // A push makes everything above the new entry unreachable; drop it so the mirror never holds a
  // path the user can no longer reach.
  if (stack.length > idx + 1) stack.length = idx + 1
  persist()
}

/** Pathname `history.back()` would land on, or `undefined` when that entry is unknown to us. */
export function previousPathname(): string | undefined {
  const idx = currentIndex()
  return idx > 0 ? stack[idx - 1] : undefined
}

/** Whether there is an in-app entry behind the current one. False on a deep link or a fresh tab. */
export function canGoBack(): boolean {
  return currentIndex() > 0
}
