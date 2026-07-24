import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Users, Pencil, AlertCircle, Search, UserPlus, X, Loader2, Eye, EyeOff } from 'lucide-react'
import { useOrgMembers, useAvailableApiaries, useAvailableBeehives, useCreateOrgMember } from '../../core/services/orgQueries'
import { useAuth } from '../../core/context/AuthContext'
import { VitalCard, VitalsSkeleton } from '../../shared/components'

interface AddMemberForm {
  firstName: string
  lastName: string
  email: string
  password: string
  role: 'ApiaryAdmin' | 'Beekeeper'
  apiaryId: string
}

const EMPTY_FORM: AddMemberForm = {
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  role: 'Beekeeper',
  apiaryId: '',
}

export default function MembersPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const { data: members = [], isLoading, error } = useOrgMembers()
  const [query, setQuery] = useState('')

  const isOrgAdmin = user?.role === 'OrganizationAdmin'

  // ── Add member modal state ─────────────────────────────────────────────────
  const [modalOpen, setModalOpen] = useState(false)
  const [form, setForm] = useState<AddMemberForm>(EMPTY_FORM)
  const [selectedBeehiveIds, setSelectedBeehiveIds] = useState<number[]>([])
  const [showPassword, setShowPassword] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const { data: availableApiaries = [] } = useAvailableApiaries(isOrgAdmin && modalOpen)
  const { data: availableBeehives = [] } = useAvailableBeehives()
  const createMember = useCreateOrgMember()

  // Filter beehives to only show those from the org (all available for OrgAdmin)
  const beehivesForRole = availableBeehives

  function openModal() {
    setForm(EMPTY_FORM)
    setSelectedBeehiveIds([])
    setShowPassword(false)
    setFormError(null)
    setModalOpen(true)
  }

  function closeModal() {
    setModalOpen(false)
    setFormError(null)
  }

  // Close modal on Escape
  useEffect(() => {
    if (!modalOpen) return
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') closeModal() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [modalOpen])

  function toggleBeehive(id: number) {
    setSelectedBeehiveIds(prev =>
      prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
    )
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    if (!form.firstName.trim() || !form.lastName.trim()) {
      setFormError('Ime i prezime su obavezni.')
      return
    }
    if (!form.email.trim() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
      setFormError('Unesite valjanu e-poštu.')
      return
    }
    if (form.password.length < 6) {
      setFormError('Lozinka mora imati najmanje 6 znakova.')
      return
    }
    if (form.role === 'ApiaryAdmin' && !form.apiaryId) {
      setFormError('Odaberite pčelinjak za Admin korisnika.')
      return
    }

    try {
      await createMember.mutateAsync({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        password: form.password,
        role: form.role,
        apiaryId: form.role === 'ApiaryAdmin' && form.apiaryId ? parseInt(form.apiaryId) : null,
        assignedBeehiveIds: form.role === 'Beekeeper' ? selectedBeehiveIds : [],
      })
      closeModal()
    } catch (e: any) {
      setFormError(e?.response?.data?.detail ?? e?.message ?? 'Greška pri dodavanju člana.')
    }
  }

  // ── Derived vitals ──────────────────────────────────────────────────────────
  const adminCount = members.filter(m => m.role === 'ApiaryAdmin').length
  const userCount  = members.filter(m => m.role === 'Beekeeper').length
  const assignedCount = members.filter(m =>
    m.role === 'ApiaryAdmin' ? !!m.apiaryName : m.assignedBeehiveNames.length > 0,
  ).length

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return members
    return members.filter(m =>
      `${m.firstName} ${m.lastName}`.toLowerCase().includes(q) ||
      m.email.toLowerCase().includes(q) ||
      m.role.toLowerCase().includes(q),
    )
  }, [members, query])

  const roleLabel = (role: string) =>
    role === 'ApiaryAdmin' ? 'Admin' : 'Korisnik'

  return (
    <div className="animate-fade-in space-y-6">

      {/* ── Hero ──────────────────────────────────────────────────────────────── */}
      <div className="relative overflow-hidden rounded-3xl border border-honey-200 dark:border-slate-800
                      bg-gradient-to-br from-honey-100 via-white to-honey-50
                      dark:from-slate-900 dark:via-slate-900 dark:to-slate-950 shadow-card dark:shadow-none">
        <div className="absolute inset-0 bg-honeycomb opacity-60 dark:opacity-100 pointer-events-none" />
        <div className="relative p-5 sm:p-7 flex items-center gap-4">
          <div className="w-14 h-14 shrink-0 rounded-2xl bg-white/70 dark:bg-slate-800 border border-honey-200 dark:border-slate-700 flex items-center justify-center text-3xl shadow-honey dark:shadow-none">
            👥
          </div>
          <div className="min-w-0 flex-1">
            <h1 className="font-display text-2xl sm:text-3xl font-bold text-gray-900 dark:text-slate-50">Članovi</h1>
            <p className="mt-0.5 text-sm text-gray-600 dark:text-slate-400">
              {isOrgAdmin
                ? 'Dodijelite pčelinjake Adminima i košnice Korisnicima u vašoj organizaciji.'
                : 'Dodijelite košnice iz vašeg pčelinjaka Korisnicima u vašoj organizaciji.'}
            </p>
          </div>
          {isOrgAdmin && (
            <button
              onClick={openModal}
              className="shrink-0 flex items-center gap-2 px-4 py-2.5 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold shadow-honey transition-colors"
            >
              <UserPlus className="w-4 h-4" />
              <span className="hidden sm:inline">Dodaj člana</span>
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="flex items-start gap-2 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-lg px-4 py-3 text-sm">
          <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
          Greška pri učitavanju članova.
        </div>
      )}

      {isLoading ? (
        <VitalsSkeleton />
      ) : members.length === 0 ? (
        <div className="text-center py-16 bg-white dark:bg-slate-900 rounded-2xl border border-dashed border-honey-200 dark:border-slate-700">
          <Users className="w-8 h-8 text-honey-300 dark:text-honey-500/50 mx-auto mb-2" />
          <p className="text-sm text-gray-500 dark:text-slate-400">Nema članova za dodjeljivanje u vašoj organizaciji.</p>
          {isOrgAdmin && (
            <button
              onClick={openModal}
              className="mt-4 flex items-center gap-2 mx-auto px-4 py-2 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold transition-colors"
            >
              <UserPlus className="w-4 h-4" />
              Dodaj prvog člana
            </button>
          )}
        </div>
      ) : (
        <>
          {/* ── Vitals strip ──────────────────────────────────────────────── */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 stagger">
            <VitalCard icon="👥" label="Članovi"   value={String(members.length)} sub="u vašoj org"       gradient="from-honey-400 to-honey-600" />
            <VitalCard icon="🛡️" label="Admini"    value={String(adminCount)}     sub="pčelinjak admini"  gradient="from-amber-400 to-orange-500" />
            <VitalCard icon="🧑‍🌾" label="Korisnici" value={String(userCount)}      sub="terenci"           gradient="from-sky-400 to-blue-600" />
            <VitalCard icon="🔗" label="Dodijeljeni" value={String(assignedCount)} sub="imaju raspored"   gradient="from-violet-400 to-indigo-600" />
          </div>

          {/* ── Members table ─────────────────────────────────────────────── */}
          <section className="rounded-2xl border border-honey-100 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-card dark:shadow-none overflow-hidden">
            <div className="flex flex-col sm:flex-row sm:items-center gap-3 p-4">
              <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-gray-800 dark:text-slate-100">
                <Users className="w-5 h-5 text-honey-600 dark:text-honey-400" />
                Tim
                <span className="badge bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300 text-xs">{members.length}</span>
              </h2>
              <div className="relative sm:ml-auto sm:w-56">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 dark:text-slate-500 pointer-events-none" />
                <input
                  value={query}
                  onChange={e => setQuery(e.target.value)}
                  placeholder="Pretraži članove…"
                  className="form-input pl-9 py-2 text-sm w-full"
                />
              </div>
            </div>

            {filtered.length === 0 ? (
              <div className="text-center py-12 border-t border-honey-100 dark:border-slate-800">
                <Search className="w-7 h-7 text-honey-300 dark:text-honey-500/40 mx-auto mb-2" />
                <p className="text-sm text-gray-500 dark:text-slate-400">Nema članova koji odgovaraju "{query}".</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-honey-50 dark:bg-slate-800/60 border-y border-honey-100 dark:border-slate-800">
                    <tr>
                      <th className="text-left px-4 py-3 font-medium text-gray-600 dark:text-slate-300">Ime</th>
                      <th className="text-left px-4 py-3 font-medium text-gray-600 dark:text-slate-300 hidden sm:table-cell">E-pošta</th>
                      <th className="text-left px-4 py-3 font-medium text-gray-600 dark:text-slate-300">Uloga</th>
                      <th className="text-left px-4 py-3 font-medium text-gray-600 dark:text-slate-300 hidden md:table-cell">Dodjele</th>
                      <th className="px-4 py-3" />
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-honey-50 dark:divide-slate-800">
                    {filtered.map((member) => (
                      <tr key={member.id} className="hover:bg-honey-50/50 dark:hover:bg-slate-800/50 transition-colors">
                        <td className="px-4 py-3 font-medium text-gray-900 dark:text-slate-100">
                          {member.firstName} {member.lastName}
                        </td>
                        <td className="px-4 py-3 text-gray-500 dark:text-slate-400 hidden sm:table-cell">{member.email}</td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium
                            ${member.role === 'ApiaryAdmin' ? 'bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300' : 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300'}`}>
                            {roleLabel(member.role)}
                          </span>
                        </td>
                        <td className="px-4 py-3 hidden md:table-cell">
                          {member.role === 'ApiaryAdmin' ? (
                            <span className="text-gray-500 dark:text-slate-400 text-xs">
                              {member.apiaryName
                                ? <span className="text-honey-700 dark:text-honey-400 font-medium">{member.apiaryName}</span>
                                : <span className="text-gray-400 dark:text-slate-500 italic">Nema pčelinjaka</span>}
                            </span>
                          ) : (
                            <span className="text-gray-500 dark:text-slate-400 text-xs">
                              {member.assignedBeehiveNames.length > 0
                                ? member.assignedBeehiveNames.join(', ')
                                : <span className="text-gray-400 dark:text-slate-500 italic">Nema košnica</span>}
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <button
                            onClick={() => navigate(`/members/${member.id}/assignments`)}
                            className="p-1.5 rounded-lg text-gray-400 dark:text-slate-500 hover:text-honey-600 dark:hover:text-honey-400 hover:bg-honey-50 dark:hover:bg-slate-800 transition-colors"
                            title="Uredi dodjele"
                          >
                            <Pencil className="w-4 h-4" />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}

      {/* ── Add Member Modal ───────────────────────────────────────────────────── */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={closeModal} />
          <div className="relative bg-white dark:bg-slate-900 dark:border dark:border-slate-800 rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto animate-slide-up">
            {/* Header */}
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-slate-800">
              <div className="flex items-center gap-2">
                <UserPlus className="w-5 h-5 text-honey-500" />
                <h2 className="font-display text-lg font-semibold text-gray-800 dark:text-slate-100">Dodaj člana</h2>
              </div>
              <button
                onClick={closeModal}
                className="text-gray-400 hover:text-gray-600 dark:hover:text-slate-200 transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Form */}
            <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">
              {formError && (
                <div className="flex items-start gap-2 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm">
                  <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
                  {formError}
                </div>
              )}

              {/* Name row */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
                    Ime <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    placeholder="Ime"
                    value={form.firstName}
                    onChange={e => setForm(f => ({ ...f, firstName: e.target.value }))}
                    className="form-input w-full"
                    required
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
                    Prezime <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    placeholder="Prezime"
                    value={form.lastName}
                    onChange={e => setForm(f => ({ ...f, lastName: e.target.value }))}
                    className="form-input w-full"
                    required
                  />
                </div>
              </div>

              {/* Email */}
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
                  E-pošta <span className="text-red-500">*</span>
                </label>
                <input
                  type="email"
                  placeholder="korisnik@primjer.com"
                  value={form.email}
                  onChange={e => setForm(f => ({ ...f, email: e.target.value }))}
                  className="form-input w-full"
                  required
                />
              </div>

              {/* Password */}
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
                  Lozinka <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <input
                    type={showPassword ? 'text' : 'password'}
                    placeholder="••••••••"
                    value={form.password}
                    onChange={e => setForm(f => ({ ...f, password: e.target.value }))}
                    className="form-input w-full pr-10"
                    required
                    minLength={6}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(v => !v)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 dark:text-slate-500 hover:text-gray-600 dark:hover:text-slate-300 transition-colors"
                    tabIndex={-1}
                    aria-label={showPassword ? 'Sakrij lozinku' : 'Prikaži lozinku'}
                  >
                    {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
                <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">Minimum 6 znakova</p>
              </div>

              {/* Role */}
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
                  Uloga <span className="text-red-500">*</span>
                </label>
                <div className="grid grid-cols-2 gap-2">
                  {(['ApiaryAdmin', 'Beekeeper'] as const).map(r => (
                    <button
                      key={r}
                      type="button"
                      onClick={() => {
                        setForm(f => ({ ...f, role: r, apiaryId: '' }))
                        setSelectedBeehiveIds([])
                      }}
                      className={`px-3 py-2.5 rounded-xl border text-sm font-medium transition-colors text-left ${
                        form.role === r
                          ? 'border-honey-400 bg-honey-50 dark:bg-honey-500/10 text-honey-700 dark:text-honey-300'
                          : 'border-gray-200 dark:border-slate-700 text-gray-600 dark:text-slate-300 hover:bg-gray-50 dark:hover:bg-slate-800'
                      }`}
                    >
                      <div className="font-semibold">{r === 'ApiaryAdmin' ? '🛡️ Admin' : '🧑‍🌾 Korisnik'}</div>
                      <div className="text-xs text-gray-400 dark:text-slate-500 mt-0.5">
                        {r === 'ApiaryAdmin' ? 'Upravlja pčelinjak' : 'Terener'}
                      </div>
                    </button>
                  ))}
                </div>
              </div>

              {/* Apiary selection for ApiaryAdmin */}
              {form.role === 'ApiaryAdmin' && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
                    Pčelinjak <span className="text-red-500">*</span>
                  </label>
                  <select
                    value={form.apiaryId}
                    onChange={e => setForm(f => ({ ...f, apiaryId: e.target.value }))}
                    className="form-input w-full"
                    required
                  >
                    <option value="">Odaberite pčelinjak…</option>
                    {availableApiaries.map(a => (
                      <option key={a.id} value={a.id}>{a.name}</option>
                    ))}
                  </select>
                  {availableApiaries.length === 0 && (
                    <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">Nema dostupnih pčelinjaka.</p>
                  )}
                </div>
              )}

              {/* Beehive selection for Beekeeper */}
              {form.role === 'Beekeeper' && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
                    Dodijeljene košnice
                  </label>
                  {beehivesForRole.length === 0 ? (
                    <p className="text-sm text-gray-400 dark:text-slate-500 py-2">
                      Nema dostupnih košnica za dodjeljivanje.
                    </p>
                  ) : (
                    <div className="border border-gray-200 dark:border-slate-700 rounded-xl divide-y divide-gray-100 dark:divide-slate-800 max-h-44 overflow-y-auto">
                      {beehivesForRole.map(b => (
                        <label
                          key={b.id}
                          className="flex items-center gap-3 px-4 py-2 cursor-pointer hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
                        >
                          <input
                            type="checkbox"
                            checked={selectedBeehiveIds.includes(b.id)}
                            onChange={() => toggleBeehive(b.id)}
                            className="w-4 h-4 accent-honey-500"
                          />
                          <span className="text-sm text-gray-800 dark:text-slate-200">{b.name}</span>
                          <span className="text-xs text-gray-400 dark:text-slate-500 ml-auto">{b.apiaryName}</span>
                        </label>
                      ))}
                    </div>
                  )}
                  <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">
                    {selectedBeehiveIds.length > 0
                      ? `${selectedBeehiveIds.length} ${selectedBeehiveIds.length === 1 ? 'košnica odabrana' : 'košnica odabrano'}`
                      : 'Nema dodijeljenih košnica'}
                  </p>
                </div>
              )}

              {/* Buttons */}
              <div className="flex gap-3 pt-2">
                <button
                  type="button"
                  onClick={closeModal}
                  className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
                >
                  Otkaži
                </button>
                <button
                  type="submit"
                  disabled={createMember.isPending}
                  className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors"
                >
                  {createMember.isPending && <Loader2 className="w-4 h-4 animate-spin" />}
                  Dodaj člana
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
