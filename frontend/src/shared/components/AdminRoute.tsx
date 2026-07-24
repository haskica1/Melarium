import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../../core/context/AuthContext'

export default function AdminRoute() {
  const { user, isAuthenticated } = useAuth()
  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (user?.role !== 'SystemAdmin') return <Navigate to="/apiaries" replace />
  return <Outlet />
}
