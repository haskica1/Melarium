import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { format } from 'date-fns'
import { Building2, Check, Image as ImageIcon, Loader2, Trash2, Upload } from 'lucide-react'
import clsx from 'clsx'
import {
  useDeleteOrgLogo,
  useMyOrganization,
  useMyOrganizationLogo,
  useUpdateMyOrganization,
  useUploadOrgLogo,
} from '../../core/services/orgQueries'
import { useAuth } from '../../core/context/AuthContext'
import { useToast } from '../../core/context/ToastContext'
import { ConfirmDialog, ErrorState, LoadingSpinner } from '../../shared/components'
import { prepareLogoForUpload } from '../../shared/utils/imageDownscale'

/** Mirrors the server cap in OrgProfileService — the server stays the source of truth. */
const MAX_LOGO_BYTES = 2 * 1024 * 1024
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

interface OrgForm {
  name: string
  description: string
}

/**
 * "Moja organizacija" (SPEC-22) — the OrgAdmin's own organization. Everything on this page acts on
 * the caller's organization, resolved server-side from the token, so no id appears in any URL here.
 */
export default function MyOrganizationPage() {
  const { data: org, isLoading, isError, refetch } = useMyOrganization()
  const updateOrg = useUpdateMyOrganization()
  const { updateUser } = useAuth()
  const { toast } = useToast()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<OrgForm>({ defaultValues: { name: '', description: '' } })

  // Seeded through `reset` rather than `setValue` so the loaded values become the form's defaults —
  // with setValue the page would open with "Spremi" already enabled and nothing actually changed.
  useEffect(() => {
    if (org) reset({ name: org.name, description: org.description ?? '' })
  }, [org, reset])

  async function onSubmit(data: OrgForm) {
    try {
      const saved = await updateOrg.mutateAsync({
        name: data.name.trim(),
        description: data.description.trim() || null,
      })
      // The cached session carries the organisation name (it is the label under the profile avatar),
      // so a rename has to land there too or the old name survives until the next sign-in.
      updateUser({ organizationName: saved.name })
      reset({ name: saved.name, description: saved.description ?? '' })
      toast.success('Podaci organizacije su spremljeni.')
    } catch (e: unknown) {
      toast.error(e instanceof Error ? e.message : 'Greška pri spremanju organizacije.')
    }
  }

  if (isLoading) return <LoadingSpinner message="Učitavanje organizacije…" />
  if (isError || !org) return <ErrorState message="Greška pri učitavanju organizacije." onRetry={refetch} />

  return (
    <div className="animate-fade-in max-w-lg mx-auto">

      {/* ── Hero with logo ───────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none mb-6">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex items-center gap-4">
          <OrgLogo hasLogo={org.hasLogo} name={org.name} />
          <div className="min-w-0">
            <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50 truncate">
              {org.name}
            </h1>
            <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
              Na Melariumu od {format(new Date(org.createdAt), 'dd.MM.yyyy.')}
            </p>
          </div>
        </div>
      </div>

      {/* ── What the organization holds ──────────────────────────────────────── */}
      <div className="grid grid-cols-3 gap-3 mb-6">
        <CountTile label="Članovi" value={org.userCount} />
        <CountTile label="Pčelinjaci" value={org.apiaryCount} />
        <CountTile label="Košnice" value={org.beehiveCount} />
      </div>

      <div className="space-y-6">
        <form onSubmit={handleSubmit(onSubmit)} className="card space-y-4">
          <div className="flex items-center gap-2 mb-1">
            <Building2 className="w-4 h-4 text-honey-500" />
            <h3 className="font-semibold text-gray-700 dark:text-slate-200">Podaci organizacije</h3>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
              Naziv <span className="text-red-500">*</span>
            </label>
            <input
              {...register('name', {
                required: 'Naziv je obavezan',
                maxLength: { value: 200, message: 'Maks 200 znakova' },
              })}
              className={clsx('form-input', errors.name && 'border-red-400 focus:ring-red-300')}
              placeholder="Naziv organizacije"
            />
            {errors.name
              ? <p className="text-xs text-red-500 mt-1">{errors.name.message}</p>
              : <p className="text-xs text-gray-500 dark:text-slate-400 mt-1">
                  Ovaj naziv vide svi članovi vaše organizacije.
                </p>}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">Opis</label>
            <textarea
              {...register('description', { maxLength: { value: 1000, message: 'Maks 1000 znakova' } })}
              rows={3}
              className={clsx('form-input resize-none', errors.description && 'border-red-400 focus:ring-red-300')}
              placeholder="Čime se bavi vaša organizacija (opcionalno)"
            />
            {errors.description && <p className="text-xs text-red-500 mt-1">{errors.description.message}</p>}
          </div>

          <div className="flex justify-end pt-1">
            <button type="submit" disabled={isSubmitting || !isDirty} className="btn-primary text-sm">
              {isSubmitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              Spremi promjene
            </button>
          </div>
        </form>

        <LogoSection hasLogo={org.hasLogo} />
      </div>
    </div>
  )
}

// ── Logo ──────────────────────────────────────────────────────────────────────

/**
 * The logo is fetched through apiClient and rendered from an object URL — the storage bucket is
 * private and a plain <img src> cannot carry the Bearer header (the inspection-photo precedent).
 */
function OrgLogo({ hasLogo, name }: { hasLogo: boolean; name: string }) {
  const { data: url, isLoading } = useMyOrganizationLogo(hasLogo)

  if (hasLogo && (isLoading || url)) {
    return (
      <div className="w-16 h-16 shrink-0 rounded-2xl overflow-hidden bg-white dark:bg-slate-800 border border-honey-200 dark:border-slate-700 shadow-honey dark:shadow-none flex items-center justify-center">
        {url
          ? <img src={url} alt={`Logotip — ${name}`} className="w-full h-full object-contain" />
          : <Loader2 className="w-4 h-4 animate-spin text-gray-400 dark:text-slate-500" />}
      </div>
    )
  }

  // No logo, or it failed to load — the initial stands in, same as the profile avatar.
  return (
    <div className="w-16 h-16 shrink-0 rounded-2xl flex items-center justify-center font-bold text-2xl
                    bg-honey-100 text-honey-700 dark:bg-honey-500/20 dark:text-honey-300 shadow-honey dark:shadow-none">
      {name[0]?.toUpperCase() ?? '?'}
    </div>
  )
}

function LogoSection({ hasLogo }: { hasLogo: boolean }) {
  const fileRef = useRef<HTMLInputElement>(null)
  const [confirmRemove, setConfirmRemove] = useState(false)
  const upload = useUploadOrgLogo()
  const remove = useDeleteOrgLogo()
  const { toast } = useToast()

  async function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const picked = e.target.files?.[0]
    // Cleared straight away so picking the same file twice still fires a change event.
    e.target.value = ''
    if (!picked) return

    // Shrinks a phone shot and turns an iPhone HEIC into a JPEG; a small PNG/WebP passes through
    // untouched so a transparent logo keeps its transparency.
    const file = await prepareLogoForUpload(picked)

    if (file.type && !ALLOWED_TYPES.includes(file.type)) {
      toast.error('Dozvoljeni formati su JPEG, PNG i WebP.')
      return
    }
    if (file.size > MAX_LOGO_BYTES) {
      toast.error('Logotip ne smije biti veći od 2 MB.')
      return
    }

    try {
      await upload.mutateAsync(file)
      toast.success('Logotip je spremljen.')
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Greška pri slanju logotipa.')
    }
  }

  async function onRemove() {
    try {
      await remove.mutateAsync()
      setConfirmRemove(false)
      toast.success('Logotip je uklonjen.')
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Greška pri uklanjanju logotipa.')
    }
  }

  return (
    <div className="card space-y-4">
      <div className="flex items-center gap-2 mb-1">
        <ImageIcon className="w-4 h-4 text-honey-500" />
        <h3 className="font-semibold text-gray-700 dark:text-slate-200">Logotip</h3>
      </div>

      <p className="text-sm text-gray-500 dark:text-slate-400">
        Kvadratna slika izgleda najbolje. Prikazuje se uz naziv organizacije.
        Najviše 2 MB — JPEG, PNG ili WebP.
      </p>

      <input
        ref={fileRef}
        type="file"
        accept="image/jpeg,image/png,image/webp,image/heic,image/heif"
        onChange={onPick}
        className="hidden"
      />

      <div className="flex flex-wrap gap-3">
        <button
          type="button"
          onClick={() => fileRef.current?.click()}
          disabled={upload.isPending}
          className="btn-secondary text-sm"
        >
          {upload.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
          {hasLogo ? 'Zamijeni logotip' : 'Dodaj logotip'}
        </button>

        {hasLogo && (
          <button
            type="button"
            onClick={() => setConfirmRemove(true)}
            disabled={remove.isPending}
            className="btn-danger text-sm"
          >
            <Trash2 className="w-4 h-4" />
            Ukloni
          </button>
        )}
      </div>

      <ConfirmDialog
        isOpen={confirmRemove}
        title="Ukloni logotip"
        message="Ukloniti logotip organizacije? Sliku možete ponovo dodati kad god poželite."
        confirmLabel="Ukloni"
        onConfirm={onRemove}
        onCancel={() => setConfirmRemove(false)}
        isLoading={remove.isPending}
      />
    </div>
  )
}

// ── Small building blocks ─────────────────────────────────────────────────────

function CountTile({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-2xl border border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 px-3 py-3 text-center shadow-card dark:shadow-none">
      <p className="font-display text-xl font-bold text-gray-900 dark:text-slate-100">{value}</p>
      <p className="text-xs text-gray-500 dark:text-slate-400 mt-0.5">{label}</p>
    </div>
  )
}
