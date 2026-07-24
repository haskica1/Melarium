import { Navigate } from 'react-router-dom'
import { useAuth } from '../../core/context/AuthContext'

export default function SmartRedirect() {
  const { user } = useAuth()
  return <Navigate to={user?.role === 'SystemAdmin' ? '/admin' : '/apiaries'} replace />
}

