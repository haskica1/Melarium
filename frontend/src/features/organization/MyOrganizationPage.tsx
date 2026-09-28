import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { addDays, format, parseISO } from 'date-fns'
import { Building2, Check, Image as ImageIcon, Loader2, Mountain, Trash2, Upload } from 'lucide-react'
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
import { SeasonPhaseLabels, type SeasonPhaseRange } from '../../core/models'
import { prepareLogoForUpload } from '../../shared/utils/imageDownscale'

/** Mirrors the server cap in OrgProfileService — the server stays the source of truth. */
const MAX_LOGO_BYTES = 2 * 1024 * 1024
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

interface OrgForm {
  name: string
  description: string
  seasonShiftDays: number
}

/** Mirrors SeasonCalendar.MinShiftDays / MaxShiftDays — the server validates the same range. */
const MIN_SHIFT = -14
const MAX_SHIFT = 30

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
    watch,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<OrgForm>({ defaultValues: { name: '', description: '', seasonShiftDays: 0 } })

  // Seeded through `reset` rather than `setValue` so the loaded values become the form's defaults —
  // with setValue the page would open with "Spremi" already enabled and nothing actually changed.
  useEffect(() => {
    if (org) reset({ name: org.name, description: org.description ?? '', seasonShiftDays: org.seasonShiftDays })
  }, [org, reset])

  const enteredShift = watch('seasonShiftDays')

  async function onSubmit(data: OrgForm) {
    try {
      const saved = await updateOrg.mutateAsync({
        name: data.name.trim(),
        description: data.description.trim() || null,
        seasonShiftDays: data.seasonShiftDays,
      })
      // The cached session carries the organisation name (it is the label under the profile avatar),
      // so a rename has to land there too or the old name survives until the next sign-in.
      updateUser({ organizationName: saved.name })
      reset({ name: saved.name, description: saved.description ?? '', seasonShiftDays: saved.seasonShiftDays })
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

          {/* ── Season (SPEC-29) ── */}
          <div className="pt-2 border-t border-honey-100 dark:border-slate-800">
            <div className="flex items-center gap-2 mb-2 mt-2">
              <Mountain className="w-4 h-4 text-honey-500" />
              <h3 className="font-semibold text-gray-700 dark:text-slate-200">Sezona</h3>
            </div>
            <label htmlFor="season-shift" className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
              Pomak sezone (dana)
            </label>
            <input
              id="season-shift"
              type="number"
              inputMode="numeric"
              step={1}
              min={MIN_SHIFT}
              max={MAX_SHIFT}
              {...register('seasonShiftDays', {
                valueAsNumber: true,
                validate: v => (Number.isInteger(v) && v >= MIN_SHIFT && v <= MAX_SHIFT)
                  || `Unesite cijeli broj od ${MIN_SHIFT} do +${MAX_SHIFT}.`,
              })}
              className={clsx('form-input w-32', errors.seasonShiftDays && 'border-red-400 focus:ring-red-300')}
            />
            {errors.seasonShiftDays
              ? <p className="text-xs text-red-500 mt-1">{errors.seasonShiftDays.message}</p>
              : <p className="text-xs text-gray-500 dark:text-slate-400 mt-1">
                  Na većoj visini proljeće kasni, a zima dolazi ranije: +N pomjera proljeće kasnije i jesen
                  ranije za N dana, negativan broj obratno. 1. august ostaje isti. Po ovome rade podsjetnici
                  i početna stranica za sve članove.
                </p>}

            <SeasonPreview
              phases={org.seasonPhases}
              savedShift={org.seasonShiftDays}
              shift={Number.isInteger(enteredShift) ? Math.min(Math.max(enteredShift, MIN_SHIFT), MAX_SHIFT) : org.seasonShiftDays}
            />
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

// ── Season preview ────────────────────────────────────────────────────────────

/**
 * The five phases under the entered shift, before saving. Derived from the phases the server sent for
 * the *saved* shift, not from dates copied into the client: spring boundaries moved by +saved, autumn
 * ones by −saved, so undoing that and applying the new value gives exactly what the server will
 * compute from its own configuration.
 */
function SeasonPreview({ phases, savedShift, shift }: { phases: SeasonPhaseRange[]; savedShift: number; shift: number }) {
  if (phases.length !== 5) return null

  const delta = shift - savedShift
  const move = (iso: string, days: number) => format(addDays(parseISO(iso), days), 'yyyy-MM-dd')
  const [winter, spring, main, late, wintering] = phases

  const starts = {
    winter: move(winter.start, -delta),
    spring: move(spring.start, delta),
    main: move(main.start, delta),
    late: late.start,
    wintering: move(wintering.start, -delta),
    nextWinter: move(addDaysIso(wintering.end, 1), -delta),
  }
  const preview: SeasonPhaseRange[] = [
    { phase: winter.phase, start: starts.winter, end: move(starts.spring, -1) },
    { phase: spring.phase, start: starts.spring, end: move(starts.main, -1) },
    { phase: main.phase, start: starts.main, end: move(starts.late, -1) },
    { phase: late.phase, start: starts.late, end: move(starts.wintering, -1) },
    { phase: wintering.phase, start: starts.wintering, end: move(starts.nextWinter, -1) },
  ]

  return (
    <ul className="mt-3 rounded-xl border border-honey-100 dark:border-slate-800 divide-y divide-honey-100 dark:divide-slate-800">
      {preview.map(p => (
        <li key={p.phase} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
          <span className="text-gray-700 dark:text-slate-300">{SeasonPhaseLabels[p.phase]}</span>
          <span className="tabular-nums text-gray-500 dark:text-slate-400">
            {format(parseISO(p.start), 'dd.MM.')} – {format(parseISO(p.end), 'dd.MM.')}
          </span>
        </li>
      ))}
    </ul>
  )
}

function addDaysIso(iso: string, days: number): string {
  return format(addDays(parseISO(iso), days), 'yyyy-MM-dd')
}

function CountTile({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-2xl border border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 px-3 py-3 text-center shadow-card dark:shadow-none">
      <p className="font-display text-xl font-bold text-gray-900 dark:text-slate-100">{value}</p>
      <p className="text-xs text-gray-500 dark:text-slate-400 mt-0.5">{label}</p>
    </div>
  )
}
