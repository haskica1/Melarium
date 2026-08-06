import { matchPath } from 'react-router-dom'

/**
 * Route patterns that have a help entry, **most specific first** — `matchPath` is tried in order and
 * the first hit wins, so `/apiaries/new` must precede `/apiaries/:id` (which would otherwise swallow
 * it, exactly as in `App.tsx`'s own route ordering).
 *
 * This list is deliberately kept apart from the prose in `helpContent.ts`: the help button needs to
 * know *whether* an entry exists on every render, while the content itself is a lazily-imported
 * chunk (see `useHelp`). Keys only here — no user-facing text.
 */
export const HELP_ROUTES = [
  '/apiaries/new',
  '/apiaries/:id/edit',
  '/apiaries/:id',
  '/apiaries',
  '/beehives/new',
  '/beehives/:id/edit',
  '/beehives/:id',
  '/inspections/new',
  '/inspections/:id/edit',
  '/feedings/new',
  '/feedings/:id/edit',
  '/feedings/:id',
  '/feedings',
  '/harvests/new',
  '/harvests/:id/edit',
  '/harvests',
  '/treatments/new',
  '/treatments/:id/edit',
  '/treatments',
  '/expenses/scan',
  '/expenses/new',
  '/expenses/:id/edit',
  '/expenses',
  '/pastures',
  '/members/:id/assignments',
  '/members',
  '/calendar/settings',
  '/calendar',
  '/advisor',
  '/learning/:id',
  '/learning',
  '/stats',
  '/outbox',
  '/plans',
  '/invite',
  '/profile',
  '/admin/feedback',
  '/admin/learning-topics',
  '/admin',
] as const

export type HelpKey = (typeof HELP_ROUTES)[number]

/** The route key whose help covers `pathname`, or null when the page has no entry. */
export function resolveHelpKey(pathname: string): HelpKey | null {
  for (const pattern of HELP_ROUTES) {
    if (matchPath(pattern, pathname)) return pattern
  }
  return null
}

/**
 * Pages where a brand-new user is auto-shown the help once. Kept to the three that make up the
 * core loop — auto-opening on twenty pages in one session is nagging, not onboarding.
 */
export const AUTO_OPEN_KEYS: HelpKey[] = ['/apiaries', '/beehives/:id', '/inspections/new']
