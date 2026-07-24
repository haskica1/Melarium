import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../../core/context/AuthContext'

interface RoleRouteProps {
  allowedRoles: string[]
  /** Where to redirect if the role check fails. Defaults to /apiaries */
  redirectTo?: string
}

export default function RoleRoute({ allowedRoles, redirectTo = '/apiaries' }: RoleRouteProps) {
  const { user } = useAuth()
  if (!user || !allowedRoles.includes(user.role)) {
    return <Navigate to={redirectTo} replace />
  }
  return <Outlet />
}
