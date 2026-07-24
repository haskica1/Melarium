import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { ArrowRight, Eye, EyeOff, Loader2, Lock, Mail, Moon, Sun } from 'lucide-react'
import clsx from 'clsx'
import { useAuth } from '../../core/context/AuthContext'
import { useTheme } from '../../core/hooks/useTheme'
import type { LoginResponse } from '../../core/services/authService'

interface LoginForm {
  email: string
  password: string
}

// ── Left-panel feature highlights ───────────────────────────────────────────────

const FEATURES = [
  { icon: '🏡', title: 'Pčelinjaci',    desc: 'Organizirajte svaki pčelinjak na jednom mjestu' },
  { icon: '🍯', title: 'Pregledi',    desc: 'Pratite zdravlje kolonija tokom vremena' },
  { icon: '🌿', title: 'Planovi ishrane', desc: 'Kreirajte planove ishrane koji se odvijaju sami' },
]


export default function LoginPage() {
  const { login } = useAuth()
  const { isDark, toggleTheme } = useTheme()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const returnUrl = searchParams.get('returnUrl')
  const [showPassword, setShowPassword] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>()

  async function onSubmit(data: LoginForm) {
    setServerError(null)
    try {
      const response = await login(data.email, data.password) as LoginResponse
      const destination = returnUrl ?? (response.role === 'SystemAdmin' ? '/admin' : '/apiaries')
      navigate(destination, { replace: true })
    } catch (err) {
      setServerError(err instanceof Error ? err.message : 'Prijava neuspješna. Pokušajte ponovo.')
    }
  }

  return (
    <div className="relative min-h-screen flex">

      {/* Theme toggle */}
      <button
        onClick={toggleTheme}
        className="absolute top-4 right-4 z-30 w-9 h-9 rounded-full flex items-center justify-center bg-white/80 dark:bg-slate-800/80 backdrop-blur border border-honey-200 dark:border-slate-700 text-gray-600 dark:text-slate-300 hover:text-honey-600 dark:hover:text-honey-300 transition-colors shadow-sm"
        aria-label={isDark ? 'Prebaci na svjetlu temu' : 'Prebaci na tamnu temu'}
        title={isDark ? 'Svjetla tema' : 'Tamna tema'}
      >
        {isDark ? <Sun className="w-[18px] h-[18px]" /> : <Moon className="w-[18px] h-[18px]" />}
      </button>

      {/* ── Left panel — decorative ─────────────────────────────────────────── */}
      <div className="hidden lg:flex lg:w-1/2 relative overflow-hidden bg-gradient-to-br from-honey-500 via-honey-600 to-amber-800 flex-col items-center justify-center p-12">
        {/* Honeycomb pattern */}
        <svg className="absolute inset-0 w-full h-full opacity-10" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <pattern id="honeycomb" x="0" y="0" width="56" height="100" patternUnits="userSpaceOnUse">
              <path d="M28 66 L0 50 L0 16 L28 0 L56 16 L56 50 Z" fill="none" stroke="white" strokeWidth="1.5" />
              <path d="M28 166 L0 150 L0 116 L28 100 L56 116 L56 150 Z" fill="none" stroke="white" strokeWidth="1.5" />
              <path d="M56 116 L84 100 L84 66 L56 50 L28 66 L28 100 Z" fill="none" stroke="white" strokeWidth="1.5" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#honeycomb)" />
        </svg>

        {/* Floating glows */}
        <div className="absolute -top-20 -left-20 w-80 h-80 rounded-full bg-amber-300/30 blur-3xl animate-float" />
        <div className="absolute -bottom-24 -right-16 w-96 h-96 rounded-full bg-honey-300/20 blur-3xl animate-float" style={{ animationDelay: '2.5s' }} />

        {/* Content */}
        <div className="relative z-10 max-w-sm w-full text-center">
          <div className="text-7xl mb-5 drop-shadow-lg animate-float">🐝</div>
          <h1 className="font-display text-5xl font-bold text-white tracking-tight">Melarium</h1>
          <p className="mt-4 text-honey-50/90 text-lg leading-relaxed">
            Upravljajte vašim pčelinjacima, pratite vaše kolonije i čuvajte vaše pčele zdravim — sve na jednom mjestu.
          </p>

          <div className="mt-10 space-y-3 text-left stagger">
            {FEATURES.map(f => (
              <div key={f.title} className="flex items-center gap-3 bg-white/10 backdrop-blur-md border border-white/15 rounded-2xl px-4 py-3">
                <div className="w-10 h-10 rounded-xl bg-white/15 flex items-center justify-center text-xl shrink-0">
                  {f.icon}
                </div>
                <div className="min-w-0">
                  <p className="text-white font-semibold text-sm leading-tight">{f.title}</p>
                  <p className="text-honey-50/80 text-xs leading-tight mt-0.5">{f.desc}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* ── Right panel — form ──────────────────────────────────────────────── */}
      <div className="flex-1 flex items-center justify-center bg-honey-50 dark:bg-slate-950 px-6 py-12">
        <div className="w-full max-w-md animate-fade-in">

          {/* Mobile logo */}
          <div className="lg:hidden text-center mb-8">
            <div className="text-5xl mb-2 animate-float">🐝</div>
            <h1 className="font-display text-3xl font-bold text-honey-800 dark:text-honey-300">Melarium</h1>
          </div>

          <div className="bg-white/90 dark:bg-slate-900/90 backdrop-blur rounded-3xl shadow-xl border border-honey-100 dark:border-slate-800 px-8 py-10">
            <div className="mb-7">
              <h2 className="font-display text-2xl font-bold text-gray-900 dark:text-slate-100">Dobrodošli nazad</h2>
              <p className="text-gray-500 dark:text-slate-400 mt-1 text-sm">Prijavite se u vaš pčelarski radni prostor</p>
            </div>

            {/* Server error */}
            {serverError && (
              <div className="mb-6 flex items-start gap-3 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm animate-slide-up">
                <span className="mt-0.5">⚠️</span>
                <span>{serverError}</span>
              </div>
            )}

            <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
              {/* Email */}
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
                    placeholder="you@example.com"
                    className={clsx(
                      'w-full pl-11 pr-4 py-3 rounded-xl border text-sm transition-all duration-200 outline-none',
                      'bg-gray-50 dark:bg-slate-800 focus:bg-white dark:focus:bg-slate-800 dark:text-slate-100',
                      errors.email
                        ? 'border-red-400 focus:ring-2 focus:ring-red-200 dark:focus:ring-red-500/30'
                        : 'border-gray-200 dark:border-slate-700 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 dark:focus:ring-honey-500/20',
                    )}
                    {...register('email', {
                      required: 'E-pošta je obavezna',
                      pattern: { value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/, message: 'Unesite valjanu e-poštu' },
                    })}
                  />
                </div>
                {errors.email && <p className="mt-1.5 text-xs text-red-600 dark:text-red-400">{errors.email.message}</p>}
              </div>

              {/* Password */}
              <div>
                <label htmlFor="password" className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                  Lozinka
                </label>
                <div className="relative">
                  <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-slate-500 pointer-events-none" />
                  <input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="current-password"
                    placeholder="••••••••"
                    className={clsx(
                      'w-full pl-11 pr-11 py-3 rounded-xl border text-sm transition-all duration-200 outline-none',
                      'bg-gray-50 dark:bg-slate-800 focus:bg-white dark:focus:bg-slate-800 dark:text-slate-100',
                      errors.password
                        ? 'border-red-400 focus:ring-2 focus:ring-red-200 dark:focus:ring-red-500/30'
                        : 'border-gray-200 dark:border-slate-700 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 dark:focus:ring-honey-500/20',
                    )}
                    {...register('password', { required: 'Lozinka je obavezna' })}
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
                {errors.password && <p className="mt-1.5 text-xs text-red-600 dark:text-red-400">{errors.password.message}</p>}
              </div>

              {/* Submit */}
              <button
                type="submit"
                disabled={isSubmitting}
                className="group relative w-full overflow-hidden flex items-center justify-center gap-2
                  bg-gradient-to-r from-honey-500 to-honey-600 hover:from-honey-600 hover:to-honey-700
                  text-white font-semibold py-3 px-6 rounded-xl mt-2
                  shadow-lg shadow-honey-500/30 hover:shadow-honey-500/40
                  transition-all duration-200
                  focus:outline-none focus:ring-2 focus:ring-honey-400 focus:ring-offset-2 dark:focus:ring-offset-slate-900
                  disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {/* Shine sweep */}
                <span className="absolute inset-0 -translate-x-full group-hover:translate-x-full transition-transform duration-700 ease-out bg-gradient-to-r from-transparent via-white/25 to-transparent" />
                <span className="relative z-10 flex items-center gap-2">
                  {isSubmitting ? (
                    <><Loader2 className="w-4 h-4 animate-spin" /> Prijavljivanje…</>
                  ) : (
                    <>Prijava <ArrowRight className="w-4 h-4 group-hover:translate-x-0.5 transition-transform" /></>
                  )}
                </span>
              </button>
            </form>

            {/* Link to register */}
            <p className="mt-6 text-center text-sm text-gray-500 dark:text-slate-400">
              Nemate račun?{' '}
              <Link to="/register" className="font-semibold text-honey-600 dark:text-honey-400 hover:text-honey-700 dark:hover:text-honey-300 transition-colors">
                Registrujte se
              </Link>
            </p>

          </div>
        </div>
      </div>
    </div>
  )
}
