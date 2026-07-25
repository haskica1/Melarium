import apiClient from './apiClient'

export interface ProfileResponse {
  firstName: string
  lastName: string
  email: string
  /** Null while the address is unconfirmed. */
  emailVerifiedAt: string | null
}

export interface UpdateProfilePayload {
  firstName: string
  lastName: string
  email: string
  currentPassword?: string
  newPassword?: string
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
}
