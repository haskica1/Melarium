import axios from 'axios'

/**
 * Auth pages talk to the API through `authApi` — a bare axios instance with no interceptors
 * (apiClient's refresh-on-401 would recurse on the auth endpoints themselves). So they get the
 * raw AxiosError and have to unwrap the backend's shapes here:
 *
 *   GlobalExceptionMiddleware → { title, errors: { detail: [msg] } }
 *   FluentValidation           → { Field: [msg] }
 *
 * Without this the user sees axios's own "Request failed with status code 401".
 */
export function getServerError(err: unknown, fallback: string): string {
  if (axios.isAxiosError(err)) {
    if (err.response?.status === 429)
      return 'Previše pokušaja. Pričekajte minutu pa pokušajte ponovo.'

    if (!err.response)
      return 'Nema veze sa serverom. Provjerite internet konekciju.'

    const data = err.response.data as
      | { title?: string; errors?: Record<string, string[]> }
      | undefined

    const detail = data?.errors?.detail?.[0]
    if (detail) return detail

    const firstNested = data?.errors && firstStringInArrayValue(data.errors)
    if (firstNested) return firstNested

    // FluentValidation's ToDictionary() lands at the top level, not under `errors`.
    const firstTopLevel = firstStringInArrayValue(data)
    if (firstTopLevel) return firstTopLevel

    if (data?.title) return data.title
  }
  return fallback
}

/** First message out of any `{ key: ["msg", …] }` entry — the shape both error formats use. */
function firstStringInArrayValue(source: unknown): string | undefined {
  if (typeof source !== 'object' || source === null) return undefined
  for (const value of Object.values(source as Record<string, unknown>)) {
    if (Array.isArray(value) && typeof value[0] === 'string') return value[0]
  }
  return undefined
}
