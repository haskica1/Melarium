/**
 * Ways for a page to ask the layout to open something it owns — the QR scanner and the assistant
 * sheet live in `Layout`, and the dashboard's quick actions (SPEC-29) open the same instances
 * instead of mounting second copies.
 */
const OPEN_QR_SCANNER = 'melarium:open-qr-scanner'
const OPEN_ASSISTANT = 'melarium:open-assistant'

export const openQrScanner = () => window.dispatchEvent(new Event(OPEN_QR_SCANNER))
export const openAssistant = () => window.dispatchEvent(new Event(OPEN_ASSISTANT))

/** Subscribes the layout; returns the cleanup for `useEffect`. */
export function onLayoutRequests(handlers: { qrScanner: () => void; assistant: () => void }): () => void {
  window.addEventListener(OPEN_QR_SCANNER, handlers.qrScanner)
  window.addEventListener(OPEN_ASSISTANT, handlers.assistant)
  return () => {
    window.removeEventListener(OPEN_QR_SCANNER, handlers.qrScanner)
    window.removeEventListener(OPEN_ASSISTANT, handlers.assistant)
  }
}
