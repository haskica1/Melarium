import { Navigate } from 'react-router-dom'
import { useAuth } from '../../core/context/AuthContext'
import DashboardPage from '../../features/dashboard/DashboardPage'

/**
 * The index route. SystemAdmin has no organization and keeps the platform console; everyone else
 * gets the dashboard as their start page (SPEC-29) — rendered here rather than redirected to, so
 * `/` stays the address of home.
 */
export default function SmartRedirect() {
  const { user } = useAuth()
  if (user?.role === 'SystemAdmin') return <Navigate to="/admin" replace />
  return <DashboardPage />
}
