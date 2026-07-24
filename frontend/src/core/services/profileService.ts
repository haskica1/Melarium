import apiClient from './apiClient'

export interface ProfileResponse {
  firstName: string
  lastName: string
  email: string
}

export interface UpdateProfilePayload {
  firstName: string
  lastName: string
  email: string
  currentPassword?: string
  newPassword?: string
}

export const profileService = {
  async update(payload: UpdateProfilePayload): Promise<ProfileResponse> {
    const { data } = await apiClient.put<ProfileResponse>('/profile', payload)
    return data
  },
}
