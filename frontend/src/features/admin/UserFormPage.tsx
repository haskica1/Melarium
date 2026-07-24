import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { Loader2 } from 'lucide-react'
import {
  useAdminUser,
  useCreateAdminUser,
  useUpdateAdminUser,
  useAdminOrganizations,
  useApiariesByOrganization,
  useBeehivesByOrganization,
} from '../../core/services/adminQueries'
import { FormHeader } from '../../shared/components'

interface UserForm {
  firstName: string
  lastName: string
  email: string
  password: string
  role: string
  organizationId: string
  apiaryId: string
}

export default function UserFormPage() {
  const { id } = useParams<{ id: string }>()
  const isEdit = !!id
  const userId = id ? parseInt(id) : 0
  const navigate = useNavigate()

  const { data: existing, isLoading: loadingExisting } = useAdminUser(userId)
  const { data: organizations = [] } = useAdminOrganizations()
  const createUser = useCreateAdminUser()
  const updateUser = useUpdateAdminUser(userId)

  const [selectedBeehiveIds, setSelectedBeehiveIds] = useState<number[]>([])

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<UserForm>({ defaultValues: { role: 'OrganizationAdmin' } })

  const selectedRole = watch('role')
  const selectedOrgId = watch('organizationId')
  const orgIdNumber = selectedOrgId ? parseInt(selectedOrgId) : 0

  const { data: apiaries = [] } = useApiariesByOrganization(orgIdNumber)
  const { data: beehives = [] } = useBeehivesByOrganization(orgIdNumber)

  const needsOrg    = selectedRole !== 'SystemAdmin'
  const needsApiary = selectedRole === 'ApiaryAdmin'
  const needsHives  = selectedRole === 'Beekeeper'

  useEffect(() => {
    if (existing) {
      reset({
        firstName: existing.firstName,
        lastName: existing.lastName,
        email: existing.email,
        password: '',
        role: existing.role,
        organizationId: existing.organizationId?.toString() ?? '',
        apiaryId: existing.apiaryId?.toString() ?? '',
      })
      setSelectedBeehiveIds(existing.assignedBeehiveIds ?? [])
    }
  }, [existing, reset])

  function toggleBeehive(beehiveId: number) {
    setSelectedBeehiveIds(prev =>
      prev.includes(beehiveId)
        ? prev.filter(id => id !== beehiveId)
        : [...prev, beehiveId]
    )
  }

  async function onSubmit(data: UserForm) {
    const orgId    = data.organizationId ? parseInt(data.organizationId) : null
    const apiaryId = data.apiaryId ? parseInt(data.apiaryId) : null

    try {
      if (isEdit) {
        await updateUser.mutateAsync({
          firstName: data.firstName,
          lastName: data.lastName,
          email: data.email,
          role: data.role,
          organizationId: orgId,
          apiaryId: needsApiary ? apiaryId : null,
          assignedBeehiveIds: needsHives ? selectedBeehiveIds : [],
        })
      } else {
        await createUser.mutateAsync({
          firstName: data.firstName,
          lastName: data.lastName,
          email: data.email,
          password: data.password,
          role: data.role,
          organizationId: orgId,
          apiaryId: needsApiary ? apiaryId : null,
          assignedBeehiveIds: needsHives ? selectedBeehiveIds : [],
        })
      }
      navigate('/admin')
    } catch (e: any) {
      const detail = e?.response?.data?.detail ?? e?.message ?? 'An error occurred.'
      setError('root', { message: detail })
    }
  }

  if (isEdit && loadingExisting) {
    return (
      <div className="flex justify-center py-20">
        <Loader2 className="w-6 h-6 animate-spin text-honey-500" />
      </div>
    )
  }

  const inputCls = (hasError: boolean) =>
    `w-full px-4 py-3 rounded-xl border text-sm outline-none transition-all bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 dark:[color-scheme:dark] ${
      hasError
        ? 'border-red-400 focus:ring-2 focus:ring-red-200'
        : 'border-gray-200 dark:border-slate-700 focus:border-honey-400 focus:ring-2 focus:ring-honey-100'
    }`

  return (
    <div className="max-w-xl mx-auto">
      <FormHeader
        icon="👤"
        title={isEdit ? 'Uredi korisnika' : 'Novi korisnik'}
        onBack={() => navigate('/admin')}
        backLabel="Nazad na kontrolnu ploču"
      />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-8 py-8">
        {errors.root && (
          <div className="mb-5 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm">
            {errors.root.message}
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                Ime <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                placeholder="Ime"
                className={inputCls(!!errors.firstName)}
                {...register('firstName', { required: 'Obavezno' })}
              />
              {errors.firstName && <p className="mt-1 text-xs text-red-600">{errors.firstName.message}</p>}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                Prezime <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                placeholder="Prezime"
                className={inputCls(!!errors.lastName)}
                {...register('lastName', { required: 'Obavezno' })}
              />
              {errors.lastName && <p className="mt-1 text-xs text-red-600">{errors.lastName.message}</p>}
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
              E-pošta <span className="text-red-500">*</span>
            </label>
            <input
              type="email"
              placeholder="user@example.com"
              className={inputCls(!!errors.email)}
              {...register('email', {
                required: 'E-pošta je obavezna',
                pattern: { value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/, message: 'Nevažeća e-pošta' },
              })}
            />
            {errors.email && <p className="mt-1.5 text-xs text-red-600">{errors.email.message}</p>}
          </div>

          {!isEdit && (
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                Lozinka <span className="text-red-500">*</span>
              </label>
              <input
                type="password"
                placeholder="••••••••"
                className={inputCls(!!errors.password)}
                {...register('password', {
                  required: isEdit ? false : 'Lozinka je obavezna',
                  minLength: { value: 6, message: 'Minimum 6 znakova' },
                })}
              />
              {errors.password && <p className="mt-1.5 text-xs text-red-600">{errors.password.message}</p>}
            </div>
          )}

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
              Uloga <span className="text-red-500">*</span>
            </label>
            <select
              className="w-full px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
              {...register('role', { required: 'Role is required' })}
            >
              <option value="OrganizationAdmin">Org Admin</option>
              <option value="ApiaryAdmin">Admin</option>
              <option value="Beekeeper">Korisnik</option>
              <option value="SystemAdmin">Sistem Admin</option>
            </select>
            <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">
              {selectedRole === 'OrganizationAdmin' && 'Može upravljati svim pčelinjacima, košnicama, programima prehrane, pregledima i zadacima u organizaciji.'}
              {selectedRole === 'ApiaryAdmin' && 'Ograničen na jedan pčelinjak — može upravljati košnicama, programima prehrane, pregledima i zadacima.'}
              {selectedRole === 'Beekeeper' && 'Može kreirati preglede, upravljati zadacima dodijeljenih košnica i pregledavati programe prehrane.'}
              {selectedRole === 'SystemAdmin' && 'Puni pristup platformi — nije potrebna organizacija.'}
            </p>
          </div>

          {needsOrg && (
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                Organizacija <span className="text-red-500">*</span>
              </label>
              <select
                className={inputCls(!!errors.organizationId)}
                {...register('organizationId', {
                  validate: (v) => !needsOrg || !!v || 'Organizacija je obavezna za ovu ulogu',
                })}
              >
                <option value="">Odaberite organizaciju…</option>
                {organizations.map((org) => (
                  <option key={org.id} value={org.id}>{org.name}</option>
                ))}
              </select>
              {errors.organizationId && <p className="mt-1.5 text-xs text-red-600">{errors.organizationId.message}</p>}
            </div>
          )}

          {needsApiary && (
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                Pčelinjak <span className="text-red-500">*</span>
              </label>
              <select
                className={inputCls(!!errors.apiaryId)}
                {...register('apiaryId', {
                  validate: (v) => !needsApiary || !!v || 'Pčelinjak je obavezan za Admin korisnike',
                })}
                disabled={!orgIdNumber}
              >
                <option value="">{orgIdNumber ? 'Odaberite pčelinjak…' : 'Prvo odaberite organizaciju'}</option>
                {apiaries.map((a) => (
                  <option key={a.id} value={a.id}>{a.name}</option>
                ))}
              </select>
              {errors.apiaryId && <p className="mt-1.5 text-xs text-red-600">{errors.apiaryId.message}</p>}
            </div>
          )}

          {needsHives && (
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1.5">
                Dodijeljene košnice
              </label>
              {!orgIdNumber ? (
                <p className="text-xs text-gray-400 dark:text-slate-500 py-2">Prvo odaberite organizaciju da vidite dostupne košnice.</p>
              ) : beehives.length === 0 ? (
                <p className="text-xs text-gray-400 dark:text-slate-500 py-2">Nema košnica u ovoj organizaciji.</p>
              ) : (
                <div className="border border-gray-200 dark:border-slate-700 rounded-xl divide-y divide-gray-100 dark:divide-slate-800 max-h-48 overflow-y-auto">
                  {beehives.map((b) => (
                    <label
                      key={b.id}
                      className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
                    >
                      <input
                        type="checkbox"
                        checked={selectedBeehiveIds.includes(b.id)}
                        onChange={() => toggleBeehive(b.id)}
                        className="w-4 h-4 accent-honey-500"
                      />
                      <span className="text-sm text-gray-800 dark:text-slate-200">{b.name}</span>
                      <span className="text-xs text-gray-400 dark:text-slate-500 ml-auto">{b.apiaryName}</span>
                    </label>
                  ))}
                </div>
              )}
              <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">
                {selectedBeehiveIds.length > 0
                  ? `${selectedBeehiveIds.length} ${selectedBeehiveIds.length === 1 ? 'košnica odabrana' : 'košnica odabrano'}`
                  : 'Nema dodijeljenih košnica — korisnik može pregledati ali ne upravljati zadacima.'}
              </p>
            </div>
          )}

          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={() => navigate('/admin')}
              className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200
                hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
            >
              Otkaži
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl
                bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold
                disabled:opacity-60 disabled:cursor-not-allowed transition-colors"
            >
              {isSubmitting ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
              {isEdit ? 'Spremi promjene' : 'Napravi korisnika'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
