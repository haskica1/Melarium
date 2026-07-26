import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { recordEntry } from '../utils/historyStack'

/**
 * Feeds every navigation into `historyStack`. Renders nothing.
 *
 * Mounted directly inside the router rather than in `Layout` so public routes (login, the QR scan
 * landing page) are recorded too — a gap in the mirror makes the entry before it unreadable.
 */
export default function HistoryTracker() {
  const location = useLocation()

  useEffect(() => {
    recordEntry(location.pathname)
  }, [location])

  return null
}
