/**
 * Reduces the article markdown to plain text for text-to-speech (SPEC-06): headings, emphasis,
 * links, lists and tables read as natural sentences instead of syntax.
 */
export function stripMarkdown(md: string): string {
  return md
    .replace(/```[\s\S]*?```/g, ' ')                  // code fences
    .replace(/`([^`]+)`/g, '$1')                       // inline code
    .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ')             // images
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')           // links → text
    .replace(/^#{1,6}\s+/gm, '')                       // headings
    .replace(/^\s*[-*+]\s+/gm, '')                     // list bullets
    .replace(/^\s*\d+\.\s+/gm, '')                     // ordered lists
    .replace(/^\s*>\s?/gm, '')                         // blockquotes
    .replace(/\|/g, ', ')                              // table pipes → pauses
    .replace(/^[,\s]*[-:]{3,}[,\s-:]*$/gm, '')         // table separator rows
    .replace(/[*_~]{1,3}([^*_~]+)[*_~]{1,3}/g, '$1')   // bold/italic/strike
    .replace(/[ \t]+/g, ' ')
    .replace(/\n{3,}/g, '\n\n')
    .trim()
}
