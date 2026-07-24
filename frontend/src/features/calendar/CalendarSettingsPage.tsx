import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowLeft, Bell, CalendarClock, Check, Copy, Info, RefreshCw } from 'lucide-react'
import clsx from 'clsx'
import { useCalendarFeedUrl, useCalendarSettings, useRotateCalendarFeed, useUpdateCalendarSettings } from '../../core/services/queries'
import type { CalendarSettings } from '../../core/models'
import { ErrorMessage, PageSkeleton } from '../../shared/components'

export default function CalendarSettingsPage() {
  const { data: feed } = useCalendarFeedUrl()
  const { data: settings, isLoading, isError } = useCalendarSettings()
  const rotate = useRotateCalendarFeed()
  const update = useUpdateCalendarSettings()

  const [form, setForm] = useState<CalendarSettings | null>(null)
  const [copied, setCopied] = useState(false)

  useEffect(() => { if (settings) setForm(settings) }, [settings])

  if (isLoading || !form) return <PageSkeleton />
  if (isError)            return <ErrorMessage message="Greška pri učitavanju postavki kalendara." />

  const dirty = settings != null && (Object.keys(form) as (keyof CalendarSettings)[]).some(k => form[k] !== settings[k])

  async function copyUrl() {
    if (!feed?.url) return
    try {
      await navigator.clipboard.writeText(feed.url)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch { /* clipboard blocked — user can select the field manually */ }
  }

  function set<K extends keyof CalendarSettings>(key: K, value: CalendarSettings[K]) {
    setForm(f => (f ? ({ ...f, [key]: value } as CalendarSettings) : f))
  }

  return (
    <div className="space-y-6 animate-fade-in max-w-3xl">

      {/* ── Header ─────────────────────────────────────────────────────────────── */}
      <div>
        <Link to="/calendar" className="inline-flex items-center gap-1.5 text-sm text-gray-500 dark:text-slate-400 hover:text-honey-700 dark:hover:text-honey-300 transition-colors">
          <ArrowLeft className="w-4 h-4" /> Nazad na kalendar
        </Link>
        <h1 className="mt-2 font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Kalendar i podsjetnici</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-slate-400">
          Prikaži svoje obaveze (hranjenja, zadatke, rokove) u Google, Apple ili Outlook kalendaru — i dobij podsjetnik svako jutro u 8h.
        </p>
      </div>

      {/* ── ICS subscription ───────────────────────────────────────────────────── */}
      <section className="card p-5 sm:p-6 space-y-4">
        <div className="flex items-start gap-3">
          <div className="w-10 h-10 shrink-0 rounded-xl bg-honey-100 dark:bg-honey-500/15 flex items-center justify-center text-honey-700 dark:text-honey-300">
            <CalendarClock className="w-5 h-5" />
          </div>
          <div>
            <h2 className="text-base font-semibold text-gray-900 dark:text-slate-100">Pretplata na kalendar</h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">Jedna tajna adresa koju dodaš u svoj kalendar. Radi na svim uređajima.</p>
          </div>
        </div>

        <div className="flex flex-col sm:flex-row gap-2">
          <input
            readOnly
            value={feed?.url ?? ''}
            onFocus={e => e.currentTarget.select()}
            className="flex-1 min-w-0 rounded-xl border border-gray-200 dark:border-slate-700 bg-gray-50 dark:bg-slate-800 px-3 py-2 text-sm font-mono text-gray-700 dark:text-slate-300"
          />
          <div className="flex gap-2">
            <button onClick={copyUrl} className="btn-secondary inline-flex items-center gap-1.5 shrink-0">
              {copied ? <Check className="w-4 h-4 text-emerald-600" /> : <Copy className="w-4 h-4" />}
              {copied ? 'Kopirano' : 'Kopiraj'}
            </button>
            <button
              onClick={() => rotate.mutate()}
              disabled={rotate.isPending}
              title="Poništi staru adresu i napravi novu"
              className="btn-secondary inline-flex items-center gap-1.5 shrink-0"
            >
              <RefreshCw className={clsx('w-4 h-4', rotate.isPending && 'animate-spin')} /> Nova adresa
            </button>
          </div>
        </div>

        <div className="flex items-start gap-2 rounded-xl bg-sky-50 dark:bg-sky-500/10 border border-sky-100 dark:border-sky-500/20 p-3 text-xs text-sky-800 dark:text-sky-300">
          <Info className="w-4 h-4 shrink-0 mt-0.5" />
          <p>Vanjski kalendari osvježavaju pretplatu periodično (Google i do ~24h), pa nove obaveze mogu kasniti. Podsjetnik u 8h svejedno stiže kroz aplikaciju odmah.</p>
        </div>

        {/* Provider instructions */}
        <div className="space-y-1.5">
          <ProviderSteps title="Google Calendar" steps={[
            'Otvori Google Calendar na računaru.',
            'Pored „Drugi kalendari" klikni + → „Pretplati se putem URL-a".',
            'Zalijepi adresu iznad i klikni „Dodaj kalendar".',
          ]} />
          <ProviderSteps title="Apple kalendar (iPhone)" steps={[
            'Postavke → Aplikacije → Kalendar → Računi → Dodaj račun → Drugo.',
            'Odaberi „Dodaj pretplatnički kalendar".',
            'Zalijepi adresu iznad i potvrdi.',
          ]} />
          <ProviderSteps title="Outlook" steps={[
            'Outlook.com → Kalendar → „Dodaj kalendar".',
            'Odaberi „Pretplati se s weba".',
            'Zalijepi adresu iznad i sačuvaj.',
          ]} />
        </div>
      </section>

      {/* ── Categories ─────────────────────────────────────────────────────────── */}
      <section className="card p-5 sm:p-6">
        <h2 className="text-base font-semibold text-gray-900 dark:text-slate-100">Šta se prikazuje</h2>
        <p className="text-sm text-gray-500 dark:text-slate-400">Odaberi koje obaveze idu u kalendar i jutarnji podsjetnik.</p>
        <div className="mt-2 divide-y divide-gray-100 dark:divide-slate-800">
          <Toggle label="Hranjenja (prihrane)" checked={form.syncFeedings} onChange={v => set('syncFeedings', v)} />
          <Toggle label="Zadaci (todo) s rokom" checked={form.syncTodos} onChange={v => set('syncTodos', v)} />
          <Toggle label="Tretmani — vađenje traka i istek karence" checked={form.syncTreatments} onChange={v => set('syncTreatments', v)} />
          <Toggle label="Preporučeni pregledi" hint="Košnice koje dugo nisu pregledane" checked={form.syncInspections} onChange={v => set('syncInspections', v)} />
        </div>
      </section>

      {/* ── Reminder + feed toggle ─────────────────────────────────────────────── */}
      <section className="card p-5 sm:p-6">
        <div className="flex items-start gap-3">
          <div className="w-10 h-10 shrink-0 rounded-xl bg-honey-100 dark:bg-honey-500/15 flex items-center justify-center text-honey-700 dark:text-honey-300">
            <Bell className="w-5 h-5" />
          </div>
          <div>
            <h2 className="text-base font-semibold text-gray-900 dark:text-slate-100">Podsjetnici</h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">Neovisno o vanjskom kalendaru.</p>
          </div>
        </div>
        <div className="mt-2 divide-y divide-gray-100 dark:divide-slate-800">
          <Toggle label="Jutarnji podsjetnik u 8h" hint="Zvono + email sa svim današnjim obavezama" checked={form.dailyAgendaEnabled} onChange={v => set('dailyAgendaEnabled', v)} />
          <Toggle label="Pretplata na kalendar aktivna" hint="Isključi da privremeno zaustaviš feed bez mijenjanja adrese" checked={form.feedEnabled} onChange={v => set('feedEnabled', v)} />
        </div>
      </section>

      {/* ── Save bar ───────────────────────────────────────────────────────────── */}
      <div className="flex items-center justify-end gap-3">
        {update.isSuccess && !dirty && <span className="text-sm text-emerald-600 dark:text-emerald-400">Sačuvano ✓</span>}
        <button onClick={() => form && update.mutate(form)} disabled={!dirty || update.isPending} className="btn-primary">
          {update.isPending ? 'Čuvam…' : 'Sačuvaj postavke'}
        </button>
      </div>
    </div>
  )
}

function Toggle({ checked, onChange, label, hint }: { checked: boolean; onChange: (v: boolean) => void; label: string; hint?: string }) {
  return (
    <button type="button" onClick={() => onChange(!checked)} className="w-full flex items-start justify-between gap-4 py-3 text-left">
      <span>
        <span className="block text-sm font-medium text-gray-800 dark:text-slate-100">{label}</span>
        {hint && <span className="block text-xs text-gray-500 dark:text-slate-400 mt-0.5">{hint}</span>}
      </span>
      <span
        role="switch"
        aria-checked={checked}
        className={clsx('mt-0.5 relative inline-flex h-6 w-11 shrink-0 rounded-full transition-colors', checked ? 'bg-honey-500' : 'bg-gray-300 dark:bg-slate-600')}
      >
        <span className={clsx('absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition-transform', checked ? 'translate-x-[22px]' : 'translate-x-0.5')} />
      </span>
    </button>
  )
}

function ProviderSteps({ title, steps }: { title: string; steps: string[] }) {
  return (
    <details className="group rounded-xl border border-gray-100 dark:border-slate-800 overflow-hidden">
      <summary className="cursor-pointer list-none flex items-center justify-between px-3.5 py-2.5 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800/60">
        {title}
        <span className="text-gray-400 transition-transform group-open:rotate-45">＋</span>
      </summary>
      <ol className="px-3.5 pb-3 pt-1 space-y-1.5 text-sm text-gray-600 dark:text-slate-400 list-decimal list-inside">
        {steps.map((s, i) => <li key={i}>{s}</li>)}
      </ol>
    </details>
  )
}
