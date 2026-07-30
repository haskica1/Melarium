/**
 * Bosnian plural agreement: three forms, picked by the last one/two digits.
 *   1, 21, 31 → one     ("1 košnica")
 *   2–4, 22–24 → few    ("3 košnice")
 *   0, 5–20, 25–30 → many ("7 košnica")
 */
export function plural(n: number, one: string, few: string, many: string): string {
  const abs = Math.abs(n) % 100
  const last = abs % 10
  if (last === 1 && abs !== 11) return one
  if (last >= 2 && last <= 4 && (abs < 12 || abs > 14)) return few
  return many
}

/** "12 košnica" / "1 košnica" / "3 košnice" */
export const hivesLabel = (n: number) => `${n} ${plural(n, 'košnica', 'košnice', 'košnica')}`
