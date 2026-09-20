import { useCallback, useState } from 'react'
import type { ReportFormat, ReportSections } from '../../core/models'

// The section selection and the export format are remembered per browser (SPEC-25). A beekeeper
// files the same kind of report every season; re-ticking the same boxes each time is the whole
// annoyance the checkboxes were meant to remove.
//
// localStorage can throw outright (private windows, site data blocked), so every read and write is
// guarded and falls back to the defaults — losing a preference must never break the page.

const SECTIONS_KEY = 'melarium-report-sections'
const FORMAT_KEY = 'melarium-report-format'

export const DEFAULT_SECTIONS: ReportSections = {
  yield: true,
  yieldByApiary: true,
  yieldByHoneyType: true,
  // Off by default: one row per hive, and an operation with 40 hives spends a whole page on it.
  yieldByBeehive: false,
  yieldByPasture: true,
  expenses: true,
  balance: true,
  treatments: true,
}

/** Section labels, in document order. The yield sub-tables are nested under `yield`. */
export const SECTION_GROUPS: Array<{
  key: keyof ReportSections
  label: string
  children?: Array<{ key: keyof ReportSections; label: string }>
}> = [
  {
    key: 'yield',
    label: 'Prinos',
    children: [
      { key: 'yieldByApiary', label: 'Po pčelinjaku' },
      { key: 'yieldByHoneyType', label: 'Po vrsti meda' },
      { key: 'yieldByBeehive', label: 'Po košnici' },
      { key: 'yieldByPasture', label: 'Po pašnjaku' },
    ],
  },
  { key: 'expenses', label: 'Troškovi' },
  { key: 'balance', label: 'Bilansa' },
  { key: 'treatments', label: 'Tretmani' },
]

/** True when at least one top-level section is on — an export with none would be a header and a
 *  signature line. */
export function hasAnySection(s: ReportSections): boolean {
  return s.yield || s.expenses || s.balance || s.treatments
}

function readSections(): ReportSections {
  try {
    const raw = localStorage.getItem(SECTIONS_KEY)
    if (!raw) return DEFAULT_SECTIONS
    const parsed = JSON.parse(raw) as Partial<ReportSections>
    // Merged onto the defaults rather than used as-is: a stored object written before a new section
    // existed would otherwise leave that section permanently `undefined`, i.e. silently off.
    const merged = { ...DEFAULT_SECTIONS } as ReportSections
    for (const key of Object.keys(DEFAULT_SECTIONS) as Array<keyof ReportSections>)
      if (typeof parsed[key] === 'boolean') merged[key] = parsed[key]
    return hasAnySection(merged) ? merged : DEFAULT_SECTIONS
  } catch {
    return DEFAULT_SECTIONS
  }
}

function readFormat(): ReportFormat {
  try {
    return localStorage.getItem(FORMAT_KEY) === 'xlsx' ? 'xlsx' : 'pdf'
  } catch {
    return 'pdf'
  }
}

export function useReportPreferences() {
  const [sections, setSectionsState] = useState<ReportSections>(readSections)
  const [format, setFormatState] = useState<ReportFormat>(readFormat)

  const setSections = useCallback((next: ReportSections) => {
    setSectionsState(next)
    try { localStorage.setItem(SECTIONS_KEY, JSON.stringify(next)) } catch { /* not worth failing over */ }
  }, [])

  const toggleSection = useCallback((key: keyof ReportSections) => {
    setSectionsState(prev => {
      const next = { ...prev, [key]: !prev[key] }
      // Unticking a parent takes its sub-tables with it, and ticking it back brings them back —
      // otherwise "Prinos" could be on with every table under it off, printing an empty heading.
      if (key === 'yield')
        for (const child of SECTION_GROUPS[0].children!) next[child.key] = next.yield
      try { localStorage.setItem(SECTIONS_KEY, JSON.stringify(next)) } catch { /* ignore */ }
      return next
    })
  }, [])

  const setFormat = useCallback((next: ReportFormat) => {
    setFormatState(next)
    try { localStorage.setItem(FORMAT_KEY, next) } catch { /* ignore */ }
  }, [])

  return { sections, setSections, toggleSection, format, setFormat }
}
