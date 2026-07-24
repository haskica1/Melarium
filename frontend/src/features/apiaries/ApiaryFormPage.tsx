import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { Loader2, MapPin, X } from 'lucide-react'
import { useApiary, useCreateApiary, useUpdateApiary } from '../../core/services/queries'
import { LoadingSpinner, ErrorMessage, FormHeader } from '../../shared/components'
import LocationPickerModal from '../../shared/components/LocationPickerModal'
import type { CreateApiaryPayload } from '../../core/models'

export default function ApiaryFormPage() {
  const { id } = useParams<{ id?: string }>()
  const isEditing = Boolean(id)
  const apiaryId = Number(id)
  const navigate = useNavigate()

  const { data: apiary, isLoading } = useApiary(apiaryId)
  const createMutation = useCreateApiary()
  const updateMutation = useUpdateApiary(apiaryId)

  const [mapOpen, setMapOpen] = useState(false)
  const [pickedLocation, setPickedLocation] = useState<{ lat: number; lng: number } | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateApiaryPayload>()

  // Populate form when editing
  useEffect(() => {
    if (apiary && isEditing) {
      reset({
        name: apiary.name,
        description: apiary.description ?? '',
      })
      if (apiary.latitude != null && apiary.longitude != null) {
        setPickedLocation({ lat: apiary.latitude, lng: apiary.longitude })
      }
    }
  }, [apiary, isEditing, reset])

  const onSubmit = async (data: CreateApiaryPayload) => {
    const payload: CreateApiaryPayload = {
      ...data,
      latitude:  pickedLocation?.lat  ?? null,
      longitude: pickedLocation?.lng ?? null,
    }
    if (isEditing) {
      await updateMutation.mutateAsync(payload)
      navigate(`/apiaries/${apiaryId}`)
    } else {
      const created = await createMutation.mutateAsync(payload)
      navigate(`/apiaries/${created.id}`)
    }
  }

  if (isEditing && isLoading) return <LoadingSpinner message="Učitavanje pčelinjaka…" />

  const mutationError = createMutation.error ?? updateMutation.error

  return (
    <div className="animate-fade-in max-w-lg mx-auto">
      <FormHeader
        icon="🏡"
        title={isEditing ? 'Uredi pčelinjak' : 'Novi pčelinjak'}
        onBack={() => navigate(isEditing ? `/apiaries/${apiaryId}` : '/apiaries')}
        backLabel="Nazad na pčelinjake"
      />

      <div className="card">
        {mutationError && (
          <div className="mb-5">
            <ErrorMessage message={mutationError.message} />
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-5">
          {/* Name */}
          <div>
            <label className="form-label" htmlFor="name">
              Naziv pčelinjaka <span className="text-red-500">*</span>
            </label>
            <input
              id="name"
              type="text"
              placeholder="npr. Planinski pčelinjak"
              className="form-input"
              {...register('name', {
                required: 'Naziv pčelinjaka je obavezan',
                maxLength: { value: 200, message: 'Naziv ne smije prelaziti 200 znakova' },
              })}
            />
            {errors.name && <p className="form-error">{errors.name.message}</p>}
          </div>

          {/* Description */}
          <div>
            <label className="form-label" htmlFor="description">
              Opis
            </label>
            <textarea
              id="description"
              rows={3}
              placeholder="Detalji lokacije, vrsta flore, napomene…"
              className="form-input resize-none"
              {...register('description', {
                maxLength: { value: 1000, message: 'Opis ne smije prelaziti 1000 znakova' },
              })}
            />
            {errors.description && <p className="form-error">{errors.description.message}</p>}
          </div>

          {/* Location */}
          <div>
            <label className="form-label flex items-center gap-1.5">
              <MapPin className="w-3.5 h-3.5 text-honey-600" />
              Lokacija <span className="text-gray-400 dark:text-slate-500 font-normal">(za vremensku prognozu)</span>
            </label>

            {pickedLocation ? (
              <div className="flex items-center gap-2 p-3 rounded-xl border border-honey-200 dark:border-slate-700 bg-honey-50 dark:bg-slate-800">
                <MapPin className="w-4 h-4 text-honey-600 dark:text-honey-400 shrink-0" />
                <span className="flex-1 text-sm font-mono text-gray-700 dark:text-slate-300">
                  {pickedLocation.lat.toFixed(6)}, {pickedLocation.lng.toFixed(6)}
                </span>
                <button
                  type="button"
                  onClick={() => setMapOpen(true)}
                  className="text-xs text-honey-600 dark:text-honey-400 hover:underline font-medium"
                >
                  Promijeni
                </button>
                <button
                  type="button"
                  onClick={() => setPickedLocation(null)}
                  className="p-0.5 rounded text-gray-400 dark:text-slate-500 hover:text-red-500 dark:hover:text-red-400 transition-colors"
                  aria-label="Ukloni lokaciju"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
            ) : (
              <button
                type="button"
                onClick={() => setMapOpen(true)}
                className="w-full flex items-center justify-center gap-2 py-3 rounded-xl border-2 border-dashed border-honey-200 dark:border-slate-700 text-honey-600 dark:text-honey-400 hover:border-honey-400 dark:hover:border-honey-500/50 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors text-sm font-medium"
              >
                <MapPin className="w-4 h-4" />
                Odaberi lokaciju na mapi
              </button>
            )}
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={() => navigate(isEditing ? `/apiaries/${apiaryId}` : '/apiaries')}
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
                'Napravi pčelinjak'
              )}
            </button>
          </div>
        </form>
      </div>

      {mapOpen && (
        <LocationPickerModal
          initialLat={pickedLocation?.lat}
          initialLng={pickedLocation?.lng}
          onConfirm={(lat, lng) => { setPickedLocation({ lat, lng }); setMapOpen(false) }}
          onClose={() => setMapOpen(false)}
        />
      )}
    </div>
  )
}
