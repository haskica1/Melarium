import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Loader2, AlertCircle } from 'lucide-react'
import {
  useOrgMember,
  useAvailableBeehives,
  useAvailableApiaries,
  useUpdateBeehiveAssignments,
  useUpdateApiaryAssignment,
} from '../../core/services/orgQueries'
import { useAuth } from '../../core/context/AuthContext'
import { FormHeader } from '../../shared/components'

export default function MemberAssignmentPage() {
  const { id } = useParams<{ id: string }>()
  const memberId = parseInt(id ?? '0')
  const navigate = useNavigate()
  const { user } = useAuth()

  const isOrgAdmin = user?.role === 'OrganizationAdmin'

  const { data: member, isLoading: loadingMember } = useOrgMember(memberId)
  const { data: beehives = [], isLoading: loadingBeehives } = useAvailableBeehives()
  const { data: apiaries = [], isLoading: loadingApiaries } = useAvailableApiaries(isOrgAdmin)

  const updateBeehives = useUpdateBeehiveAssignments(memberId)
  const updateApiary = useUpdateApiaryAssignment(memberId)

  const [selectedBeehiveIds, setSelectedBeehiveIds] = useState<number[]>([])
  const [selectedApiaryId, setSelectedApiaryId] = useState<string>('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (member) {
      setSelectedBeehiveIds(member.assignedBeehiveIds ?? [])
      setSelectedApiaryId(member.apiaryId?.toString() ?? '')
    }
  }, [member])

  function toggleBeehive(beehiveId: number) {
    setSelectedBeehiveIds(prev =>
      prev.includes(beehiveId)
        ? prev.filter(id => id !== beehiveId)
        : [...prev, beehiveId]
    )
  }

  async function handleSaveBeehives() {
    setError(null)
    try {
      await updateBeehives.mutateAsync({ beehiveIds: selectedBeehiveIds })
      navigate('/members')
    } catch (e: any) {
      setError(e?.response?.data?.detail ?? e?.message ?? 'Greška pri ažuriranju dodjela.')
    }
  }

  async function handleSaveApiary() {
    setError(null)
    try {
      await updateApiary.mutateAsync({ apiaryId: selectedApiaryId ? parseInt(selectedApiaryId) : null })
      navigate('/members')
    } catch (e: any) {
      setError(e?.response?.data?.detail ?? e?.message ?? 'Greška pri ažuriranju dodjele pčelinjaka.')
    }
  }

  if (loadingMember) {
    return (
      <div className="flex justify-center py-20">
        <Loader2 className="w-6 h-6 animate-spin text-honey-500" />
      </div>
    )
  }

  if (!member) {
    return (
      <div className="text-center py-20 text-gray-500 dark:text-slate-400">Član nije pronađen.</div>
    )
  }

  const isUserRole = member.role === 'Beekeeper'
  const isAdminRole = member.role === 'ApiaryAdmin'
  const isSaving = updateBeehives.isPending || updateApiary.isPending

  const roleLabel = isAdminRole ? 'Admin' : 'Korisnik'

  return (
    <div className="max-w-xl mx-auto">
      <FormHeader
        icon="🔗"
        title={`${member.firstName} ${member.lastName}`}
        subtitle={member.email}
        onBack={() => navigate('/members')}
        backLabel="Nazad na Članove"
      />

      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm dark:shadow-none border border-honey-100 dark:border-slate-800 px-8 py-8 space-y-6">
        {/* Role */}
        <div>
          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium
            ${isAdminRole ? 'bg-honey-100 text-honey-700 dark:bg-honey-500/15 dark:text-honey-300' : 'bg-gray-100 text-gray-600 dark:bg-slate-700 dark:text-slate-300'}`}>
            {roleLabel}
          </span>
        </div>

        {error && (
          <div className="flex items-start gap-2 bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 text-red-700 dark:text-red-300 rounded-xl px-4 py-3 text-sm">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            {error}
          </div>
        )}

        {/* Beehive assignments — for User role */}
        {isUserRole && (
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
              Dodijeljene košnice
            </label>
            {loadingBeehives ? (
              <div className="flex justify-center py-6">
                <Loader2 className="w-5 h-5 animate-spin text-honey-400" />
              </div>
            ) : beehives.length === 0 ? (
              <p className="text-sm text-gray-400 dark:text-slate-500 py-2">
                Nema dostupnih košnica za dodjeljivanje.
              </p>
            ) : (
              <div className="border border-gray-200 dark:border-slate-700 rounded-xl divide-y divide-gray-100 dark:divide-slate-800 max-h-60 overflow-y-auto">
                {beehives.map((b) => (
                  <label
                    key={b.id}
                    className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
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
            <p className="mt-1.5 text-xs text-gray-400 dark:text-slate-500">
              {selectedBeehiveIds.length > 0
                ? `${selectedBeehiveIds.length} ${selectedBeehiveIds.length === 1 ? 'košnica odabrana' : 'košnica odabrano'}`
                : 'Nema dodijeljenih košnica'}
            </p>

            <div className="flex gap-3 pt-4">
              <button
                type="button"
                onClick={() => navigate('/members')}
                className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
              >
                Otkaži
              </button>
              <button
                onClick={handleSaveBeehives}
                disabled={isSaving}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors"
              >
                {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
                Spremi dodjele
              </button>
            </div>
          </div>
        )}

        {/* Apiary assignment — for Admin role, OrgAdmin only */}
        {isAdminRole && isOrgAdmin && (
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
              Dodijeljeni pčelinjak
            </label>
            {loadingApiaries ? (
              <div className="flex justify-center py-6">
                <Loader2 className="w-5 h-5 animate-spin text-honey-400" />
              </div>
            ) : (
              <select
                value={selectedApiaryId}
                onChange={e => setSelectedApiaryId(e.target.value)}
                className="w-full px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm outline-none bg-gray-50 focus:bg-white dark:bg-slate-800 dark:focus:bg-slate-800 dark:text-slate-100 focus:border-honey-400 focus:ring-2 focus:ring-honey-100 transition-all"
              >
                <option value="">Nema dodijeljenog pčelinjaka</option>
                {apiaries.map((a) => (
                  <option key={a.id} value={a.id}>{a.name}</option>
                ))}
              </select>
            )}

            <div className="flex gap-3 pt-4">
              <button
                type="button"
                onClick={() => navigate('/members')}
                className="flex-1 px-4 py-3 rounded-xl border border-gray-200 dark:border-slate-700 text-sm font-medium text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
              >
                Otkaži
              </button>
              <button
                onClick={handleSaveApiary}
                disabled={isSaving}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-honey-500 hover:bg-honey-600 text-white text-sm font-semibold disabled:opacity-60 transition-colors"
              >
                {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
                Spremi dodjelu
              </button>
            </div>
          </div>
        )}

        {/* Admin member but caller is also Admin (not OrgAdmin) */}
        {isAdminRole && !isOrgAdmin && (
          <div className="text-sm text-gray-500 dark:text-slate-400 bg-gray-50 dark:bg-slate-800/60 rounded-xl px-4 py-3">
            Samo administratori organizacije mogu mijenjati dodjele pčelinjaka za Admin korisnike.
          </div>
        )}
      </div>
    </div>
  )
}
