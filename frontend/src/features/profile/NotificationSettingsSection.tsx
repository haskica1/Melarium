import { useEffect, useState } from 'react'
import { Bell } from 'lucide-react'
import clsx from 'clsx'
import { EmailNotificationMode, type NotificationSettings } from '../../core/models'
import { useNotificationSettings, useUpdateNotificationSettings } from '../../core/services/notificationQueries'
import { useScrollIntoViewOnHash } from '../../core/hooks/useScrollIntoViewOnHash'
import { ErrorMessage, Skeleton } from '../../shared/components'
import Toggle from '../../shared/components/Toggle'

const EMAIL_OPTIONS: { value: EmailNotificationMode; label: string; hint: string }[] = [
  {
    value: EmailNotificationMode.All,
    label: 'Sva',
    hint: 'Kritična odmah, upozorenja i današnje obaveze u jednom jutarnjem e-mailu u 8h, a novi zadaci i dodjele odmah.',
  },
  {
    value: EmailNotificationMode.CriticalOnly,
    label: 'Samo kritična',
    hint: 'Npr. proljetni mraz ili zaključavanje podataka zbog isteka paketa. Ostalo vidite u aplikaciji.',
  },
  {
    value: EmailNotificationMode.Off,
    label: 'Isključeno',
    hint: 'Ništa e-mailom. Kritična obavještenja i dalje vidite u aplikaciji.',
  },
]

/**
 * The user's own notification preferences (SPEC-29). E-mail has three modes; in the app, only the
 * alert categories can be hidden — Critical cannot, and it is shown switched on and locked so the
 * user sees that it exists rather than wondering where it went.
 */
export default function NotificationSettingsSection() {
  const { data, isPending, isError } = useNotificationSettings()
  const update = useUpdateNotificationSettings()
  const [form, setForm] = useState<NotificationSettings | null>(null)

  useEffect(() => { if (data) setForm(data) }, [data])
  useScrollIntoViewOnHash('obavjestenja', !isPending)

  const dirty = !!form && !!data && (
    form.emailMode !== data.emailMode ||
    form.normalAlertsInApp !== data.normalAlertsInApp ||
    form.infoAlertsInApp !== data.infoAlertsInApp
  )

  return (
    // The e-mail footer's "Promijenite postavke obavještenja" lands here (ADR-048).
    <div id="obavjestenja" className="card scroll-mt-20">
      <div className="flex items-center gap-2 mb-3">
        <Bell className="w-4 h-4 text-honey-500" />
        <h3 className="font-semibold text-gray-700 dark:text-slate-200">Obavještenja</h3>
      </div>

      {isPending || !form ? (
        isError ? <ErrorMessage message="Podešavanja obavještenja nisu učitana." /> : <Skeleton className="h-40 rounded-xl" />
      ) : (
        <>
          <p className="text-sm font-medium text-gray-800 dark:text-slate-100">E-mail</p>
          <div role="radiogroup" aria-label="E-mail obavještenja" className="mt-2 grid gap-2 sm:grid-cols-3">
            {EMAIL_OPTIONS.map(o => (
              <label
                key={o.value}
                className={clsx(
                  'flex flex-col gap-1 rounded-xl border p-3 cursor-pointer transition-colors',
                  form.emailMode === o.value
                    ? 'border-honey-400 bg-honey-50 dark:border-honey-500/60 dark:bg-honey-500/10'
                    : 'border-gray-200 dark:border-slate-700 hover:border-honey-300 dark:hover:border-slate-600',
                )}
              >
                <span className="flex items-center gap-2 text-sm font-medium text-gray-900 dark:text-slate-100">
                  <input
                    type="radio"
                    name="email-mode"
                    className="text-honey-600 focus:ring-honey-400"
                    checked={form.emailMode === o.value}
                    onChange={() => setForm({ ...form, emailMode: o.value })}
                  />
                  {o.label}
                </span>
                <span className="text-xs text-gray-500 dark:text-slate-400">{o.hint}</span>
              </label>
            ))}
          </div>
          <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
            Promjena lozinke, novi račun i prenos organizacije stižu e-mailom uvijek — to su sigurnosne poruke.
          </p>

          <p className="mt-5 text-sm font-medium text-gray-800 dark:text-slate-100">U aplikaciji</p>
          <div className="divide-y divide-gray-100 dark:divide-slate-800">
            <Toggle
              label="Kritična upozorenja"
              hint="Npr. proljetni mraz — ne mogu se isključiti."
              checked
              disabled
              onChange={() => {}}
            />
            <Toggle
              label="Obična upozorenja"
              hint="Košnice bez pregleda, trake, kašnjenje prehrane ili tretmana, početak sezonske faze."
              checked={form.normalAlertsInApp}
              onChange={v => setForm({ ...form, normalAlertsInApp: v })}
            />
            <Toggle
              label="Savjeti"
              hint="Npr. stara matica koju treba planirati za zamjenu."
              checked={form.infoAlertsInApp}
              onChange={v => setForm({ ...form, infoAlertsInApp: v })}
            />
          </div>

          <div className="mt-4 flex items-center justify-end gap-3">
            {update.isSuccess && !dirty && <span className="text-sm text-emerald-600 dark:text-emerald-400">Sačuvano ✓</span>}
            {update.isError && <span className="text-sm text-red-600 dark:text-red-400">Nije sačuvano. Pokušajte ponovo.</span>}
            <button
              type="button"
              className="btn-primary"
              disabled={!dirty || update.isPending}
              onClick={() => update.mutate(form)}
            >
              {update.isPending ? 'Čuvam…' : 'Sačuvaj'}
            </button>
          </div>
        </>
      )}
    </div>
  )
}
