import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { AlertTriangle, CheckCircle2, Loader2 } from 'lucide-react'
import { authService } from '../../core/services/authService'
import { AuthCard } from './AuthCard'
import { getServerError } from './authErrors'

type Status = 'verifying' | 'success' | 'error'

export default function VerifyEmailPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''

  const [status, setStatus] = useState<Status>(token ? 'verifying' : 'error')
  const [message, setMessage] = useState<string | null>(
    token ? null : 'Ovaj link za potvrdu nije potpun. Kopirajte cijeli link iz e-pošte.',
  )

  // StrictMode mounts effects twice in development; the second call would redeem an
  // already-used token and flip a successful verification into an error.
  const attempted = useRef(false)

  useEffect(() => {
    if (!token || attempted.current) return
    attempted.current = true

    authService
      .verifyEmail(token)
      .then(() => setStatus('success'))
      .catch(err => {
        setMessage(getServerError(err, 'Potvrda e-pošte nije uspjela.'))
        setStatus('error')
      })
  }, [token])

  if (status === 'verifying') {
    return (
      <AuthCard title="Potvrda e-pošte" showBackToLogin={false}>
        <div className="flex flex-col items-center text-center gap-4 py-4">
          <Loader2 className="w-8 h-8 text-honey-500 animate-spin" />
          <p className="text-sm text-gray-500 dark:text-slate-400">Potvrđujemo vašu adresu…</p>
        </div>
      </AuthCard>
    )
  }

  if (status === 'success') {
    return (
      <AuthCard title="E-pošta potvrđena">
        <div className="flex flex-col items-center text-center gap-4">
          <div className="w-14 h-14 rounded-full bg-green-100 dark:bg-green-500/15 flex items-center justify-center">
            <CheckCircle2 className="w-7 h-7 text-green-600 dark:text-green-400" />
          </div>
          <p className="text-sm text-gray-600 dark:text-slate-400">
            Hvala — vaša adresa je potvrđena. Sada možete primati obavijesti i vratiti pristup računu
            ako zaboravite lozinku.
          </p>
        </div>
      </AuthCard>
    )
  }

  return (
    <AuthCard title="Potvrda nije uspjela">
      <div className="flex flex-col items-center text-center gap-4">
        <div className="w-14 h-14 rounded-full bg-red-100 dark:bg-red-500/15 flex items-center justify-center">
          <AlertTriangle className="w-7 h-7 text-red-500" />
        </div>
        <p className="text-sm text-gray-600 dark:text-slate-400">{message}</p>
        <p className="text-xs text-gray-500 dark:text-slate-500">
          Prijavite se i zatražite novi link sa vašeg profila.{' '}
          <Link to="/profile" className="underline hover:no-underline">
            Otvori profil
          </Link>
        </p>
      </div>
    </AuthCard>
  )
}
