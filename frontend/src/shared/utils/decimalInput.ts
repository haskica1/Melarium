/**
 * Decimal (money / weight) form fields.
 *
 * `<input type="number">` only ever accepts a *valid floating-point number* as defined by HTML,
 * and that grammar allows exactly one decimal separator: `.`. On an iPhone whose keyboard is set
 * to Bosnian/Croatian the numeric keypad's decimal key emits `,`, and the type=number value
 * sanitisation algorithm then makes `input.value` return an **empty string** — so the keystroke is
 * silently thrown away and a decimal simply cannot be entered ("ne mogu dodati decimalni broj").
 * `inputMode="decimal"` does not rescue it: the keypad layout follows the *type*, not the hint,
 * while the type stays `number`.
 *
 * Every decimal field is therefore a `type="text"` + `inputMode="decimal"` pair that accepts both
 * separators and normalises to `.` when the value is read. That is engine-independent — nothing
 * here depends on a WebKit quirk — so it behaves identically on desktop, Android and iOS.
 *
 * Integer-only fields (trajanje u danima, karenca, godina matice, mm za QR) keep `type="number"`:
 * they have no separator to type, so they are unaffected.
 */

/** Spread onto a decimal input in place of `type="number" step="0.01" min="0"`. */
export const decimalInputProps = {
  type: 'text',
  inputMode: 'decimal' as const,
  autoComplete: 'off',
}

/**
 * Strips anything that cannot belong to a decimal and keeps at most one separator.
 *
 * A trailing separator is deliberately preserved so that "12," survives the keystroke that
 * produced it — rewriting the character under the user's finger moves the caret and is why
 * "normalise while typing" approaches feel broken on a phone.
 */
export function sanitizeDecimal(raw: string): string {
  const cleaned = raw.replace(/[^\d.,]/g, '')
  const first = cleaned.search(/[.,]/)
  if (first === -1) return cleaned
  return cleaned.slice(0, first + 1) + cleaned.slice(first + 1).replace(/[.,]/g, '')
}

/**
 * "12,50" | "12.50" | "12," → 12.5 / 12. Returns `NaN` for empty or unparseable input, matching
 * `parseFloat` so existing `|| 0` and `isNaN` call sites keep their meaning.
 */
export function parseDecimal(raw: string | number | null | undefined): number {
  if (raw == null) return NaN
  if (typeof raw === 'number') return raw
  const normalized = raw.trim().replace(',', '.')
  if (normalized === '' || normalized === '.') return NaN
  const parsed = Number(normalized)
  return Number.isFinite(parsed) ? parsed : NaN
}
