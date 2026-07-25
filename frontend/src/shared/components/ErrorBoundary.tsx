import { Component, type ErrorInfo, type ReactNode } from 'react'
import { AlertTriangle, RotateCw } from 'lucide-react'

interface Props {
  children: ReactNode
}

interface State {
  error: Error | null
}

/**
 * Last line of defence for render-time crashes. Without it, one thrown error in any component
 * unmounts the whole tree and the user is left staring at a blank white page with no way back.
 *
 * Must be a class — React has no hook equivalent of componentDidCatch.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // No error-reporting service wired up yet — the console is all we have, and it is still far
    // better than a silent white screen when someone reports "the app broke".
    console.error('Neuhvaćena greška u komponenti:', error, info.componentStack)
  }

  private handleReload = () => {
    // A full reload, not setState: the tree that threw may hold corrupt state, and re-rendering it
    // usually just throws again.
    window.location.reload()
  }

  private handleGoHome = () => {
    window.location.href = '/'
  }

  render() {
    if (!this.state.error) return this.props.children

    return (
      <div className="min-h-screen flex items-center justify-center bg-honey-50 dark:bg-slate-950 px-6 py-12">
        <div className="w-full max-w-md text-center">
          <div className="text-5xl mb-2">🐝</div>
          <h1 className="font-display text-3xl font-bold text-honey-800 dark:text-honey-300 mb-8">
            Melarium
          </h1>

          <div className="bg-white/90 dark:bg-slate-900/90 backdrop-blur rounded-3xl shadow-xl border border-honey-100 dark:border-slate-800 px-8 py-10">
            <div className="w-14 h-14 mx-auto rounded-full bg-red-100 dark:bg-red-500/15 flex items-center justify-center mb-4">
              <AlertTriangle className="w-7 h-7 text-red-500" />
            </div>

            <h2 className="font-display text-xl font-bold text-gray-900 dark:text-slate-100">
              Nešto je pošlo po zlu
            </h2>
            <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">
              Dogodila se neočekivana greška. Vaši podaci su sigurni — osvježite stranicu i pokušajte
              ponovo.
            </p>

            {/* Only in dev: the message helps while building, but it can leak internals to users. */}
            {import.meta.env.DEV && (
              <pre className="mt-4 text-left text-xs bg-gray-100 dark:bg-slate-800 rounded-xl p-3 overflow-x-auto text-red-700 dark:text-red-300">
                {this.state.error.message}
              </pre>
            )}

            <div className="mt-6 flex flex-col sm:flex-row gap-3">
              <button onClick={this.handleReload} className="btn-primary flex-1 justify-center">
                <RotateCw className="w-4 h-4" />
                Osvježi
              </button>
              <button onClick={this.handleGoHome} className="btn-secondary flex-1 justify-center">
                Početna
              </button>
            </div>
          </div>
        </div>
      </div>
    )
  }
}
