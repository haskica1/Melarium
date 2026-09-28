import { Link } from 'react-router-dom'
import { useAuth } from '../../core/context/AuthContext'
import { usePermissions } from '../../core/hooks/usePermissions'
import { useDashboard, useDashboardWeather } from '../../core/services/dashboardQueries'
import { ErrorState, PageSkeleton, VitalCard } from '../../shared/components'
import SeasonHero from './SeasonHero'
import AttentionCard from './AttentionCard'
import ObligationsCard from './ObligationsCard'
import WeatherCard from './WeatherCard'
import HiveStatusCard from './HiveStatusCard'
import ProgrammesCard from './ProgrammesCard'
import YieldCard from './YieldCard'
import OpenTodosCard from './OpenTodosCard'
import PlanCard from './PlanCard'
import QuickActions from './QuickActions'
import LearningSpotlight from './LearningSpotlight'
import { count, daysFromToday, formatKg, inMonth } from './format'

/**
 * The start page for every role except SystemAdmin (SPEC-29). The order is the phone's order —
 * what needs doing, what is due, the weather — and on wide screens the numbers move to the top.
 */
export default function DashboardPage() {
  const { user } = useAuth()
  const { isOrgAdmin, canManageApiaries } = usePermissions()
  const dashboard = useDashboard()
  const weather = useDashboardWeather()

  if (dashboard.isPending) return <PageSkeleton />
  if (dashboard.isError) return <ErrorState onRetry={() => dashboard.refetch()} />

  const d = dashboard.data
  const todayCount = d.obligations.filter(o => daysFromToday(o.date) <= 0).length
  const frostCount = (weather.data ?? []).filter(w => w.frostPriority).length
  const year = new Date().getFullYear()

  return (
    <div className="animate-fade-in space-y-6">
      <SeasonHero
        firstName={user?.firstName}
        season={d.season}
        todayCount={todayCount}
        attentionCount={d.attention.length + frostCount}
      />

      {d.counts.apiaries === 0 ? (
        <StartHere canCreate={canManageApiaries} />
      ) : (
        <div className="grid gap-4 sm:gap-6 lg:grid-cols-2">
          <AttentionCard items={d.attention} weather={weather.data} resting={d.hivesResting} />
          <ObligationsCard items={d.obligations} />
          <WeatherCard
            data={weather.data}
            isPending={weather.isPending}
            isError={weather.isError}
            apiaryCount={d.counts.apiaries}
          />

          {/* Numbers first on a wide screen, after the weather on a phone. */}
          <div className="grid grid-cols-2 xl:grid-cols-4 gap-3 sm:gap-4 lg:col-span-2 lg:order-first">
            <VitalCard icon="🐝" label="Košnice" value={String(d.counts.beehives)}
              sub={`u ${count(d.counts.apiaries, 'pčelinjaku', 'pčelinjaka', 'pčelinjaka')}`}
              gradient="from-amber-400 to-orange-500" />
            <VitalCard icon="🔍" label="Pregledi" value={String(d.counts.inspectionsThisMonth)}
              sub={inMonth()}
              gradient="from-honey-400 to-honey-600" />
            {/* Whole kilograms: VitalCard counts up the leading number and reads it the English way. */}
            <VitalCard icon="🍯" label={`Prinos ${year}.`} value={`${Math.round(d.counts.yieldThisYearKg)} kg`}
              sub={`prošle god. do danas: ${formatKg(d.counts.yieldLastYearToDateKg)}`}
              gradient="from-emerald-400 to-teal-600" />
            <VitalCard icon="✅" label="Otvoreni zadaci" value={String(d.counts.openTodos)}
              sub={d.counts.overdueTodos > 0 ? `kasni: ${d.counts.overdueTodos}` : 'ništa ne kasni'}
              subAlert={d.counts.overdueTodos > 0}
              gradient="from-sky-400 to-blue-600" />
          </div>

          <OpenTodosCard todos={d.openTodos} total={d.counts.openTodos} />
          <HiveStatusCard
            statuses={d.hiveStatus}
            resting={d.hivesResting}
            springStart={d.season.nextStart}
          />
          <ProgrammesCard programmes={d.programmes} />
          <YieldCard months={d.yieldByMonth} year={year} />
          {isOrgAdmin && <PlanCard />}
          <QuickActions />
          {d.topic && <LearningSpotlight topic={d.topic} />}
        </div>
      )}
    </div>
  )
}

/**
 * Nothing to show yet. Someone who can create apiaries is sent to the apiary list, where "Prvi
 * koraci" lives; a member who cannot is told the list is empty because nothing was assigned yet.
 */
function StartHere({ canCreate }: { canCreate: boolean }) {
  return (
    <div className="card text-center py-10">
      <p className="text-4xl mb-3">🏡</p>
      {canCreate ? (
        <>
          <h2 className="font-display text-xl font-bold text-gray-900 dark:text-slate-50">Počnite s prvim pčelinjakom</h2>
          <p className="mt-2 text-sm text-gray-600 dark:text-slate-400 max-w-md mx-auto">
            Kad dodate pčelinjak i košnice, ovdje ćete vidjeti šta traži pažnju, obaveze, vrijeme i prinos.
          </p>
          <Link to="/apiaries" className="btn-primary mt-5 inline-flex">Otvori pčelinjake</Link>
        </>
      ) : (
        <>
          <h2 className="font-display text-xl font-bold text-gray-900 dark:text-slate-50">Još nemate dodijeljenih košnica</h2>
          <p className="mt-2 text-sm text-gray-600 dark:text-slate-400 max-w-md mx-auto">
            Administrator vaše organizacije vam treba dodijeliti košnice. Nakon toga ovdje vidite šta traži
            pažnju, obaveze i vrijeme za njih.
          </p>
        </>
      )}
    </div>
  )
}
