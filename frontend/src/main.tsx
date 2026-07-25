import React from 'react'
import ReactDOM from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import App from './App.tsx'
import { ErrorBoundary } from './shared/components/ErrorBoundary'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 2,   // 2 minutes
      retry: 1,
      refetchOnWindowFocus: false,
      // React Query's default ('online') *pauses* queries while the browser is offline: the query
      // stays `pending` with no data and no error, so every page fell through to its empty state
      // and told a beekeeper in the field "nema vrcanja" instead of "no connection".
      //
      // 'always' lets the request run: the service worker answers GETs from its NetworkFirst cache
      // when it can, and a genuine failure surfaces as an error the page can show. Reconnects are
      // still handled — refetchOnReconnect stays on by default.
      networkMode: 'always',
    },
    mutations: {
      // Writes must fail loudly rather than queue invisibly — the offline outbox (SPEC-07) is the
      // deliberate exception and handles its own retry.
      networkMode: 'always',
    },
  },
})

// Outbox unit-test harness (SPEC-07) — dev builds only; run `await __outboxSelfTest()` in the console.
if (import.meta.env.DEV) {
  void import('./core/offline/outboxSelfTest').then(m => {
    ;(window as unknown as Record<string, unknown>).__outboxSelfTest = m.runOutboxSelfTest
  })
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    {/* Outermost so it also catches failures in the providers and the router itself. */}
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <App />
      </QueryClientProvider>
    </ErrorBoundary>
  </React.StrictMode>,
)
