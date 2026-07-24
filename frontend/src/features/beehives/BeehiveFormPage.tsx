import { useEffect } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { Loader2 } from 'lucide-react'
import { useBeehive, useCreateBeehive, useUpdateBeehive } from '../../core/services/queries'
import { LoadingSpinner, ErrorMessage, FormHeader } from '../../shared/components'
import {
  BeehiveType,
  BeehiveTypeLabels,
  BeehiveMaterial,
  BeehiveMaterialLabels,
} from '../../core/models'
import type { CreateBeehivePayload } from '../../core/models'

export default function BeehiveFormPage() {
  const { id } = useParams<{ id?: string }>()
  const [searchParams] = useSearchParams()
  const isEditing = Boolean(id)
  const beehiveId = Number(id)
  const preselectedApiaryId = Number(searchParams.get('apiaryId') ?? 0)
  const navigate = useNavigate()

  const { data: beehive, isLoading } = useBeehive(beehiveId)
  const createMutation = useCreateBeehive(preselectedApiaryId)
  const updateMutation = useUpdateBeehive(beehiveId, beehive?.apiaryId ?? preselectedApiaryId)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateBeehivePayload>({
    defaultValues: {
      type: BeehiveType.Langstroth,
      material: BeehiveMaterial.Wood,
      dateCreated: new Date().toISOString().split('T')[0],
      apiaryId: preselectedApiaryId || undefined,
    },
  })

  useEffect(() => {
    if (beehive && isEditing) {
      reset({
        name: beehive.name,
        type: beehive.type,
        material: beehive.material,
        dateCreated: beehive.dateCreated.split('T')[0],
        notes: beehive.notes ?? '',
        labelNumber: beehive.labelNumber ?? '',
        apiaryId: beehive.apiaryId,
      })
    }
  }, [beehive, isEditing, reset])

  const onSubmit = async (data: CreateBeehivePayload) => {
    const payload = {
      ...data,
      type: Number(data.type),
      material: Number(data.material),
      apiaryId: Number(data.apiaryId),
      labelNumber: data.labelNumber?.trim() || undefined,
    }

    if (isEditing) {
      await updateMutation.mutateAsync(payload)
      navigate(`/beehives/${beehiveId}`)
    } else {
      const created = await createMutation.mutateAsync(payload)
      navigate(`/beehives/${created.id}`)
    }
  }

  if (isEditing && isLoading) return <LoadingSpinner message="Učitavanje košnice…" />

  const mutationError = createMutation.error ?? updateMutation.error
  const backUrl = isEditing
    ? `/beehives/${beehiveId}`
    : preselectedApiaryId
    ? `/apiaries/${preselectedApiaryId}`
    : '/apiaries'

  return (
    <div className="animate-fade-in max-w-lg mx-auto">
      <FormHeader
        icon="🐝"
        title={isEditing ? 'Uredi košnicu' : 'Nova košnica'}
        onBack={() => navigate(backUrl)}
        backLabel="Nazad"
      />

      <div className="card">
        {mutationError && (
          <div className="mb-5">
            <ErrorMessage message={mutationError.message} />
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-5">
          {/* Hidden field — keeps apiaryId in form state so handleSubmit always includes it */}
          <input type="hidden" {...register('apiaryId', { valueAsNumber: true })} />

          {/* Name */}
          <div>
            <label className="form-label" htmlFor="name">
              Naziv / Oznaka <span className="text-red-500">*</span>
            </label>
            <input
              id="name"
              type="text"
              placeholder="npr. Košnica A1"
              className="form-input"
              {...register('name', {
                required: 'Naziv košnice je obavezan',
                maxLength: { value: 100, message: 'Naziv ne smije prelaziti 100 znakova' },
              })}
            />
            {errors.name && <p className="form-error">{errors.name.message}</p>}
          </div>

          {/* Label number — the physical number painted on the hive, used by scan-by-number */}
          <div>
            <label className="form-label" htmlFor="labelNumber">Broj na košnici</label>
            <input
              id="labelNumber"
              type="text"
              inputMode="numeric"
              placeholder="npr. 1"
              className="form-input"
              {...register('labelNumber', {
                maxLength: { value: 20, message: 'Oznaka ne smije prelaziti 20 znakova' },
              })}
            />
            <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">
              Broj naslikan na košnici — omogućava otvaranje slikanjem broja, bez QR koda.
            </p>
            {errors.labelNumber && <p className="form-error">{errors.labelNumber.message}</p>}
          </div>

          {/* Type + Material row */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="form-label" htmlFor="type">
                Tip <span className="text-red-500">*</span>
              </label>
              <select
                id="type"
                className="form-input"
                {...register('type', { required: 'Tip je obavezan' })}
              >
                {Object.entries(BeehiveTypeLabels).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
              {errors.type && <p className="form-error">{errors.type.message}</p>}
            </div>

            <div>
              <label className="form-label" htmlFor="material">
                Materijal <span className="text-red-500">*</span>
              </label>
              <select
                id="material"
                className="form-input"
                {...register('material', { required: 'Materijal je obavezan' })}
              >
                {Object.entries(BeehiveMaterialLabels).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
              {errors.material && <p className="form-error">{errors.material.message}</p>}
            </div>
          </div>

          {/* Date Created */}
          <div>
            <label className="form-label" htmlFor="dateCreated">
              Datum osnivanja <span className="text-red-500">*</span>
            </label>
            <input
              id="dateCreated"
              type="date"
              className="form-input"
              max={new Date().toISOString().split('T')[0]}
              {...register('dateCreated', {
                required: 'Datum osnivanja je obavezan',
                validate: v => v <= new Date().toISOString().split('T')[0] || 'Datum ne može biti u budućnosti',
              })}
            />
            {errors.dateCreated && <p className="form-error">{errors.dateCreated.message}</p>}
          </div>

          {/* Notes */}
          <div>
            <label className="form-label" htmlFor="notes">Napomene</label>
            <textarea
              id="notes"
              rows={3}
              placeholder="Info o matici, snaga kolonije, posebne napomene…"
              className="form-input resize-none"
              {...register('notes', {
                maxLength: { value: 2000, message: 'Napomene ne smiju prelaziti 2000 znakova' },
              })}
            />
            {errors.notes && <p className="form-error">{errors.notes.message}</p>}
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={() => navigate(backUrl)}
              className="btn-secondary flex-1"
            >
              Otkaži
            </button>
            <button type="submit" className="btn-primary flex-1" disabled={isSubmitting}>
              {isSubmitting ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : isEditing ? (
                'Spremi promjene'
              ) : (
                'Napravi košnicu'
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
