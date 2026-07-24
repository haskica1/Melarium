import React from 'react'
import ReactDOM from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import App from './App.tsx'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 2,   // 2 minutes
      retry: 1,
      refetchOnWindowFocus: false,
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
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </React.StrictMode>,
)
