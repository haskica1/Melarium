import { createContext, useContext } from 'react'

interface HelpTrigger {
  /** Opens the current page's help panel. No-op when the page has no entry. */
  openHelp: () => void
  /** False when the current route has no help entry — callers should not offer the affordance. */
  hasHelp: boolean
}

const HelpContext = createContext<HelpTrigger>({ openHelp: () => {}, hasHelp: false })

/**
 * Lets a page inside `<Outlet />` open the help panel that `Layout` owns — the panel is mounted once,
 * so pages need a handle to it rather than their own copy of the state.
 */
export const HelpProvider = HelpContext.Provider

export function useHelpTrigger(): HelpTrigger {
  return useContext(HelpContext)
}
