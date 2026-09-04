import { Eye } from 'lucide-react'
import { useMyPlan } from '../../core/services/planService'

/**
 * Standing notice for a member who lost write access because the organization has more accounts
 * than its plan has seats (SPEC-24).
 *
 * It has to be standing rather than a toast on first refusal: this account can still read
 * everything, so without it the app looks normal right up until a save fails, and the member has no
 * way to guess why. It also says who can fix it — the member cannot, only the owner can.
 * Renders nothing for everyone else, which is almost everyone.
 */
export default function ReadOnlyMemberBanner() {
  const { data: plan } = useMyPlan()

  if (!plan?.isReadOnlyMember) return null

  return (
    <div className="mb-4 flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-500/30 dark:bg-amber-500/10">
      <Eye className="mt-0.5 h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400" />
      <div className="text-sm text-amber-900 dark:text-amber-200">
        <p className="font-medium">Vaš nalog je trenutno samo za čitanje.</p>
        <p className="mt-0.5 text-amber-800/80 dark:text-amber-200/70">
          {plan.effectivePlanName} paket organizacije ne pokriva sve članove, pa možete pregledati
          sve podatke ali ne i unositi nove. Vlasnik organizacije može ovo riješiti nadogradnjom paketa.
        </p>
      </div>
    </div>
  )
}
