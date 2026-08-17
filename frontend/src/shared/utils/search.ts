/**
 * Folds a string down to what someone actually types into a search box on a phone: lower case, no
 * diacritics. Without this, "cistoca" finds nothing while "čistoća" is sitting right there — and a
 * beekeeper in the field is not going to hold the č key down to reach a topic.
 *
 * NFD splits č/ć/š/ž into a base letter plus a combining mark, which the regex then strips. Đ/đ is
 * the exception: Unicode treats it as its own letter, not d + a mark, so it needs the explicit pass.
 */
export function fold(text: string): string {
  return text
    .toLowerCase()
    .replace(/đ/g, 'd')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
}

/** True when every whitespace-separated word of the query appears somewhere in the text. */
export function matchesQuery(text: string, query: string): boolean {
  const words = fold(query).split(/\s+/).filter(Boolean)
  if (words.length === 0) return true

  const haystack = fold(text)
  return words.every(word => haystack.includes(word))
}
