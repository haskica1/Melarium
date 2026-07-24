import {
  AreaChart, Area,
  BarChart, Bar,
  PieChart, Pie, Cell,
  LineChart, Line,
  XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
} from 'recharts'
import { CheckSquare, Leaf } from 'lucide-react'
import { useStats } from '../../core/services/queries'
import { ErrorMessage, VitalCard, PageSkeleton } from '../../shared/components'
import type { NameValue, MonthTemp, PriorityStats } from '../../core/services/statsService'

// ── Colour palettes ───────────────────────────────────────────────────────────

const HONEY   = ['#f59e0b', '#fbbf24', '#fcd34d', '#fde68a', '#fef3c7']
const MULTI   = ['#f59e0b', '#10b981', '#6366f1', '#f43f5e', '#0ea5e9', '#8b5cf6', '#14b8a6', '#f97316']
const HONEY_GRADIENT_ID = 'honeyGrad'
const TEMP_GRADIENT_ID  = 'tempGrad'

// ── Custom tooltip ─────────────────────────────────────────────────────────────

function CustomTooltip({ active, payload, label }: {
  active?: boolean; payload?: { name: string; value: number | null; color?: string }[]; label?: string
}) {
  if (!active || !payload?.length) return null
  return (
    <div className="bg-white dark:bg-slate-800 border border-honey-100 dark:border-slate-700 rounded-xl shadow-lg px-4 py-3 text-sm">
      {label && <p className="font-semibold text-gray-700 dark:text-slate-200 mb-1">{label}</p>}
      {payload.map((p, i) => (
        <p key={i} style={{ color: p.color ?? '#f59e0b' }} className="font-medium">
          {p.name}: {p.value != null ? p.value : '—'}
        </p>
      ))}
    </div>
  )
}

// ── Custom pie label ───────────────────────────────────────────────────────────

function PieLabel({ cx, cy, midAngle, innerRadius, outerRadius, percent }: {
  cx: number; cy: number; midAngle: number; innerRadius: number; outerRadius: number;
  percent: number; name: string
}) {
  if (percent < 0.05) return null
  const RADIAN = Math.PI / 180
  const radius = innerRadius + (outerRadius - innerRadius) * 0.5
  const x = cx + radius * Math.cos(-midAngle * RADIAN)
  const y = cy + radius * Math.sin(-midAngle * RADIAN)
  return (
    <text x={x} y={y} fill="white" textAnchor="middle" dominantBaseline="central"
      fontSize={11} fontWeight="600">
      {`${(percent * 100).toFixed(0)}%`}
    </text>
  )
}

// ── Section wrapper ────────────────────────────────────────────────────────────

function Section({ title, icon, children }: { title: string; icon: string; children: React.ReactNode }) {
  return (
    <div className="card mb-6">
      <div className="flex items-center gap-2 mb-5">
        <span className="text-xl">{icon}</span>
        <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">{title}</h2>
      </div>
      {children}
    </div>
  )
}

/* KPI cards now use the shared VitalCard (with count-up animation). */

// ── Empty chart placeholder ────────────────────────────────────────────────────

function EmptyChart({ message = 'Nedovoljno podataka' }: { message?: string }) {
  return (
    <div className="flex items-center justify-center h-48 rounded-xl border-2 border-dashed border-gray-200 dark:border-slate-700">
      <p className="text-gray-400 dark:text-slate-500 text-sm">{message}</p>
    </div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function StatsPage() {
  const { data: stats, isLoading, error } = useStats()

  if (isLoading) return <PageSkeleton rows={6} />
  if (error)     return <ErrorMessage message={error.message} />
  if (!stats)    return null

  const hasInspectionData   = stats.inspectionsByMonth.some(m => m.count > 0)
  const hasTempData         = stats.temperatureByMonth.some(m => m.avgTemp != null)
  const hasHiveData         = stats.beehivesByType.length > 0
  const hasDietData         = stats.dietsByStatus.length > 0
  const hasApiaryData       = stats.apiariesByBeehiveCount.length > 0
  const hasTodoData         = stats.todosByPriority.length > 0
  const hasTopBeehivesData  = stats.topBeehivesByInspections.some(b => b.value > 0)
  const hasHarvestData      = stats.seasonTotalKg > 0 || stats.kgByHoneyType.length > 0 || stats.yearlyYield.some(y => y.value > 0)

  return (
    <div className="animate-fade-in">
      {/* ── Hero ──────────────────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none mb-6">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex items-center gap-4">
          <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
            📊
          </div>
          <div className="min-w-0">
            <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Statistike</h1>
            <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">Pregled aktivnosti vašeg pčelinjaka i zdravlja košnica</p>
          </div>
        </div>
      </div>

      {/* ── KPI Summary ──────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger mb-8">
        <VitalCard icon="🏡" label="Pčelinjaci"    value={String(stats.totalApiaries)}    gradient="from-honey-400 to-honey-600" />
        <VitalCard icon="🐝" label="Košnice"      value={String(stats.totalBeehives)}    gradient="from-amber-400 to-orange-500" />
        <VitalCard icon="🔍" label="Pregledi"   value={String(stats.totalInspections)} gradient="from-emerald-400 to-teal-600" />
        <VitalCard icon="🌿" label="Aktivna prehrana" value={String(stats.activeDiets)}    gradient="from-violet-400 to-indigo-600" />
      </div>

      {/* ── Inspections over time ─────────────────────────────────────────────── */}
      <Section title="Pregledi — posljednjih 12 mjeseci" icon="📋">
        {!hasInspectionData ? <EmptyChart /> : (
          <ResponsiveContainer width="100%" height={220}>
            <AreaChart data={stats.inspectionsByMonth} margin={{ top: 5, right: 10, left: -10, bottom: 0 }}>
              <defs>
                <linearGradient id={HONEY_GRADIENT_ID} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%"  stopColor="#f59e0b" stopOpacity={0.35} />
                  <stop offset="95%" stopColor="#f59e0b" stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
              <XAxis dataKey="month" tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
              <Tooltip content={<CustomTooltip />} />
              <Area
                type="monotone"
                dataKey="count"
                name="Pregledi"
                stroke="#f59e0b"
                strokeWidth={2.5}
                fill={`url(#${HONEY_GRADIENT_ID})`}
                dot={{ fill: '#f59e0b', r: 3 }}
                activeDot={{ r: 5 }}
              />
            </AreaChart>
          </ResponsiveContainer>
        )}
      </Section>

      {/* ── Temperature trend ────────────────────────────────────────────────── */}
      <Section title="Trend temperature — posljednjih 12 mjeseci (°C)" icon="🌡️">
        {!hasTempData ? <EmptyChart message="Nema zabilježenih podataka o temperaturi" /> : (
          <ResponsiveContainer width="100%" height={220}>
            <LineChart
              data={stats.temperatureByMonth as MonthTemp[]}
              margin={{ top: 5, right: 10, left: -10, bottom: 0 }}
            >
              <defs>
                <linearGradient id={TEMP_GRADIENT_ID} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%"  stopColor="#ef4444" stopOpacity={0.15} />
                  <stop offset="95%" stopColor="#ef4444" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
              <XAxis dataKey="month" tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
              <Tooltip content={<CustomTooltip />} />
              <Legend wrapperStyle={{ fontSize: 12 }} />
              <Line type="monotone" dataKey="maxTemp" name="Maks °C" stroke="#ef4444" strokeWidth={2} dot={false} connectNulls />
              <Line type="monotone" dataKey="avgTemp" name="Prosjek °C" stroke="#f97316" strokeWidth={2.5} dot={{ r: 3 }} connectNulls />
              <Line type="monotone" dataKey="minTemp" name="Min °C" stroke="#3b82f6" strokeWidth={2} dot={false} connectNulls />
            </LineChart>
          </ResponsiveContainer>
        )}
      </Section>

      {/* ── Two-column row: beehive type + material ───────────────────────────── */}
      <div className="grid sm:grid-cols-2 gap-6 mb-6">
        <div className="card">
          <div className="flex items-center gap-2 mb-5">
            <span className="text-xl">🏠</span>
            <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Tipovi košnica</h2>
          </div>
          {!hasHiveData ? <EmptyChart /> : (
            <ResponsiveContainer width="100%" height={200}>
              <PieChart>
                <Pie
                  data={stats.beehivesByType as NameValue[]}
                  dataKey="value"
                  nameKey="name"
                  cx="50%"
                  cy="50%"
                  innerRadius={50}
                  outerRadius={80}
                  paddingAngle={3}
                  labelLine={false}
                  label={PieLabel}
                >
                  {stats.beehivesByType.map((_, i) => (
                    <Cell key={i} fill={MULTI[i % MULTI.length]} />
                  ))}
                </Pie>
                <Tooltip formatter={(v, n) => [v, n]} />
                <Legend wrapperStyle={{ fontSize: 12 }} />
              </PieChart>
            </ResponsiveContainer>
          )}
        </div>

        <div className="card">
          <div className="flex items-center gap-2 mb-5">
            <span className="text-xl">🪵</span>
            <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Materijali košnica</h2>
          </div>
          {stats.beehivesByMaterial.length === 0 ? <EmptyChart /> : (
            <ResponsiveContainer width="100%" height={200}>
              <PieChart>
                <Pie
                  data={stats.beehivesByMaterial as NameValue[]}
                  dataKey="value"
                  nameKey="name"
                  cx="50%"
                  cy="50%"
                  innerRadius={50}
                  outerRadius={80}
                  paddingAngle={3}
                  labelLine={false}
                  label={PieLabel}
                >
                  {stats.beehivesByMaterial.map((_, i) => (
                    <Cell key={i} fill={HONEY[i % HONEY.length]} />
                  ))}
                </Pie>
                <Tooltip />
                <Legend wrapperStyle={{ fontSize: 12 }} />
              </PieChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>

      {/* ── Honey level distribution ──────────────────────────────────────────── */}
      <Section title="Distribucija nivoa meda" icon="🍯">
        {stats.honeyLevelDistribution.length === 0 ? <EmptyChart /> : (
          <ResponsiveContainer width="100%" height={200}>
            <BarChart
              data={stats.honeyLevelDistribution as NameValue[]}
              margin={{ top: 5, right: 10, left: -10, bottom: 0 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" vertical={false} />
              <XAxis dataKey="name" tick={{ fontSize: 12, fill: '#6b7280' }} axisLine={false} tickLine={false} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
              <Tooltip content={<CustomTooltip />} />
              <Bar dataKey="value" name="Pregledi" radius={[6, 6, 0, 0]}>
                {stats.honeyLevelDistribution.map((entry, i) => (
                  <Cell
                    key={i}
                    fill={entry.name === 'Visoko' ? '#10b981' : entry.name === 'Srednje' ? '#f59e0b' : '#ef4444'}
                  />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        )}
      </Section>

      {/* ── Beehives per apiary ───────────────────────────────────────────────── */}
      {hasApiaryData && (
        <Section title="Košnice po pčelinjaku" icon="🏡">
          <ResponsiveContainer width="100%" height={Math.max(160, stats.apiariesByBeehiveCount.length * 44)}>
            <BarChart
              data={stats.apiariesByBeehiveCount as NameValue[]}
              layout="vertical"
              margin={{ top: 0, right: 20, left: 10, bottom: 0 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" horizontal={false} />
              <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
              <YAxis type="category" dataKey="name" tick={{ fontSize: 12, fill: '#6b7280' }} axisLine={false} tickLine={false} width={120} />
              <Tooltip content={<CustomTooltip />} />
              <Bar dataKey="value" name="Košnice" fill="#f59e0b" radius={[0, 6, 6, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </Section>
      )}

      {/* ── Top beehives by inspection count ─────────────────────────────────── */}
      {hasTopBeehivesData && (
        <Section title="Najinspektiranije košnice" icon="🔍">
          <ResponsiveContainer width="100%" height={Math.max(160, stats.topBeehivesByInspections.length * 44)}>
            <BarChart
              data={stats.topBeehivesByInspections as NameValue[]}
              layout="vertical"
              margin={{ top: 0, right: 20, left: 10, bottom: 0 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" horizontal={false} />
              <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
              <YAxis type="category" dataKey="name" tick={{ fontSize: 12, fill: '#6b7280' }} axisLine={false} tickLine={false} width={120} />
              <Tooltip content={<CustomTooltip />} />
              <Bar dataKey="value" name="Pregledi" radius={[0, 6, 6, 0]}>
                {stats.topBeehivesByInspections.map((_, i) => (
                  <Cell key={i} fill={MULTI[i % MULTI.length]} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </Section>
      )}

      {/* ── Harvests (SPEC-02) ───────────────────────────────────────────────── */}
      {hasHarvestData && (
        <Section title={`Prinosi meda — sezona ${new Date().getFullYear()}.`} icon="🍯">
          {/* Headline numbers */}
          <div className="flex flex-wrap gap-3 mb-5">
            <div className="flex items-center gap-2 px-3 py-1.5 rounded-xl text-sm font-medium bg-honey-100 text-honey-800 dark:bg-honey-500/15 dark:text-honey-300">
              🍯 Ukupno: {stats.seasonTotalKg.toFixed(1).replace(/\.0$/, '')} kg
            </div>
            {stats.estimatedRevenue > 0 && (
              <div className="flex items-center gap-2 px-3 py-1.5 rounded-xl text-sm font-medium bg-emerald-100 text-emerald-800 dark:bg-emerald-500/15 dark:text-emerald-300">
                💰 Procij. prihod: {stats.estimatedRevenue.toFixed(0)} KM
              </div>
            )}
          </div>

          <div className="grid sm:grid-cols-2 gap-6">
            {/* Kg by honey type */}
            <div>
              <p className="text-sm font-medium text-gray-500 dark:text-slate-400 mb-3">Med po vrsti (kg)</p>
              {stats.kgByHoneyType.length === 0 ? <EmptyChart /> : (
                <ResponsiveContainer width="100%" height={220}>
                  <PieChart>
                    <Pie
                      data={stats.kgByHoneyType as NameValue[]}
                      dataKey="value" nameKey="name" cx="50%" cy="50%"
                      innerRadius={45} outerRadius={80} paddingAngle={3}
                      labelLine={false} label={PieLabel}
                    >
                      {stats.kgByHoneyType.map((_, i) => <Cell key={i} fill={HONEY[i % HONEY.length]} />)}
                    </Pie>
                    <Tooltip formatter={(v: number) => [`${v} kg`, 'Med']} />
                    <Legend wrapperStyle={{ fontSize: 12 }} />
                  </PieChart>
                </ResponsiveContainer>
              )}
            </div>

            {/* Top hives by yield */}
            <div>
              <p className="text-sm font-medium text-gray-500 dark:text-slate-400 mb-3">Najproduktivnije košnice (kg)</p>
              {stats.topHivesByYield.length === 0 ? <EmptyChart /> : (
                <ResponsiveContainer width="100%" height={Math.max(160, stats.topHivesByYield.length * 44)}>
                  <BarChart data={stats.topHivesByYield as NameValue[]} layout="vertical" margin={{ top: 0, right: 24, left: 10, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" horizontal={false} />
                    <XAxis type="number" tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
                    <YAxis type="category" dataKey="name" tick={{ fontSize: 12, fill: '#6b7280' }} axisLine={false} tickLine={false} width={110} />
                    <Tooltip formatter={(v: number) => [`${v} kg`, 'Prinos']} />
                    <Bar dataKey="value" name="Prinos" fill="#f59e0b" radius={[0, 6, 6, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              )}
            </div>
          </div>

          {/* Yearly yield trend */}
          {stats.yearlyYield.some(y => y.value > 0) && (
            <div className="mt-6">
              <p className="text-sm font-medium text-gray-500 dark:text-slate-400 mb-3">Prinos po godinama (kg)</p>
              <ResponsiveContainer width="100%" height={200}>
                <BarChart data={stats.yearlyYield as NameValue[]} margin={{ top: 5, right: 10, left: -10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" vertical={false} />
                  <XAxis dataKey="name" tick={{ fontSize: 12, fill: '#6b7280' }} axisLine={false} tickLine={false} />
                  <YAxis tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
                  <Tooltip formatter={(v: number) => [`${v} kg`, 'Med']} />
                  <Bar dataKey="value" name="Med" fill="#10b981" radius={[6, 6, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}

          {/* Yield per pasture (SPEC-10) — only for organizations that record moves */}
          {stats.kgByPasture.length > 0 && (
            <div className="mt-6">
              <p className="text-sm font-medium text-gray-500 dark:text-slate-400 mb-3">Prinos po pašnjaku (kg)</p>
              <ResponsiveContainer width="100%" height={Math.max(160, stats.kgByPasture.length * 44)}>
                <BarChart data={stats.kgByPasture as NameValue[]} layout="vertical" margin={{ top: 0, right: 24, left: 10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" horizontal={false} />
                  <XAxis type="number" tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
                  <YAxis type="category" dataKey="name" tick={{ fontSize: 12, fill: '#6b7280' }} axisLine={false} tickLine={false} width={130} />
                  <Tooltip formatter={(v: number) => [`${v} kg`, 'Prinos']} />
                  <Bar dataKey="value" name="Prinos" fill="#8b5cf6" radius={[0, 6, 6, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </Section>
      )}

      {/* ── Diet section ─────────────────────────────────────────────────────── */}
      {hasDietData && (
        <div className="grid sm:grid-cols-2 gap-6 mb-6">
          <div className="card">
            <div className="flex items-center gap-2 mb-5">
              <span className="text-xl">🌿</span>
              <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Status prehrane</h2>
            </div>
            <ResponsiveContainer width="100%" height={200}>
              <PieChart>
                <Pie
                  data={stats.dietsByStatus as NameValue[]}
                  dataKey="value"
                  nameKey="name"
                  cx="50%"
                  cy="50%"
                  innerRadius={50}
                  outerRadius={80}
                  paddingAngle={3}
                  labelLine={false}
                  label={PieLabel}
                >
                  {stats.dietsByStatus.map((entry, i) => {
                    const c = entry.name === 'Završeno' ? '#10b981'
                      : entry.name === 'U toku' ? '#6366f1'
                      : entry.name === 'Prijevremeno prekinuto' ? '#ef4444'
                      : '#9ca3af'
                    return <Cell key={i} fill={c} />
                  })}
                </Pie>
                <Tooltip />
                <Legend wrapperStyle={{ fontSize: 12 }} />
              </PieChart>
            </ResponsiveContainer>
          </div>

          <div className="card">
            <div className="flex items-center gap-2 mb-5">
              <Leaf className="w-5 h-5 text-honey-500" />
              <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Korišćeni tipovi hrane</h2>
            </div>
            <ResponsiveContainer width="100%" height={200}>
              <BarChart
                data={stats.dietsByFoodType as NameValue[]}
                margin={{ top: 5, right: 10, left: -10, bottom: 5 }}
              >
                <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" vertical={false} />
                <XAxis dataKey="name" tick={{ fontSize: 11, fill: '#6b7280' }} axisLine={false} tickLine={false} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
                <Tooltip content={<CustomTooltip />} />
                <Bar dataKey="value" name="Prehrane" radius={[6, 6, 0, 0]}>
                  {stats.dietsByFoodType.map((_, i) => (
                    <Cell key={i} fill={MULTI[i % MULTI.length]} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {/* ── Todos by priority ────────────────────────────────────────────────── */}
      {hasTodoData && (
        <Section title="Zadaci po prioritetu" icon="✅">
          <ResponsiveContainer width="100%" height={200}>
            <BarChart
              data={stats.todosByPriority as PriorityStats[]}
              margin={{ top: 5, right: 20, left: -10, bottom: 0 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" vertical={false} />
              <XAxis dataKey="priority" tick={{ fontSize: 12, fill: '#6b7280' }} axisLine={false} tickLine={false} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
              <Tooltip content={<CustomTooltip />} />
              <Legend wrapperStyle={{ fontSize: 12 }} />
              <Bar dataKey="total" name="Ukupno" fill="#e5e7eb" radius={[6, 6, 0, 0]} />
              <Bar dataKey="completed" name="Završeno" fill="#10b981" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
          {/* Completion rate chips */}
          <div className="flex flex-wrap gap-3 mt-4">
            {(stats.todosByPriority as PriorityStats[]).map((p) => {
              const pct = p.total > 0 ? Math.round((p.completed / p.total) * 100) : 0
              const color = p.priority === 'Visok' ? 'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300'
                : p.priority === 'Srednji' ? 'bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300'
                : 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300'
              return (
                <div key={p.priority} className={`flex items-center gap-2 px-3 py-1.5 rounded-xl text-sm font-medium ${color}`}>
                  <CheckSquare className="w-3.5 h-3.5" />
                  {p.priority}: {pct}% završeno
                </div>
              )
            })}
          </div>
        </Section>
      )}
    </div>
  )
}
