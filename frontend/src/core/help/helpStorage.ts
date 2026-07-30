/**
 * Per-user, per-browser help flags. Keyed by the signed-in user's e-mail because the client session
 * carries no numeric user id (same reason the offline outbox keys by e-mail).
 *
 * Every access is wrapped: in Safari private mode `localStorage` can throw on read *and* write, and
 * a help preference must never be able to break the header it lives in.
 */

const PREFIX = 'melarium-help'

function read(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function write(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch {
    // Storage unavailable or full — the flag just doesn't persist. Not worth surfacing.
  }
}

function scoped(name: string, email: string | undefined): string {
  return `${PREFIX}-${name}:${email ?? 'anon'}`
}

// ── Welcome flow ──────────────────────────────────────────────────────────────

export function hasSeenWelcome(email: string | undefined): boolean {
  return read(scoped('welcome', email)) === '1'
}

export function markWelcomeSeen(email: string | undefined): void {
  write(scoped('welcome', email), '1')
}

// ── Auto-open preference ──────────────────────────────────────────────────────

export function isAutoOpenEnabled(email: string | undefined): boolean {
  return read(scoped('autoopen', email)) !== 'false'
}

export function setAutoOpenEnabled(email: string | undefined, enabled: boolean): void {
  write(scoped('autoopen', email), String(enabled))
}

// ── Which pages have already auto-opened ──────────────────────────────────────

export function getAutoOpenedKeys(email: string | undefined): string[] {
  const raw = read(scoped('opened', email))
  if (!raw) return []
  try {
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed.filter((k): k is string => typeof k === 'string') : []
  } catch {
    return []
  }
}

export function markAutoOpened(email: string | undefined, keys: string[]): void {
  const merged = [...new Set([...getAutoOpenedKeys(email), ...keys])]
  write(scoped('opened', email), JSON.stringify(merged))
}

// ── "Has the user ever opened help" — drives the attention dot on the icon ────

export function hasEverOpenedHelp(email: string | undefined): boolean {
  return read(scoped('used', email)) === '1'
}

export function markHelpUsed(email: string | undefined): void {
  write(scoped('used', email), '1')
}
