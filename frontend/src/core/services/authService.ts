import axios from 'axios'

const API_BASE = import.meta.env.VITE_API_URL ?? '/api'

// Bare axios instance for the auth endpoints. These must NOT pass through apiClient's
// refresh-on-401 interceptor (that would recurse), so they get their own client.
const authApi = axios.create({
  baseURL: API_BASE,
  headers: { 'Content-Type': 'application/json' },
  timeout: 10_000,
})

export interface AuthUser {
  email: string
  firstName: string
  lastName: string
  role: string
  organizationId?: number | null
  organizationName?: string | null
  assignedBeehiveIds: number[]
}

export interface LoginResponse extends AuthUser {
  token: string
  refreshToken: string
  accessTokenExpiresAt: string
}

export interface RegisterPayload {
  firstName: string
  lastName: string
  email: string
  password: string
  organizationName: string
  organizationDescription?: string
}

const TOKEN_KEY = 'beehive_token'
const REFRESH_KEY = 'beehive_refresh_token'
const USER_KEY = 'beehive_user'

function persistSession(data: LoginResponse): void {
  localStorage.setItem(TOKEN_KEY, data.token)
  localStorage.setItem(REFRESH_KEY, data.refreshToken)
  localStorage.setItem(USER_KEY, JSON.stringify({
    email: data.email,
    firstName: data.firstName,
    lastName: data.lastName,
    role: data.role,
    organizationId: data.organizationId,
    organizationName: data.organizationName,
    assignedBeehiveIds: data.assignedBeehiveIds ?? [],
  }))
}

export const authService = {
  async login(email: string, password: string): Promise<LoginResponse> {
    const { data } = await authApi.post<LoginResponse>('/auth/login', { email, password })
    persistSession(data)
    return data
  },

  /**
   * Creates a new account + organisation and signs the user in immediately
   * (the API returns the same token payload as login).
   */
  async register(payload: RegisterPayload): Promise<LoginResponse> {
    const { data } = await authApi.post<LoginResponse>('/auth/register', payload)
    persistSession(data)
    return data
  },

  /**
   * Exchanges the stored refresh token for a new access token (rotation).
   * Returns the new access token, or null when no refresh token is available.
   * Used by apiClient's 401 interceptor.
   */
  async refresh(): Promise<string | null> {
    const refreshToken = this.getRefreshToken()
    if (!refreshToken) return null
    const { data } = await authApi.post<LoginResponse>('/auth/refresh', { refreshToken })
    persistSession(data)
    return data.token
  },

  logout(): void {
    const refreshToken = this.getRefreshToken()
    // Best-effort server-side revocation — don't block the UI on it.
    if (refreshToken) {
      void authApi.post('/auth/logout', { refreshToken }).catch(() => { /* ignore */ })
    }
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_KEY)
    localStorage.removeItem(USER_KEY)
  },

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY)
  },

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY)
  },

  getUser(): AuthUser | null {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? (JSON.parse(raw) as AuthUser) : null
  },

  isAuthenticated(): boolean {
    return !!localStorage.getItem(TOKEN_KEY)
  },

  updateStoredUser(partial: Pick<AuthUser, 'firstName' | 'lastName' | 'email'>): void {
    const current = this.getUser()
    if (!current) return
    const updated = { ...current, ...partial }
    localStorage.setItem(USER_KEY, JSON.stringify(updated))
  },
}
