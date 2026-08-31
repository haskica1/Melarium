import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { AlertTriangle, Trash2, Users } from 'lucide-react'
import { Modal } from '../../shared/components'
import { useAuth } from '../../core/context/AuthContext'
import { errorMessage } from '../../core/services/apiClient'
import { useAccountDeletionPreview, useDeleteAccount } from '../../core/services/profileQueries'
import type { AccountDeletionPreview } from '../../core/services/profileService'

/**
 * "Opasna zona" — deleting your own account, which both app stores require to be reachable from
 * inside the app and which the web has never offered at all.
 *
 * The three outcomes are decided on the server (`/profile/deletion-preview`) and only rendered
 * here. Re-deriving them from the cached session would mean two copies of the rule, and the copy
 * that drifts is the one that shows a plain "obriši račun" dialog to someone who is about to take
 * an entire organization down with them.
 */
export default function DeleteAccountSection() {
  const [open, setOpen] = useState(false)

  return (
    <>
      <div className="card border border-red-200 dark:border-red-500/30 space-y-3">
        <div className="flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 text-red-500" />
          <h3 className="font-semibold text-gray-700 dark:text-slate-200">Brisanje računa</h3>
        </div>

        <p className="text-sm text-gray-600 dark:text-slate-400">
          Brisanje računa je trajno i ne može se poništiti. Prije nego što potvrdite, pokazaćemo vam
          tačno šta se briše.
        </p>

        <button
          type="button"
          onClick={() => setOpen(true)}
          className="inline-flex items-center gap-2 rounded-xl border border-red-300 dark:border-red-500/40
                     px-4 py-2 text-sm font-medium text-red-600 dark:text-red-300
                     hover:bg-red-50 dark:hover:bg-red-500/10 transition-colors"
        >
          <Trash2 className="w-4 h-4" />
          Obriši moj račun
        </button>
      </div>

      <DeleteAccountModal open={open} onClose={() => setOpen(false)} />
    </>
  )
}

function DeleteAccountModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const { logout } = useAuth()
  const [password, setPassword] = useState('')
  const [orgName, setOrgName] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data: preview, isPending, isError } = useAccountDeletionPreview(open)
  const remove = useDeleteAccount()

  function confirmDelete() {
    setError(null)
    remove.mutate(
      {
        password,
        organizationNameConfirmation: preview?.mode === 'organization' ? orgName : undefined,
      },
      {
        // The account is gone, so there is nothing for a refresh to recover — clear the session
        // locally instead of letting the next request 401 into the interceptor's hard logout.
        onSuccess: () => {
          logout()
          navigate('/login', { replace: true })
        },
        onError: (err) => setError(errorMessage(err)),
      },
    )
  }

  function close() {
    setPassword('')
    setOrgName('')
    setError(null)
    onClose()
  }

  const nameMatches =
    preview?.mode !== 'organization' ||
    orgName.trim() === (preview?.organizationName ?? '').trim()

  const canSubmit =
    !!preview &&
    preview.mode !== 'transfer-required' &&
    password.length > 0 &&
    nameMatches &&
    !remove.isPending

  return (
    <Modal
      open={open}
      onClose={close}
      title="Brisanje računa"
      icon={<AlertTriangle className="w-5 h-5 text-red-500" />}
      // A stray click on the backdrop must not close a dialog someone is typing a password into.
      closeOnBackdropClick={false}
      footer={
        <div className="flex items-center justify-end gap-3">
          <button type="button" onClick={close} className="btn-secondary">
            Odustani
          </button>
          {preview?.mode === 'transfer-required' ? (
            <button
              type="button"
              onClick={() => { close(); navigate('/members') }}
              className="btn-primary inline-flex items-center gap-2"
            >
              <Users className="w-4 h-4" />
              Idi na članove
            </button>
          ) : (
            <button
              type="button"
              disabled={!canSubmit}
              onClick={confirmDelete}
              className="btn-danger disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {remove.isPending ? 'Brisanje…' : 'Trajno obriši'}
            </button>
          )}
        </div>
      }
    >
      {isPending && <p className="text-sm text-gray-500 dark:text-slate-400">Provjeravamo šta se briše…</p>}

      {/* Only when there is nothing to show. A refetch that fails while a previous answer is still
          cached would otherwise print "we cannot check what would be deleted" directly above the
          list of what would be deleted. */}
      {isError && !preview && (
        <p className="text-sm text-red-600 dark:text-red-300">
          Ne možemo provjeriti šta bi se obrisalo. Pokušajte ponovo kasnije.
        </p>
      )}

      {preview && (
        <div className="space-y-4">
          <PreviewBody preview={preview} />

          {preview.mode !== 'transfer-required' && (
            <>
              {preview.mode === 'organization' && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
                    Za potvrdu upišite naziv organizacije:{' '}
                    <span className="font-semibold">{preview.organizationName}</span>
                  </label>
                  <input
                    value={orgName}
                    onChange={e => setOrgName(e.target.value)}
                    className="form-input"
                    placeholder={preview.organizationName ?? ''}
                    autoComplete="off"
                  />
                </div>
              )}

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-1">
                  Vaša lozinka
                </label>
                <input
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  type="password"
                  className="form-input"
                  placeholder="Unesite lozinku"
                  autoComplete="current-password"
                />
                <p className="text-xs text-gray-500 dark:text-slate-400 mt-1">
                  Tražimo je da neko drugi ne bi obrisao vaš račun s otključanog telefona.
                </p>
              </div>
            </>
          )}

          {error && (
            <p className="text-sm text-red-600 dark:text-red-300 bg-red-50 dark:bg-red-500/10
                          border border-red-200 dark:border-red-500/30 rounded-xl px-4 py-2.5">
              {error}
            </p>
          )}
        </div>
      )}
    </Modal>
  )
}

/** The part that differs by outcome. Everything below reads what the server decided. */
function PreviewBody({ preview }: { preview: AccountDeletionPreview }) {
  if (preview.mode === 'transfer-required') {
    return (
      <div className="space-y-3">
        <p className="text-sm text-gray-700 dark:text-slate-300">
          Vi ste administrator organizacije <strong>{preview.organizationName}</strong>, koja ima još{' '}
          <strong>{preview.memberCount - 1}</strong>{' '}
          {preview.memberCount - 1 === 1 ? 'člana' : 'članova'}.
        </p>
        <p className="text-sm text-gray-600 dark:text-slate-400">
          Da vaš odlazak ne bi ostavio organizaciju bez vlasnika — a njene članove bez pristupa
          njihovom radu — prvo prenesite vlasništvo na nekog od članova. Nakon toga možete obrisati
          svoj račun kao i svaki drugi korisnik.
        </p>
      </div>
    )
  }

  if (preview.mode === 'organization') {
    return (
      <div className="space-y-3">
        <p className="text-sm text-gray-700 dark:text-slate-300">
          Vi ste jedini član organizacije <strong>{preview.organizationName}</strong>, pa se s vašim
          računom briše i <strong>cijela organizacija</strong>.
        </p>

        <ul className="text-sm text-gray-700 dark:text-slate-300 space-y-1.5 rounded-xl
                       bg-red-50 dark:bg-red-500/10 border border-red-200 dark:border-red-500/30 px-4 py-3">
          <li>• <strong>{preview.apiaryCount}</strong> {preview.apiaryCount === 1 ? 'pčelinjak' : 'pčelinjaka'} sa svim podacima</li>
          <li>• <strong>{preview.beehiveCount}</strong> {preview.beehiveCount === 1 ? 'košnica' : 'košnica'}, sa pregledima, vrcanjima i maticama</li>
          {preview.deletesTreatmentRegister && (
            <li className="pt-1 border-t border-red-200 dark:border-red-500/30 mt-1.5">
              • <strong>Registar tretmana</strong> — zakonska evidencija koju ste dužni čuvati.
              Preuzmite PDF prije brisanja ako vam treba.
            </li>
          )}
        </ul>

        <p className="text-sm text-gray-600 dark:text-slate-400">
          Ovo se ne može poništiti i podaci se ne mogu vratiti.
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-3">
      <p className="text-sm text-gray-700 dark:text-slate-300">
        Briše se vaš račun i vaši lični podaci: prijave, notifikacije, razgovori s AI asistentom i
        vaše dodjele košnica.
      </p>
      {preview.organizationName && (
        <p className="text-sm text-gray-600 dark:text-slate-400">
          Podaci organizacije <strong>{preview.organizationName}</strong> ostaju — pčelinjaci,
          košnice i pregledi pripadaju organizaciji, ne vama lično.
        </p>
      )}
    </div>
  )
}
