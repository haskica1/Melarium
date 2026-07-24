import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation } from '@tanstack/react-query'
import { Check, Eye, EyeOff, KeyRound, Mail, User } from 'lucide-react'
import { useAuth } from '../../core/context/AuthContext'
import { profileService } from '../../core/services/profileService'
import type { UpdateProfilePayload } from '../../core/services/profileService'
import clsx from 'clsx'

interface ProfileForm {
  firstName: string
  lastName: string
  email: string
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

export default function ProfilePage() {
  const { user, updateUser } = useAuth()
  const [showCurrent, setShowCurrent] = useState(false)
  const [showNew, setShowNew] = useState(false)
  const [showConfirm, setShowConfirm] = useState(false)
  const [saved, setSaved] = useState(false)

  const {
    register,
    handleSubmit,
    watch,
    setError,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<ProfileForm>({
    defaultValues: {
      firstName: user?.firstName ?? '',
      lastName: user?.lastName ?? '',
      email: user?.email ?? '',
      currentPassword: '',
      newPassword: '',
      confirmPassword: '',
    },
  })

  const newPassword = watch('newPassword')

  const mutation = useMutation({
    mutationFn: (payload: UpdateProfilePayload) => profileService.update(payload),
    onSuccess: (data) => {
      updateUser({ firstName: data.firstName, lastName: data.lastName, email: data.email })
      reset({
        firstName: data.firstName,
        lastName: data.lastName,
        email: data.email,
        currentPassword: '',
        newPassword: '',
        confirmPassword: '',
      })
      setSaved(true)
      setTimeout(() => setSaved(false), 3000)
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const msg = err?.response?.data?.message ?? 'Something went wrong.'
      if (msg.toLowerCase().includes('password')) {
        setError('currentPassword', { message: msg })
      } else if (msg.toLowerCase().includes('email')) {
        setError('email', { message: msg })
      } else {
        setError('root', { message: msg })
      }
    },
  })

  async function onSubmit(data: ProfileForm) {
    if (data.newPassword && data.newPassword !== data.confirmPassword) {
      setError('confirmPassword', { message: 'Lozinke se ne podudaraju.' })
      return
    }

    const payload: UpdateProfilePayload = {
      firstName: data.firstName,
      lastName: data.lastName,
      email: data.email,
    }

    if (data.newPassword) {
      payload.currentPassword = data.currentPassword
      payload.newPassword = data.newPassword
    }

    await mutation.mutateAsync(payload)
  }

  const avatarClass = user?.role === 'SystemAdmin'
    ? 'bg-purple-100 text-purple-700 dark:bg-purple-500/20 dark:text-purple-300'
    : user?.role === 'OrganizationAdmin'
    ? 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-300'
    : 'bg-honey-100 text-honey-700 dark:bg-honey-500/20 dark:text-honey-300'

  const roleLabel = user?.role === 'SystemAdmin'
    ? 'Sistem Admin'
    : user?.role === 'OrganizationAdmin'
    ? `Org Admin · ${user?.organizationName}`
    : user?.role === 'ApiaryAdmin'
    ? `Admin · ${user?.organizationName}`
    : user?.organizationName ?? ''

  return (
    <div className="animate-fade-in max-w-lg mx-auto">
      {/* ── Hero with avatar ─────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none mb-6">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex items-center gap-4">
          <div className={clsx('w-16 h-16 rounded-2xl flex items-center justify-center font-bold text-2xl shrink-0 shadow-honey dark:shadow-none', avatarClass)}>
            {user?.firstName[0] ?? '?'}
          </div>
          <div className="min-w-0">
            <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50 truncate">
              {user?.firstName} {user?.lastName}
            </h1>
            <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">{roleLabel}</p>
          </div>
        </div>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">

        {/* Personal info */}
        <div className="card space-y-4">
          <div className="flex items-center gap-2 mb-1">
            <User className="w-4 h-4 text-honey-500" />
            <h3 className="font-semibold text-gray-700 dark:text-slate-200">Lični podaci</h3>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">Ime</label>
              <input
                {...register('firstName', { required: 'Ime je obavezno' })}
                className={clsx('form-input', errors.firstName && 'border-red-400 focus:ring-red-300')}
                placeholder="Ime"
              />
              {errors.firstName && <p className="text-xs text-red-500 mt-1">{errors.firstName.message}</p>}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">Prezime</label>
              <input
                {...register('lastName', { required: 'Prezime je obavezno' })}
                className={clsx('form-input', errors.lastName && 'border-red-400 focus:ring-red-300')}
                placeholder="Prezime"
              />
              {errors.lastName && <p className="text-xs text-red-500 mt-1">{errors.lastName.message}</p>}
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">E-pošta</label>
            <div className="relative">
              <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-slate-500" />
              <input
                {...register('email', {
                  required: 'E-pošta je obavezna',
                  pattern: { value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/, message: 'Nevažeća e-pošta' },
                })}
                type="email"
                className={clsx('form-input pl-9', errors.email && 'border-red-400 focus:ring-red-300')}
                placeholder="you@example.com"
              />
            </div>
            {errors.email && <p className="text-xs text-red-500 mt-1">{errors.email.message}</p>}
          </div>
        </div>

        {/* Password change */}
        <div className="card space-y-4">
          <div className="flex items-center gap-2 mb-1">
            <KeyRound className="w-4 h-4 text-honey-500" />
            <h3 className="font-semibold text-gray-700 dark:text-slate-200">Promjena lozinke</h3>
            <span className="text-xs text-gray-400 dark:text-slate-500 font-normal">(opcionalno)</span>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">Trenutna lozinka</label>
            <div className="relative">
              <input
                {...register('currentPassword')}
                type={showCurrent ? 'text' : 'password'}
                className={clsx('form-input pr-10', errors.currentPassword && 'border-red-400 focus:ring-red-300')}
                placeholder="Unesite trenutnu lozinku"
                autoComplete="current-password"
              />
              <button type="button" onClick={() => setShowCurrent(v => !v)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 dark:text-slate-500 hover:text-gray-600 dark:hover:text-slate-300">
                {showCurrent ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
            {errors.currentPassword && <p className="text-xs text-red-500 mt-1">{errors.currentPassword.message}</p>}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">Nova lozinka</label>
            <div className="relative">
              <input
                {...register('newPassword', {
                  minLength: newPassword ? { value: 6, message: 'Minimum 6 znakova' } : undefined,
                })}
                type={showNew ? 'text' : 'password'}
                className={clsx('form-input pr-10', errors.newPassword && 'border-red-400 focus:ring-red-300')}
                placeholder="Unesite novu lozinku"
                autoComplete="new-password"
              />
              <button type="button" onClick={() => setShowNew(v => !v)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 dark:text-slate-500 hover:text-gray-600 dark:hover:text-slate-300">
                {showNew ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
            {errors.newPassword && <p className="text-xs text-red-500 mt-1">{errors.newPassword.message}</p>}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">Potvrdi novu lozinku</label>
            <div className="relative">
              <input
                {...register('confirmPassword')}
                type={showConfirm ? 'text' : 'password'}
                className={clsx('form-input pr-10', errors.confirmPassword && 'border-red-400 focus:ring-red-300')}
                placeholder="Ponovite novu lozinku"
                autoComplete="new-password"
              />
              <button type="button" onClick={() => setShowConfirm(v => !v)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 dark:text-slate-500 hover:text-gray-600 dark:hover:text-slate-300">
                {showConfirm ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
            {errors.confirmPassword && <p className="text-xs text-red-500 mt-1">{errors.confirmPassword.message}</p>}
          </div>
        </div>

        {/* Root error */}
        {errors.root && (
          <p className="text-sm text-red-600 dark:text-red-300 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 rounded-xl px-4 py-2.5">
            {errors.root.message}
          </p>
        )}

        {/* Submit */}
        <div className="flex items-center justify-end gap-3">
          {saved && (
            <span className="flex items-center gap-1.5 text-sm text-green-600 font-medium animate-fade-in">
              <Check className="w-4 h-4" /> Uspješno sačuvano
            </span>
          )}
          <button
            type="submit"
            disabled={isSubmitting || !isDirty}
            className="btn-primary"
          >
            {isSubmitting ? 'Čuvanje…' : 'Spremi promjene'}
          </button>
        </div>
      </form>
    </div>
  )
}
