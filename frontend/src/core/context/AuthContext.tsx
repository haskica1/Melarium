import { createContext, useCallback, useContext, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { authService, type AuthUser, type LoginResponse, type RegisterPayload } from '../services/authService'

interface AuthContextValue {
  user: AuthUser | null
  isAuthenticated: boolean
  /** `identifier` is an email address or a phone number — either signs the user in. */
  login: (identifier: string, password: string) => Promise<LoginResponse>
  register: (payload: RegisterPayload) => Promise<LoginResponse>
  logout: () => void
  updateUser: (partial: Partial<Pick<AuthUser, 'firstName' | 'lastName' | 'email' | 'organizationName'>>) => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<AuthUser | null>(() => authService.getUser())

  const login = useCallback(async (identifier: string, password: string): Promise<LoginResponse> => {
    const response = await authService.login(identifier, password)
    // Drop any cached data from a previous session so the new user never sees stale data.
    queryClient.clear()
    setUser({
      email: response.email,
      firstName: response.firstName,
      lastName: response.lastName,
      role: response.role,
      organizationId: response.organizationId,
      organizationName: response.organizationName,
      assignedBeehiveIds: response.assignedBeehiveIds ?? [],
    })
    return response
  }, [queryClient])

  const register = useCallback(async (payload: RegisterPayload): Promise<LoginResponse> => {
    const response = await authService.register(payload)
    // A fresh account must start with an empty cache (no carry-over from a prior session).
    queryClient.clear()
    setUser({
      email: response.email,
      firstName: response.firstName,
      lastName: response.lastName,
      role: response.role,
      organizationId: response.organizationId,
      organizationName: response.organizationName,
      assignedBeehiveIds: response.assignedBeehiveIds ?? [],
    })
    return response
  }, [queryClient])

  const logout = useCallback(() => {
    authService.logout()
    queryClient.clear()
    setUser(null)
  }, [queryClient])

  const updateUser = useCallback((partial: Partial<Pick<AuthUser, 'firstName' | 'lastName' | 'email' | 'organizationName'>>) => {
    authService.updateStoredUser(partial)
    setUser(prev => prev ? { ...prev, ...partial } : prev)
  }, [])

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: !!user, login, register, logout, updateUser }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
