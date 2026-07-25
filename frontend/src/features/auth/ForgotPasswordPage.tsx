import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { ArrowRight, Mail, MailCheck } from 'lucide-react'
import { authService } from '../../core/services/authService'
import { AuthCard, AuthSubmitButton, authInputClass } from './AuthCard'
import { getServerError } from './authErrors'

interface ForgotForm {
  email: string
}

export default function ForgotPasswordPage() {
  const [sent, setSent] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<ForgotForm>()

  async function onSubmit(data: ForgotForm) {
    setServerError(null)
    try {
      await authService.forgotPassword(data.email)
      setSent(true)
    } catch (err) {
      // Only real failures (rate limit, server down) surface — a missing account is a 204.
      setServerError(getServerError(err, 'Slanje nije uspjelo. Pokušajte ponovo.'))
    }
  }

  // Deliberately identical whether or not the address is registered: revealing the difference
  // would turn this form into a way to test who has an account.
  if (sent) {
    return (
      <AuthCard title="Provjerite e-poštu">
        <div className="flex flex-col items-center text-center gap-4">
          <div className="w-14 h-14 rounded-full bg-green-100 dark:bg-green-500/15 flex items-center justify-center">
            <MailCheck className="w-7 h-7 text-green-600 dark:text-green-400" />
          </div>
          <p className="text-sm text-gray-600 dark:text-slate-400">
            Ako postoji račun povezan s adresom{' '}
            <strong className="text-gray-800 dark:text-slate-200">{getValues('email')}</strong>,
            poslali smo link za postavljanje nove lozinke.
          </p>
          <p className="text-xs text-gray-500 dark:text-slate-500">
            Link vrijedi 2 sata i može se iskoristiti samo jednom. Ako poruka ne stigne za nekoliko
            minuta, provjerite spam folder.
          </p>
        </div>
      </AuthCard>
    )
  }

  return (
    <AuthCard
      title="Zaboravljena lozinka"
      subtitle="Unesite vašu e-poštu i poslat ćemo vam link za postavljanje nove lozinke."
    >
      {serverError && (
        <div
          role="alert"
          className="mb-6 flex items-start gap-3 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm animate-slide-up"
        >
          <span className="mt-0.5">⚠️</span>
          <span>{serverError}</span>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
        <div>
          <label htmlFor="email" className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
            E-pošta
          </label>
          <div className="relative">
            <Mail className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-slate-500 pointer-events-none" />
            <input
              id="email"
              type="email"
              autoComplete="email"
              autoFocus
              placeholder="you@example.com"
              className={authInputClass(!!errors.email)}
              {...register('email', {
                required: 'E-pošta je obavezna',
                pattern: { value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/, message: 'Unesite valjanu e-poštu' },
              })}
            />
          </div>
          {errors.email && (
            <p className="mt-1.5 text-xs text-red-600 dark:text-red-400">{errors.email.message}</p>
          )}
        </div>

        <AuthSubmitButton
          isSubmitting={isSubmitting}
          label="Pošalji link"
          busyLabel="Slanje…"
          icon={<ArrowRight className="w-4 h-4 group-hover:translate-x-0.5 transition-transform" />}
        />
      </form>
    </AuthCard>
  )
}
