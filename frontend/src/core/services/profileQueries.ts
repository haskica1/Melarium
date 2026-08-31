import { useMutation, useQuery } from '@tanstack/react-query'
import { profileService } from './profileService'
import type { DeleteAccountPayload } from './profileService'

export const profileQueryKeys = {
  deletionPreview: ['profile', 'deletion-preview'] as const,
}

/**
 * What deleting the current account would do. `enabled` so it is fetched when the confirmation
 * dialog opens rather than with the page — it is one request that only matters to someone who
 * actually reached for that button.
 *
 * `staleTime: 0`: membership can change between opening the dialog twice, and a stale answer here
 * decides whether the user is asked to type an organization name before destroying it.
 */
export const useAccountDeletionPreview = (enabled: boolean) =>
  useQuery({
    queryKey: profileQueryKeys.deletionPreview,
    queryFn: () => profileService.getDeletionPreview(),
    enabled,
    staleTime: 0,
  })

/**
 * Deletes the account. No cache invalidation on success on purpose — the session is over and the
 * caller navigates to /login; invalidating would fire refetches with a token that no longer has
 * an account behind it.
 */
export const useDeleteAccount = () =>
  useMutation({
    mutationFn: (payload: DeleteAccountPayload) => profileService.deleteAccount(payload),
  })
