import apiClient from './apiClient'

export interface ProfileResponse {
  firstName: string
  lastName: string
  email: string
  /** Canonical E.164. Null on accounts created before phone numbers existed. */
  phone: string | null
  /** Null while the address is unconfirmed. */
  emailVerifiedAt: string | null
}

export interface UpdateProfilePayload {
  firstName: string
  lastName: string
  email: string
  /** Omitted or blank leaves the stored number unchanged — it never clears it. */
  phone?: string
  currentPassword?: string
  newPassword?: string
}

/**
 * `account` — only this account and its personal records go.
 * `organization` — the caller is the organization's last member and its admin, so the organization
 *   and everything in it goes too; the dialog must ask them to type its name.
 * `transfer-required` — they still have members, so nothing can be deleted until they hand over.
 */
export type AccountDeletionMode = 'account' | 'organization' | 'transfer-required'

export interface AccountDeletionPreview {
  mode: AccountDeletionMode
  organizationName: string | null
  memberCount: number
  apiaryCount: number
  beehiveCount: number
  /** True when the organization's treatment register (SPEC-08) goes with it. */
  deletesTreatmentRegister: boolean
}

export interface DeleteAccountPayload {
  password: string
  /** The organization's exact name — required only when `mode` is `organization`. */
  organizationNameConfirmation?: string
}

export const profileService = {
  async get(): Promise<ProfileResponse> {
    const { data } = await apiClient.get<ProfileResponse>('/profile')
    return data
  },

  async update(payload: UpdateProfilePayload): Promise<ProfileResponse> {
    const { data } = await apiClient.put<ProfileResponse>('/profile', payload)
    return data
  },

  /** Re-sends the confirmation link to the signed-in user's address. */
  async resendVerification(): Promise<void> {
    await apiClient.post('/auth/resend-verification')
  },

  /**
   * What deleting this account would actually do. The rule lives on the server — the dialog only
   * renders whichever of the three outcomes it is handed, so the two can never disagree.
   */
  async getDeletionPreview(): Promise<AccountDeletionPreview> {
    const { data } = await apiClient.get<AccountDeletionPreview>('/profile/deletion-preview')
    return data
  },

  /**
   * Permanently deletes the account. The password travels in the body, not the query string, so it
   * stays out of server logs and browser history — hence `data` rather than a second argument.
   */
  async deleteAccount(payload: DeleteAccountPayload): Promise<void> {
    await apiClient.delete('/profile', { data: payload })
  },
}
