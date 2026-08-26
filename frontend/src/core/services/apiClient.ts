import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { authService } from './authService'

/**
 * Pre-configured Axios instance for Melarium API calls.
 * The base URL is handled by the Vite dev-proxy during development,
 * and replaced with the real API URL in production via the VITE_API_URL env var.
 */
const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 10_000,
})

// Attach JWT access token from localStorage on every request
apiClient.interceptors.request.use((config) => {
  const token = authService.getToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// SPEC-09: surface plan-limit responses (402 + code "plan-limit") as a global upsell event,
// then let the error continue through the normal rejection path untouched. Registered BEFORE
// the 401 interceptor so it sees the raw response before it is reduced to an Error message.
// (This does not alter the 401 handling below — see ignore.md.)
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<{ code?: string; errors?: { detail?: string[] } }>) => {
    if (error.response?.status === 402 && error.response.data?.code === 'plan-limit') {
      const detail =
        error.response.data?.errors?.detail?.[0] ??
        'Ova funkcija zahtijeva nadogradnju paketa.'
      window.dispatchEvent(new CustomEvent('plan-limit', { detail }))
      // Reject with the Bosnian message so any inline form error shows it too (instead of the
      // generic English "Payment Required" title the downstream interceptor would derive).
      // Flagged, because `UpsellModal` already explains this one: a caller that reports its own
      // errors needs a way to stay quiet here and not stack a toast on top of the modal.
      return Promise.reject(Object.assign(new Error(detail), { planLimit: true as const }))
    }
    return Promise.reject(error)
  },
)

// A client-side timeout carries no response, so the interceptor below falls through to axios's own
// "timeout of 120000ms exceeded" — English, and meaningless to a beekeeper who was told the voice
// note failed. The distinction matters: this one means the work may well have succeeded on the
// server and the browser stopped waiting, which is different advice than "it broke". Registered
// before the 401 interceptor, which then passes the message through untouched.
// Deliberately narrow — only timeouts. A response-less error is also how an offline submit reaches
// the outbox (SPEC-07), and that path is left exactly as it is.
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT') {
      return Promise.reject(new Error(
        'Zahtjev je trajao predugo pa je prekinut. Provjerite signal i pokušajte ponovo.',
      ))
    }
    return Promise.reject(error)
  },
)

// Single-flight refresh: concurrent 401s share one /auth/refresh call so the
// rotating refresh token is only spent once.
let refreshPromise: Promise<string | null> | null = null
function refreshOnce(): Promise<string | null> {
  refreshPromise ??= authService.refresh().finally(() => { refreshPromise = null })
  return refreshPromise
}

function hardLogout(): void {
  authService.logout()
  if (window.location.pathname !== '/login') {
    window.location.href = '/login'
  }
}

/**
 * Controllers reject invalid input with `BadRequest(validation.ToDictionary())`, whose body is a
 * flat `{ Field: ["message", …] }` map — not the Problem-Details shape the middleware emits. Without
 * this, every validation failure surfaced as "Request failed with status code 400".
 */
function firstValidationMessage(data: unknown): string | undefined {
  if (typeof data !== 'object' || data === null) return undefined
  for (const value of Object.values(data as Record<string, unknown>)) {
    if (Array.isArray(value) && typeof value[0] === 'string') return value[0]
  }
  return undefined
}

// On 401: try to rotate the refresh token once and replay the request; otherwise sign out.
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<{ title?: string; message?: string; errors?: { detail?: string[] } }>) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined
    const status = error.response?.status

    if (status === 401 && original && !original._retry && authService.getRefreshToken()) {
      original._retry = true
      try {
        const newToken = await refreshOnce()
        if (newToken) {
          original.headers.Authorization = `Bearer ${newToken}`
          return apiClient(original)
        }
      } catch {
        // refresh failed — fall through to logout
      }
      hardLogout()
      return Promise.reject(new Error('Your session has expired. Please sign in again.'))
    }

    if (status === 401) {
      hardLogout()
    }

    // GlobalExceptionMiddleware puts the human-readable (Bosnian) reason in errors.detail and only a
    // generic English category in title — prefer the former, or callers surface "Business Rule
    // Violation" to the user. This Error replaces the AxiosError, so `err.response` is gone
    // downstream; read `err.message`.
    const message =
      error.response?.data?.errors?.detail?.[0] ??
      firstValidationMessage(error.response?.data) ??
      error.response?.data?.title ??
      error.response?.data?.message ??
      error.message ??
      'An unexpected error occurred'

    return Promise.reject(new Error(message))
  },
)

/**
 * The message every interceptor above ends up rejecting with. Nothing in this file *displays* it —
 * a caller that swallows the rejection shows the user nothing at all, which is how the AI assistant
 * managed to fail in complete silence.
 */
export function errorMessage(error: unknown): string {
  return error instanceof Error && error.message
    ? error.message
    : 'Došlo je do greške. Pokušajte ponovo.'
}

/** True for a 402 plan limit, which `UpsellModal` has already shown — do not report it twice. */
export function isPlanLimit(error: unknown): boolean {
  return typeof error === 'object' && error !== null && (error as { planLimit?: boolean }).planLimit === true
}

export default apiClient
