import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { ArrowRight, CheckCircle2, Eye, EyeOff, Lock } from 'lucide-react'
import { authService } from '../../core/services/authService'
import { AuthCard, AuthSubmitButton, authInputClass } from './AuthCard'
import { getServerError } from './authErrors'

interface ResetForm {
  newPassword: string
  confirmPassword: string
}

/** Must match the backend policy (PasswordRules.MinLength). */
const MIN_PASSWORD_LENGTH = 8

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const navigate = useNavigate()

  const [showPassword, setShowPassword] = useState(false)
  const [done, setDone] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    watch,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ResetForm>()

  const newPassword = watch('newPassword')

  async function onSubmit(data: ResetForm) {
    setServerError(null)

    if (data.newPassword !== data.confirmPassword) {
      setError('confirmPassword', { message: 'Lozinke se ne podudaraju.' })
      return
    }

    try {
      await authService.resetPassword(token, data.newPassword)
      // The reset revokes every session server-side, so any stale local token is already dead.
      authService.logout()
      setDone(true)
    } catch (err) {
      setServerError(getServerError(err, 'Promjena lozinke nije uspjela. Pokušajte ponovo.'))
    }
  }

  // A link without a token is a broken/truncated email — say so instead of showing a form that
  // is guaranteed to fail on submit.
  if (!token) {
    return (
      <AuthCard title="Link nije potpun">
        <p className="text-sm text-gray-600 dark:text-slate-400">
          Ovaj link za promjenu lozinke nije potpun. Kopirajte cijeli link iz e-pošte ili zatražite novi.
        </p>
        <Link
          to="/forgot-password"
          className="mt-5 inline-flex items-center gap-1.5 text-sm font-semibold text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300"
        >
          Zatraži novi link <ArrowRight className="w-4 h-4" />
        </Link>
      </AuthCard>
    )
  }

  if (done) {
    return (
      <AuthCard title="Lozinka je promijenjena" showBackToLogin={false}>
        <div className="flex flex-col items-center text-center gap-4">
          <div className="w-14 h-14 rounded-full bg-green-100 dark:bg-green-500/15 flex items-center justify-center">
            <CheckCircle2 className="w-7 h-7 text-green-600 dark:text-green-400" />
          </div>
          <p className="text-sm text-gray-600 dark:text-slate-400">
            Iz sigurnosnih razloga odjavljeni ste sa svih uređaja. Prijavite se novom lozinkom.
          </p>
          <button onClick={() => navigate('/login', { replace: true })} className="btn-primary w-full justify-center">
            Prijavi se
          </button>
        </div>
      </AuthCard>
    )
  }

  return (
    <AuthCard title="Nova lozinka" subtitle="Postavite novu lozinku za vaš račun.">
      {serverError && (
        <div
          role="alert"
          className="mb-6 flex items-start gap-3 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm animate-slide-up"
        >
          <span className="mt-0.5">⚠️</span>
          <div>
            <p>{serverError}</p>
            <Link to="/forgot-password" className="underline hover:no-underline font-medium">
              Zatraži novi link
            </Link>
          </div>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
        <div>
          <label htmlFor="newPassword" className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
            Nova lozinka
          </label>
          <div className="relative">
            <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-slate-500 pointer-events-none" />
            <input
              id="newPassword"
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              autoFocus
              placeholder="••••••••"
              className={authInputClass(!!errors.newPassword)}
              {...register('newPassword', {
                required: 'Lozinka je obavezna',
                minLength: {
                  value: MIN_PASSWORD_LENGTH,
                  message: `Lozinka mora imati najmanje ${MIN_PASSWORD_LENGTH} znakova`,
                },
              })}
            />
            <button
              type="button"
              onClick={() => setShowPassword(v => !v)}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 dark:text-slate-500 hover:text-gray-600 dark:hover:text-slate-300 transition-colors"
              tabIndex={-1}
              aria-label={showPassword ? 'Sakrij lozinku' : 'Prikaži lozinku'}
            >
              {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
            </button>
          </div>
          {errors.newPassword && (
            <p className="mt-1.5 text-xs text-red-600 dark:text-red-400">{errors.newPassword.message}</p>
          )}
        </div>

        <div>
          <label htmlFor="confirmPassword" className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
            Potvrdite lozinku
          </label>
          <div className="relative">
            <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-slate-500 pointer-events-none" />
            <input
              id="confirmPassword"
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              placeholder="••••••••"
              className={authInputClass(!!errors.confirmPassword)}
              {...register('confirmPassword', {
                required: 'Potvrdite lozinku',
                validate: value => value === newPassword || 'Lozinke se ne podudaraju',
              })}
            />
          </div>
          {errors.confirmPassword && (
            <p className="mt-1.5 text-xs text-red-600 dark:text-red-400">{errors.confirmPassword.message}</p>
          )}
        </div>

        <AuthSubmitButton
          isSubmitting={isSubmitting}
          label="Postavi lozinku"
          busyLabel="Spremanje…"
          icon={<ArrowRight className="w-4 h-4 group-hover:translate-x-0.5 transition-transform" />}
        />
      </form>
    </AuthCard>
  )
}
