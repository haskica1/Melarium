import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../core/context/AuthContext'

export default function ProtectedRoute() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()
  if (isAuthenticated) return <Outlet />
  const returnUrl = encodeURIComponent(location.pathname + location.search)
  return <Navigate to={`/login?returnUrl=${returnUrl}`} replace />
}
