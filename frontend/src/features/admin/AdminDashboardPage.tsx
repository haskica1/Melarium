import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { differenceInDays, format, formatDistanceToNow } from 'date-fns'
import { bs } from 'date-fns/locale'
import {
  ArrowDown, ArrowUp, Building2, ChevronsUpDown, Loader2, MailCheck, MailWarning,
  Pencil, Plus, Search, Trash2, Users,
} from 'lucide-react'
import {
  useAdminOrganizationLogo,
  useAdminOrganizations,
  useAdminUsers,
  useDeleteAdminUser,
  useDeleteOrganization,
} from '../../core/services/adminQueries'
import { VitalCard, Skeleton, ConfirmDialog, ErrorState } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'
import { PlanType, PlanTypeLabels, type AdminOrganization, type AdminUser } from '../../core/models'

// ── Sorting ───────────────────────────────────────────────────────────────────

type SortDir = 'asc' | 'desc'

/** Sortable value of one row. Null means "no answer" and always sorts as the smallest. */
type SortValue = string | number

function useSort<K extends string>(initialKey: K, initialDir: SortDir) {
  const [key, setKey] = useState<K>(initialKey)
  const [dir, setDir] = useState<SortDir>(initialDir)

  // Clicking the active column flips direction; a new column starts from its own natural
  // direction — names read A→Z, dates and counts read biggest/newest first.
  function toggle(next: K, naturalDir: SortDir) {
    if (next === key) setDir(d => (d === 'asc' ? 'desc' : 'asc'))
    else { setKey(next); setDir(naturalDir) }
  }

  return { key, dir, toggle }
}

function sortRows<T, K extends string>(rows: T[], key: K, dir: SortDir, value: (row: T, key: K) => SortValue) {
  const factor = dir === 'asc' ? 1 : -1
  return [...rows].sort((a, b) => {
    const av = value(a, key)
    const bv = value(b, key)
    if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * factor
    return String(av).localeCompare(String(bv), 'bs') * factor
  })
}

const timeValue = (at?: string | null) => (at ? new Date(at).getTime() : 0)

// ── Filters ───────────────────────────────────────────────────────────────────

/** Plan filter: a specific plan, everything, or the billing follow-up list. */
type PlanFilter = 'all' | 'expired' | `${PlanType}`
type ActivityFilter = 'all' | 'active' | 'dormant' | 'inactive' | 'never'
type UserStatusFilter = 'all' | 'unverified' | 'never' | 'idle'

/**
 * Expiry is judged at the end of the stored day, the same way `PlanBadge` shows it — a plan valid
 * "until 31.12." is not expired at 09:00 on 31.12.
 */
const isPlanExpired = (org: AdminOrganization) =>
  !!org.planValidUntil && new Date(org.planValidUntil).setHours(23, 59, 59, 999) < Date.now()

/** Days since the moment, or null when it never happened. Shared by the filters and the cells. */
const daysSince = (at?: string | null) => (at ? differenceInDays(new Date(), new Date(at)) : null)

type OrgSortKey = 'name' | 'owner' | 'plan' | 'users' | 'apiaries' | 'beehives' | 'activity'
type UserSortKey = 'name' | 'email' | 'role' | 'organization' | 'lastLogin' | 'createdAt'

const ROLE_LABELS: Record<string, string> = {
  SystemAdmin: 'Sistem Admin',
  OrganizationAdmin: 'Org Admin',
  ApiaryAdmin: 'Admin',
  Beekeeper: 'Korisnik',
}

export default function AdminDashboardPage() {
  const navigate = useNavigate()
  const { data: organizations = [], isLoading: orgsLoading, isError: orgsError, refetch: refetchOrgs } =
    useAdminOrganizations()
  const { data: users = [], isLoading: usersLoading, isError: usersError, refetch: refetchUsers } =
    useAdminUsers()
  const deleteOrg = useDeleteOrganization()
  const deleteUser = useDeleteAdminUser()
  const { toast } = useToast()

  const [confirmTarget, setConfirmTarget] = useState<{ kind: 'org' | 'user'; id: number; name: string } | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const [orgQuery, setOrgQuery] = useState('')
  const [userQuery, setUserQuery] = useState('')
  const [planFilter, setPlanFilter] = useState<PlanFilter>('all')
  const [activityFilter, setActivityFilter] = useState<ActivityFilter>('all')
  const [roleFilter, setRoleFilter] = useState<string>('all')
  const [userStatusFilter, setUserStatusFilter] = useState<UserStatusFilter>('all')

  const orgSort = useSort<OrgSortKey>('name', 'asc')
  const userSort = useSort<UserSortKey>('name', 'asc')

  // ── Derived vitals ──
  const totalApiaries = organizations.reduce((s, o) => s + o.apiaryCount, 0)
  const adminCount = users.filter(u => u.role !== 'Beekeeper').length
  const expiredCount = organizations.filter(isPlanExpired).length

  // ── Filtered + sorted lists ──
  const visibleOrgs = useMemo(() => {
    const q = orgQuery.trim().toLowerCase()
    const filtered = organizations.filter(o => {
      if (q) {
        const haystack = [o.name, o.description, o.ownerName, o.ownerEmail, o.ownerPhone, o.planNotes]
        if (!haystack.some(v => v?.toLowerCase().includes(q))) return false
      }
      if (planFilter === 'expired' && !isPlanExpired(o)) return false
      if (planFilter !== 'all' && planFilter !== 'expired' && String(o.plan) !== planFilter) return false

      if (activityFilter !== 'all') {
        const days = daysSince(o.lastActivityAt)
        if (activityFilter === 'never' && days !== null) return false
        if (activityFilter === 'active' && (days === null || days > 30)) return false
        if (activityFilter === 'dormant' && (days === null || days <= 30 || days > 90)) return false
        if (activityFilter === 'inactive' && (days === null || days <= 90)) return false
      }
      return true
    })

    return sortRows(filtered, orgSort.key, orgSort.dir, (o, key): SortValue => {
      switch (key) {
        case 'name':     return o.name
        case 'owner':    return o.ownerName ?? ''
        case 'plan':     return o.plan
        case 'users':    return o.userCount
        case 'apiaries': return o.apiaryCount
        case 'beehives': return o.beehiveCount
        case 'activity': return timeValue(o.lastActivityAt)
      }
    })
  }, [organizations, orgQuery, planFilter, activityFilter, orgSort.key, orgSort.dir])

  const visibleUsers = useMemo(() => {
    const q = userQuery.trim().toLowerCase()
    const filtered = users.filter(u => {
      if (q) {
        const haystack = [`${u.firstName} ${u.lastName}`, u.email, u.phone, u.organizationName, u.apiaryName, u.role]
        if (!haystack.some(v => v?.toLowerCase().includes(q))) return false
      }
      if (roleFilter !== 'all' && u.role !== roleFilter) return false

      if (userStatusFilter === 'unverified' && !!u.emailVerifiedAt) return false
      if (userStatusFilter === 'never' && !!u.lastLoginAt) return false
      if (userStatusFilter === 'idle') {
        const days = daysSince(u.lastLoginAt)
        if (days === null || days <= 90) return false
      }
      return true
    })

    return sortRows(filtered, userSort.key, userSort.dir, (u, key): SortValue => {
      switch (key) {
        case 'name':         return `${u.lastName} ${u.firstName}`
        case 'email':        return u.email
        case 'role':         return ROLE_LABELS[u.role] ?? u.role
        case 'organization': return u.organizationName ?? ''
        case 'lastLogin':    return timeValue(u.lastLoginAt)
        case 'createdAt':    return timeValue(u.createdAt)
      }
    })
  }, [users, userQuery, roleFilter, userStatusFilter, userSort.key, userSort.dir])

  const orgFiltersActive = !!orgQuery.trim() || planFilter !== 'all' || activityFilter !== 'all'
  const userFiltersActive = !!userQuery.trim() || roleFilter !== 'all' || userStatusFilter !== 'all'

  async function handleConfirmDelete() {
    if (!confirmTarget) return
    const { kind, id, name } = confirmTarget
    setIsDeleting(true)
    try {
      if (kind === 'org') await deleteOrg.mutateAsync(id)
      else await deleteUser.mutateAsync(id)
      toast.success(`${kind === 'org' ? 'Organizacija' : 'Korisnik'} "${name}" obrisan/a.`)
      setConfirmTarget(null)
    } catch (e: any) {
      toast.error(e?.response?.data?.detail ?? e?.message ?? 'Greška pri brisanju. Pokušajte ponovo.')
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <div className="animate-fade-in space-y-6">

      {/* ── Hero ──────────────────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center gap-4 min-w-0">
            <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
              🌐
            </div>
            <div className="min-w-0">
              <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">
                Sistemska kontrolna ploča
              </h1>
              <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
                Upravljajte svim organizacijama i korisnicima na platformi.
              </p>
            </div>
          </div>
          <div className="shrink-0 flex items-center gap-2 flex-wrap">
            <button
              onClick={() => navigate('/admin/learning-topics')}
              className="flex items-center gap-1.5 px-3 py-2 rounded-xl border border-honey-200 dark:border-slate-700 bg-white/70 dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-700 transition-colors"
            >
              🎓 Uredi edukaciju
            </button>
            <button
              onClick={() => navigate('/admin/announcements')}
              className="flex items-center gap-1.5 px-3 py-2 rounded-xl border border-honey-200 dark:border-slate-700 bg-white/70 dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-700 transition-colors"
            >
              ✨ Šta je novo
            </button>
          </div>
        </div>
      </div>

      {/* ── Vitals strip ──────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-5 gap-3 sm:gap-4 stagger">
        <VitalCard icon="🏢" label="Organizacije" value={String(organizations.length)} sub="na platformi"           gradient="from-honey-400 to-honey-600" />
        <VitalCard icon="👥" label="Korisnici"    value={String(users.length)}         sub="ukupno računa"          gradient="from-amber-400 to-orange-500" />
        <VitalCard icon="🏡" label="Pčelinjaci"   value={String(totalApiaries)}        sub="u svim organizacijama"  gradient="from-sky-400 to-blue-600" />
        <VitalCard icon="🛡️" label="Admini"       value={String(adminCount)}           sub="uloge s pravima"        gradient="from-violet-400 to-indigo-600" />
        {/* Manual annual billing (SPEC-09) has no dunning job — this tile is the reminder. */}
        <VitalCard icon="⏰" label="Istekli paketi" value={String(expiredCount)}       sub="za obnovu naplate"      gradient="from-rose-400 to-red-600" />
      </div>

      {/* ── Organizations ───────────────────────────────────────────────────── */}
      <SectionCard
        icon={<Building2 className="w-5 h-5 text-honey-600 dark:text-honey-400" />}
        title="Organizacije"
        count={organizations.length}
        shownCount={visibleOrgs.length}
        filtersActive={orgFiltersActive}
        query={orgQuery}
        onQuery={setOrgQuery}
        searchPlaceholder="Naziv, vlasnik, e-pošta…"
        onAdd={() => navigate('/admin/organizations/new')}
        addLabel="Dodaj organizaciju"
        filters={
          <>
            <FilterSelect value={planFilter} onChange={v => setPlanFilter(v as PlanFilter)} label="Paket">
              <option value="all">Svi paketi</option>
              {Object.values(PlanType).filter(v => typeof v === 'number').map(v => (
                <option key={v} value={String(v)}>{PlanTypeLabels[v as PlanType]}</option>
              ))}
              <option value="expired">⏰ Istekao paket</option>
            </FilterSelect>
            <FilterSelect value={activityFilter} onChange={v => setActivityFilter(v as ActivityFilter)} label="Aktivnost">
              <option value="all">Sva aktivnost</option>
              <option value="active">Aktivne (do 30 dana)</option>
              <option value="dormant">Uspavane (30–90 dana)</option>
              <option value="inactive">Neaktivne (90+ dana)</option>
              <option value="never">Nikad aktivne</option>
            </FilterSelect>
          </>
        }
      >
        {orgsLoading ? (
          <SpinnerRow />
        ) : orgsError ? (
          <ErrorState message="Greška pri učitavanju organizacija." onRetry={refetchOrgs} />
        ) : organizations.length === 0 ? (
          <EmptyRow icon={<Building2 className="w-8 h-8 text-honey-300 dark:text-honey-500/40 mx-auto mb-2" />} text="Nema organizacija." />
        ) : visibleOrgs.length === 0 ? (
          <NoMatchRow />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-honey-50 dark:bg-slate-800/60 border-y border-honey-100 dark:border-slate-800">
                <tr>
                  <SortableTh sort={orgSort} sortKey="name" natural="asc">Naziv</SortableTh>
                  <SortableTh sort={orgSort} sortKey="owner" natural="asc" className="hidden lg:table-cell">Vlasnik</SortableTh>
                  <SortableTh sort={orgSort} sortKey="plan" natural="asc" align="center">Paket</SortableTh>
                  <SortableTh sort={orgSort} sortKey="users" natural="desc" align="center">Korisnici</SortableTh>
                  <SortableTh sort={orgSort} sortKey="apiaries" natural="desc" align="center" className="hidden sm:table-cell">Pčelinjaci</SortableTh>
                  <SortableTh sort={orgSort} sortKey="beehives" natural="desc" align="center" className="hidden sm:table-cell">Košnice</SortableTh>
                  <SortableTh sort={orgSort} sortKey="activity" natural="desc" align="center">Zadnja aktivnost</SortableTh>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-honey-50 dark:divide-slate-800">
                {visibleOrgs.map((org) => (
                  <tr key={org.id} className="hover:bg-honey-50/50 dark:hover:bg-slate-800/50 transition-colors">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2.5 min-w-0">
                        <OrgAvatar org={org} />
                        <div className="min-w-0">
                          <p className="font-medium text-gray-900 dark:text-slate-100 truncate">{org.name}</p>
                          {/* The owner has no column of its own on a narrow screen — it lives here instead. */}
                          {org.ownerName && (
                            <p className="lg:hidden text-xs text-gray-500 dark:text-slate-400 truncate">{org.ownerName}</p>
                          )}
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3 hidden lg:table-cell"><OwnerCell org={org} /></td>
                    <td className="px-4 py-3 text-center"><PlanBadge org={org} /></td>
                    <td className="px-4 py-3 text-center text-gray-700 dark:text-slate-300">{org.userCount}</td>
                    <td className="px-4 py-3 text-center text-gray-700 dark:text-slate-300 hidden sm:table-cell">{org.apiaryCount}</td>
                    <td className="px-4 py-3 text-center text-gray-700 dark:text-slate-300 hidden sm:table-cell">{org.beehiveCount}</td>
                    <td className="px-4 py-3 text-center"><ActivityCell at={org.lastActivityAt} /></td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-1">
                        <RowAction kind="edit" onClick={() => navigate(`/admin/organizations/${org.id}/edit`)} />
                        <RowAction
                          kind="delete"
                          onClick={() => setConfirmTarget({ kind: 'org', id: org.id, name: org.name })}
                        />
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SectionCard>

      {/* ── Users ────────────────────────────────────────────────────────────── */}
      <SectionCard
        icon={<Users className="w-5 h-5 text-honey-600 dark:text-honey-400" />}
        title="Korisnici"
        count={users.length}
        shownCount={visibleUsers.length}
        filtersActive={userFiltersActive}
        query={userQuery}
        onQuery={setUserQuery}
        searchPlaceholder="Ime, e-pošta, telefon…"
        onAdd={() => navigate('/admin/users/new')}
        addLabel="Dodaj korisnika"
        filters={
          <>
            <FilterSelect value={roleFilter} onChange={setRoleFilter} label="Uloga">
              <option value="all">Sve uloge</option>
              {Object.entries(ROLE_LABELS).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </FilterSelect>
            <FilterSelect value={userStatusFilter} onChange={v => setUserStatusFilter(v as UserStatusFilter)} label="Status">
              <option value="all">Svi računi</option>
              <option value="unverified">Nepotvrđena e-pošta</option>
              <option value="never">Nikad se nije prijavio</option>
              <option value="idle">Bez prijave 90+ dana</option>
            </FilterSelect>
          </>
        }
      >
        {usersLoading ? (
          <SpinnerRow />
        ) : usersError ? (
          <ErrorState message="Greška pri učitavanju korisnika." onRetry={refetchUsers} />
        ) : users.length === 0 ? (
          <EmptyRow icon={<Users className="w-8 h-8 text-honey-300 dark:text-honey-500/40 mx-auto mb-2" />} text="Nema korisnika." />
        ) : visibleUsers.length === 0 ? (
          <NoMatchRow />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-honey-50 dark:bg-slate-800/60 border-y border-honey-100 dark:border-slate-800">
                <tr>
                  <SortableTh sort={userSort} sortKey="name" natural="asc">Ime</SortableTh>
                  <SortableTh sort={userSort} sortKey="email" natural="asc" className="hidden sm:table-cell">Kontakt</SortableTh>
                  <SortableTh sort={userSort} sortKey="role" natural="asc">Uloga</SortableTh>
                  <SortableTh sort={userSort} sortKey="organization" natural="asc" className="hidden md:table-cell">Organizacija</SortableTh>
                  <SortableTh sort={userSort} sortKey="lastLogin" natural="desc" align="center" className="hidden md:table-cell">Zadnja prijava</SortableTh>
                  <SortableTh sort={userSort} sortKey="createdAt" natural="desc" align="center" className="hidden xl:table-cell">Registrovan</SortableTh>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-honey-50 dark:divide-slate-800">
                {visibleUsers.map((user) => (
                  <tr key={user.id} className="hover:bg-honey-50/50 dark:hover:bg-slate-800/50 transition-colors">
                    <td className="px-4 py-3 font-medium text-gray-900 dark:text-slate-100">
                      {user.firstName} {user.lastName}
                    </td>
                    <td className="px-4 py-3 hidden sm:table-cell"><ContactCell user={user} /></td>
                    <td className="px-4 py-3"><RoleBadge role={user.role} /></td>
                    <td className="px-4 py-3 text-gray-500 dark:text-slate-400 hidden md:table-cell">
                      {user.organizationName ?? '—'}
                      {user.apiaryName && (
                        <span className="ml-1 text-xs text-honey-600 dark:text-honey-400">· {user.apiaryName}</span>
                      )}
                      {user.role === 'Beekeeper' && (
                        <span className="ml-1 text-xs text-gray-400 dark:text-slate-500">
                          · {user.assignedBeehiveIds.length} košnica
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-center hidden md:table-cell">
                      <LastLoginCell at={user.lastLoginAt} />
                    </td>
                    <td className="px-4 py-3 text-center hidden xl:table-cell text-[11px] text-gray-500 dark:text-slate-400">
                      {format(new Date(user.createdAt), 'dd.MM.yyyy.')}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-1">
                        <RowAction kind="edit" onClick={() => navigate(`/admin/users/${user.id}/edit`)} />
                        <RowAction
                          kind="delete"
                          onClick={() => setConfirmTarget({ kind: 'user', id: user.id, name: `${user.firstName} ${user.lastName}` })}
                        />
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SectionCard>

      <ConfirmDialog
        isOpen={!!confirmTarget}
        title={`Obriši ${confirmTarget?.kind === 'org' ? 'organizaciju' : 'korisnika'}`}
        message={`Obrisati "${confirmTarget?.name}"? Ova radnja se ne može poništiti.`}
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmTarget(null)}
        isLoading={isDeleting}
      />
    </div>
  )
}

// ── Section card wrapper ─────────────────────────────────────────────────────────

function SectionCard({
  icon, title, count, shownCount, filtersActive, query, onQuery, searchPlaceholder,
  onAdd, addLabel, filters, children,
}: {
  icon: React.ReactNode
  title: string
  /** Everything the server returned. */
  count: number
  /** What survived search + filters — shown next to the total so a filter is never invisible. */
  shownCount: number
  filtersActive: boolean
  query: string
  onQuery: (v: string) => void
  searchPlaceholder: string
  onAdd: () => void
  addLabel: string
  filters?: React.ReactNode
  children: React.ReactNode
}) {
  return (
    <section className="rounded-2xl border border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-card dark:shadow-none overflow-hidden">
      <div className="flex flex-col lg:flex-row lg:items-center gap-3 p-4">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-gray-800 dark:text-slate-100">
          {icon}
          {title}
          {count > 0 && (
            <span className="badge bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300 text-xs">
              {filtersActive ? `${shownCount} / ${count}` : count}
            </span>
          )}
        </h2>
        <div className="flex items-start gap-2 lg:items-center lg:ml-auto">
          {count > 0 && (
            <div className="flex flex-1 flex-wrap items-center gap-2 lg:flex-none">
              {filters}
              <div className="relative flex-1 min-w-[10rem] lg:flex-none">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-slate-500 pointer-events-none" />
                <input
                  value={query}
                  onChange={e => onQuery(e.target.value)}
                  placeholder={searchPlaceholder}
                  className="form-input pl-9 py-2 text-sm w-full lg:w-56"
                />
              </div>
            </div>
          )}
          <button
            onClick={onAdd}
            className="flex items-center gap-1.5 px-3 py-2 bg-honey-500 hover:bg-honey-600 text-white text-sm font-medium rounded-xl transition-colors shrink-0 shadow-honey dark:shadow-none"
          >
            <Plus className="w-4 h-4" />
            <span className="hidden sm:inline">{addLabel}</span>
          </button>
        </div>
      </div>
      {children}
    </section>
  )
}

// ── Small building blocks ────────────────────────────────────────────────────────

function Th({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <th className={`text-left px-4 py-3 font-medium text-gray-600 dark:text-slate-300 ${className}`}>{children}</th>
}

function FilterSelect({ value, onChange, label, children }: {
  value: string
  onChange: (v: string) => void
  label: string
  children: React.ReactNode
}) {
  return (
    <select
      aria-label={label}
      value={value}
      onChange={e => onChange(e.target.value)}
      className="form-input py-2 text-sm flex-1 min-w-[8.5rem] lg:w-auto lg:flex-none"
    >
      {children}
    </select>
  )
}

/** A column header that sorts. `natural` is the direction the column starts in when first picked. */
function SortableTh<K extends string>({
  sort, sortKey, natural, align = 'left', className = '', children,
}: {
  sort: ReturnType<typeof useSort<K>>
  sortKey: K
  natural: SortDir
  align?: 'left' | 'center'
  className?: string
  children: React.ReactNode
}) {
  const active = sort.key === sortKey
  const Icon = !active ? ChevronsUpDown : sort.dir === 'asc' ? ArrowUp : ArrowDown

  return (
    <Th className={className}>
      <button
        type="button"
        onClick={() => sort.toggle(sortKey, natural)}
        aria-label={`Sortiraj po: ${String(children)}`}
        className={`group inline-flex items-center gap-1 ${align === 'center' ? 'justify-center w-full' : ''}
          ${active ? 'text-honey-700 dark:text-honey-300' : 'hover:text-gray-800 dark:hover:text-slate-100'} transition-colors`}
      >
        {children}
        <Icon className={`w-3 h-3 shrink-0 ${active ? 'opacity-100' : 'opacity-0 group-hover:opacity-50'} transition-opacity`} />
      </button>
    </Th>
  )
}

/** The organization's logo, or its initial. Only orgs the list says have one are fetched. */
function OrgAvatar({ org }: { org: AdminOrganization }) {
  const { data: url } = useAdminOrganizationLogo(org.id, org.hasLogo)

  if (url) {
    return (
      <img
        src={url}
        alt=""
        className="w-8 h-8 shrink-0 rounded-lg object-contain bg-white dark:bg-slate-800 border border-honey-100 dark:border-slate-700"
      />
    )
  }
  return (
    <span
      aria-hidden="true"
      className="w-8 h-8 shrink-0 rounded-lg flex items-center justify-center text-xs font-bold
                 bg-honey-100 text-honey-700 dark:bg-honey-500/20 dark:text-honey-300"
    >
      {org.name[0]?.toUpperCase() ?? '?'}
    </span>
  )
}

/**
 * Who to call about this organization. Manual billing and support both start with finding this
 * person, which used to mean scrolling the users table for a matching organization name.
 */
function OwnerCell({ org }: { org: AdminOrganization }) {
  if (!org.ownerName) {
    return <span className="text-[11px] font-semibold text-red-600 dark:text-red-400">bez admina</span>
  }

  return (
    <div className="min-w-0">
      <p className="text-gray-700 dark:text-slate-300 truncate">
        {org.ownerName}
        {org.orgAdminCount > 1 && (
          <span className="ml-1 text-[10px] text-gray-400 dark:text-slate-500">+{org.orgAdminCount - 1}</span>
        )}
      </p>
      <p className="text-xs text-gray-500 dark:text-slate-400 truncate">
        {org.ownerEmail && <a href={`mailto:${org.ownerEmail}`} className="hover:text-honey-600 dark:hover:text-honey-400">{org.ownerEmail}</a>}
        {org.ownerPhone && (
          <>
            {org.ownerEmail && ' · '}
            <a href={`tel:${org.ownerPhone}`} className="hover:text-honey-600 dark:hover:text-honey-400">{org.ownerPhone}</a>
          </>
        )}
      </p>
    </div>
  )
}

/** Address + phone, with whether the address was ever confirmed. */
function ContactCell({ user }: { user: AdminUser }) {
  const verified = !!user.emailVerifiedAt
  const Icon = verified ? MailCheck : MailWarning

  return (
    <div className="min-w-0">
      <p className="flex items-center gap-1.5 text-gray-500 dark:text-slate-400 truncate">
        <Icon
          className={`w-3.5 h-3.5 shrink-0 ${verified ? 'text-emerald-500' : 'text-amber-500'}`}
          aria-label={verified ? 'E-pošta potvrđena' : 'E-pošta nije potvrđena'}
        />
        <span className="truncate">{user.email}</span>
      </p>
      {user.phone && <p className="text-xs text-gray-400 dark:text-slate-500 truncate pl-5">{user.phone}</p>}
    </div>
  )
}

function RoleBadge({ role }: { role: string }) {
  const className =
    role === 'SystemAdmin'
      ? 'bg-purple-100 text-purple-700 dark:bg-purple-500/15 dark:text-purple-300'
      : role === 'OrganizationAdmin'
      ? 'bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300'
      : role === 'Beekeeper'
      ? 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300'
      : 'bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300'

  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${className}`}>
      {ROLE_LABELS[role] ?? role}
    </span>
  )
}

/** Plan chip for the admin org table (SPEC-09). Marks an expired plan so billing follow-up is visible. */
function PlanBadge({ org }: { org: AdminOrganization }) {
  const expired = isPlanExpired(org)
  return (
    <span className="inline-flex flex-col items-center" title={org.planNotes ?? undefined}>
      <span className={`text-[11px] font-semibold px-2 py-0.5 rounded-full ${
        expired
          ? 'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300'
          : 'bg-honey-100 text-honey-700 dark:bg-honey-500/20 dark:text-honey-300'
      }`}>
        {PlanTypeLabels[org.plan]}
      </span>
      {org.planValidUntil && (
        <span className={`text-[10px] mt-0.5 ${expired ? 'text-red-500' : 'text-gray-400 dark:text-slate-500'}`}>
          {expired ? 'isteklo' : `do ${new Date(org.planValidUntil).toLocaleDateString('bs-BA')}`}
        </span>
      )}
    </span>
  )
}

/**
 * "Koristi li se ova organizacija?" — the server derives the moment from the org's own records and
 * sign-ins, this only colours it. 30/90 days: 90 is the dormancy threshold agreed in SPEC-16 §0 D1,
 * and 30 splits "working normally" off from "worth a look" so the two never share a colour.
 *
 * Reads as a label, not a verdict: an organization is never *declared* abandoned here — deciding
 * that is a person looking at the date, which is exactly what SPEC-16 D2 reserves for a human.
 */
function ActivityCell({ at }: { at?: string | null }) {
  if (!at) {
    return <span className="text-[11px] font-semibold text-red-600 dark:text-red-400">nikad</span>
  }

  const date = new Date(at)
  const days = differenceInDays(new Date(), date)
  const tone =
    days <= 30 ? 'text-emerald-600 dark:text-emerald-400'
    : days <= 90 ? 'text-amber-600 dark:text-amber-400'
    : 'text-red-600 dark:text-red-400'

  return (
    <span className="inline-flex flex-col items-center">
      <span className={`text-[11px] font-semibold ${tone}`}>
        {formatDistanceToNow(date, { addSuffix: true, locale: bs })}
      </span>
      <span className="text-[10px] mt-0.5 text-gray-400 dark:text-slate-500">
        {format(date, 'dd.MM.yyyy.')}
      </span>
    </span>
  )
}

/**
 * Newest issued session — a sign-in or a token refresh, so it answers "is anyone still using this
 * account?". Coloured on the same 30/90-day scale as organization activity, so the two tables can
 * be read with one set of eyes.
 */
function LastLoginCell({ at }: { at?: string | null }) {
  if (!at) {
    return <span className="text-[11px] font-semibold text-red-600 dark:text-red-400">nikad</span>
  }

  const date = new Date(at)
  const days = differenceInDays(new Date(), date)
  const tone =
    days <= 30 ? 'text-emerald-600 dark:text-emerald-400'
    : days <= 90 ? 'text-amber-600 dark:text-amber-400'
    : 'text-red-600 dark:text-red-400'

  return (
    <span className={`text-[11px] font-semibold ${tone}`} title={format(date, 'dd.MM.yyyy. HH:mm')}>
      {formatDistanceToNow(date, { addSuffix: true, locale: bs })}
    </span>
  )
}

function RowAction({ kind, onClick, loading }: { kind: 'edit' | 'delete'; onClick: () => void; loading?: boolean }) {
  if (kind === 'edit') {
    return (
      <button
        onClick={onClick}
        className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
        title="Uredi"
      >
        <Pencil className="w-4 h-4" />
      </button>
    )
  }
  return (
    <button
      onClick={onClick}
      disabled={loading}
      className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-red-600 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors disabled:opacity-50"
      title="Obriši"
    >
      {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
    </button>
  )
}


function SpinnerRow() {
  return (
    <div className="p-4 space-y-2 border-t border-honey-100 dark:border-slate-800">
      {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-11 rounded-lg" />)}
    </div>
  )
}

function EmptyRow({ icon, text }: { icon: React.ReactNode; text: string }) {
  return (
    <div className="text-center py-12 border-t border-honey-100 dark:border-slate-800">
      {icon}
      <p className="text-sm text-gray-500 dark:text-slate-400">{text}</p>
    </div>
  )
}

function NoMatchRow() {
  return (
    <div className="text-center py-12 border-t border-honey-100 dark:border-slate-800">
      <Search className="w-7 h-7 text-honey-300 dark:text-honey-500/40 mx-auto mb-2" />
      <p className="text-sm text-gray-500 dark:text-slate-400">Nema rezultata za trenutnu pretragu i filtere.</p>
    </div>
  )
}

// ── Vitals KPI tile ────────────────────────────────────────────────────────────

/* VitalCard now lives in shared/components (with count-up animation). */
