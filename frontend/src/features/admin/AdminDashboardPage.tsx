import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Building2, Users, Plus, Pencil, Trash2, Loader2, Search } from 'lucide-react'
import {
  useAdminOrganizations,
  useAdminUsers,
  useDeleteOrganization,
  useDeleteAdminUser,
} from '../../core/services/adminQueries'
import { VitalCard, Skeleton, ConfirmDialog } from '../../shared/components'
import { useToast } from '../../core/context/ToastContext'
import { PlanTypeLabels, type AdminOrganization } from '../../core/models'

export default function AdminDashboardPage() {
  const navigate = useNavigate()
  const { data: organizations = [], isLoading: orgsLoading } = useAdminOrganizations()
  const { data: users = [], isLoading: usersLoading } = useAdminUsers()
  const deleteOrg = useDeleteOrganization()
  const deleteUser = useDeleteAdminUser()
  const { toast } = useToast()

  const [confirmTarget, setConfirmTarget] = useState<{ kind: 'org' | 'user'; id: number; name: string } | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const [orgQuery, setOrgQuery] = useState('')
  const [userQuery, setUserQuery] = useState('')

  // ── Derived vitals ──
  const totalApiaries = organizations.reduce((s, o) => s + o.apiaryCount, 0)
  const adminCount = users.filter(u => u.role !== 'Beekeeper').length

  // ── Filtered lists ──
  const filteredOrgs = useMemo(() => {
    const q = orgQuery.trim().toLowerCase()
    if (!q) return organizations
    return organizations.filter(o =>
      o.name.toLowerCase().includes(q) || (o.description?.toLowerCase().includes(q) ?? false),
    )
  }, [organizations, orgQuery])

  const filteredUsers = useMemo(() => {
    const q = userQuery.trim().toLowerCase()
    if (!q) return users
    return users.filter(u =>
      `${u.firstName} ${u.lastName}`.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q) ||
      (u.organizationName?.toLowerCase().includes(q) ?? false) ||
      u.role.toLowerCase().includes(q),
    )
  }, [users, userQuery])

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
          <button
            onClick={() => navigate('/admin/learning-topics')}
            className="shrink-0 flex items-center gap-1.5 px-3 py-2 rounded-xl border border-honey-200 dark:border-slate-700 bg-white/70 dark:bg-slate-800 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-honey-50 dark:hover:bg-slate-700 transition-colors"
          >
            🎓 Uredi edukaciju
          </button>
        </div>
      </div>

      {/* ── Vitals strip ──────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
        <VitalCard icon="🏢" label="Organizacije" value={String(organizations.length)} sub="na platformi"           gradient="from-honey-400 to-honey-600" />
        <VitalCard icon="👥" label="Korisnici"    value={String(users.length)}         sub="ukupno računa"          gradient="from-amber-400 to-orange-500" />
        <VitalCard icon="🏡" label="Pčelinjaci"   value={String(totalApiaries)}        sub="u svim organizacijama"  gradient="from-sky-400 to-blue-600" />
        <VitalCard icon="🛡️" label="Admini"       value={String(adminCount)}           sub="uloge s pravima"        gradient="from-violet-400 to-indigo-600" />
      </div>

      {/* ── Organizations ───────────────────────────────────────────────────── */}
      <SectionCard
        icon={<Building2 className="w-5 h-5 text-honey-600 dark:text-honey-400" />}
        title="Organizacije"
        count={organizations.length}
        query={orgQuery}
        onQuery={setOrgQuery}
        searchPlaceholder="Pretraži organizacije…"
        onAdd={() => navigate('/admin/organizations/new')}
        addLabel="Dodaj organizaciju"
      >
        {orgsLoading ? (
          <SpinnerRow />
        ) : organizations.length === 0 ? (
          <EmptyRow icon={<Building2 className="w-8 h-8 text-honey-300 dark:text-honey-500/40 mx-auto mb-2" />} text="Nema organizacija." />
        ) : filteredOrgs.length === 0 ? (
          <NoMatchRow query={orgQuery} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-honey-50 dark:bg-slate-800/60 border-y border-honey-100 dark:border-slate-800">
                <tr>
                  <Th>Naziv</Th>
                  <Th className="text-center">Paket</Th>
                  <Th className="text-center">Korisnici</Th>
                  <Th className="text-center">Pčelinjaci</Th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-honey-50 dark:divide-slate-800">
                {filteredOrgs.map((org) => (
                  <tr key={org.id} className="hover:bg-honey-50/50 dark:hover:bg-slate-800/50 transition-colors">
                    <td className="px-4 py-3 font-medium text-gray-900 dark:text-slate-100">{org.name}</td>
                    <td className="px-4 py-3 text-center">
                      <PlanBadge org={org} />
                    </td>
                    <td className="px-4 py-3 text-center text-gray-700 dark:text-slate-300">{org.userCount}</td>
                    <td className="px-4 py-3 text-center text-gray-700 dark:text-slate-300">{org.apiaryCount}</td>
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
        query={userQuery}
        onQuery={setUserQuery}
        searchPlaceholder="Pretraži korisnike…"
        onAdd={() => navigate('/admin/users/new')}
        addLabel="Dodaj korisnika"
      >
        {usersLoading ? (
          <SpinnerRow />
        ) : users.length === 0 ? (
          <EmptyRow icon={<Users className="w-8 h-8 text-honey-300 dark:text-honey-500/40 mx-auto mb-2" />} text="Nema korisnika." />
        ) : filteredUsers.length === 0 ? (
          <NoMatchRow query={userQuery} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-honey-50 dark:bg-slate-800/60 border-y border-honey-100 dark:border-slate-800">
                <tr>
                  <Th>Ime</Th>
                  <Th className="hidden sm:table-cell">E-pošta</Th>
                  <Th>Uloga</Th>
                  <Th className="hidden md:table-cell">Organizacija</Th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-honey-50 dark:divide-slate-800">
                {filteredUsers.map((user) => (
                  <tr key={user.id} className="hover:bg-honey-50/50 dark:hover:bg-slate-800/50 transition-colors">
                    <td className="px-4 py-3 font-medium text-gray-900 dark:text-slate-100">
                      {user.firstName} {user.lastName}
                    </td>
                    <td className="px-4 py-3 text-gray-500 dark:text-slate-400 hidden sm:table-cell">{user.email}</td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium
                        ${user.role === 'SystemAdmin'
                          ? 'bg-purple-100 text-purple-700 dark:bg-purple-500/15 dark:text-purple-300'
                          : user.role === 'OrganizationAdmin'
                          ? 'bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300'
                          : user.role === 'Beekeeper'
                          ? 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300'
                          : 'bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300'
                        }`}>
                        {user.role === 'SystemAdmin' ? 'Sistem Admin'
                          : user.role === 'OrganizationAdmin' ? 'Org Admin'
                          : user.role === 'Beekeeper' ? 'Korisnik'
                          : 'Admin'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-500 dark:text-slate-400 hidden md:table-cell">
                      {user.organizationName ?? '—'}
                      {user.apiaryName && (
                        <span className="ml-1 text-xs text-honey-600 dark:text-honey-400">· {user.apiaryName}</span>
                      )}
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
  icon, title, count, query, onQuery, searchPlaceholder, onAdd, addLabel, children,
}: {
  icon: React.ReactNode
  title: string
  count: number
  query: string
  onQuery: (v: string) => void
  searchPlaceholder: string
  onAdd: () => void
  addLabel: string
  children: React.ReactNode
}) {
  return (
    <section className="rounded-2xl border border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-card dark:shadow-none overflow-hidden">
      <div className="flex flex-col sm:flex-row sm:items-center gap-3 p-4">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-gray-800 dark:text-slate-100">
          {icon}
          {title}
          {count > 0 && <span className="badge bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300 text-xs">{count}</span>}
        </h2>
        <div className="flex items-center gap-2 sm:ml-auto">
          {count > 0 && (
            <div className="relative flex-1 sm:flex-none">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-slate-500 pointer-events-none" />
              <input
                value={query}
                onChange={e => onQuery(e.target.value)}
                placeholder={searchPlaceholder}
                className="form-input pl-9 py-2 text-sm sm:w-56"
              />
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

/** Plan chip for the admin org table (SPEC-09). Marks an expired plan so billing follow-up is visible. */
function PlanBadge({ org }: { org: AdminOrganization }) {
  const expired = !!org.planValidUntil && new Date(org.planValidUntil).setHours(23, 59, 59, 999) < Date.now()
  return (
    <span className="inline-flex flex-col items-center">
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

function NoMatchRow({ query }: { query: string }) {
  return (
    <div className="text-center py-12 border-t border-honey-100 dark:border-slate-800">
      <Search className="w-7 h-7 text-honey-300 dark:text-honey-500/40 mx-auto mb-2" />
      <p className="text-sm text-gray-500 dark:text-slate-400">Nema rezultata za &quot;{query}&quot;.</p>
    </div>
  )
}

// ── Vitals KPI tile ────────────────────────────────────────────────────────────

/* VitalCard now lives in shared/components (with count-up animation). */
