import { useQuery } from '@tanstack/react-query'
import { inviteService } from './inviteService'

export const inviteQueryKeys = {
  all: ['invites'] as const,
  summary: ['invites', 'summary'] as const,
  mine: ['invites', 'mine'] as const,
  inviter: (code: string) => ['invites', 'ref', code] as const,
}

export const useInviteSummary = () =>
  useQuery({ queryKey: inviteQueryKeys.summary, queryFn: inviteService.getSummary })

export const useMyInvitations = () =>
  useQuery({ queryKey: inviteQueryKeys.mine, queryFn: inviteService.getMine })

/**
 * The inviter behind a ?ref= code, for the banner on /register. Never retried and never treated as
 * an error: this is decoration on the sign-up form, and a failed lookup must not make registration
 * look broken.
 */
export const useReferralInviter = (code: string | null) =>
  useQuery({
    queryKey: inviteQueryKeys.inviter(code ?? ''),
    queryFn: () => inviteService.getInviter(code!),
    enabled: !!code,
    retry: false,
    staleTime: Infinity,
  })
