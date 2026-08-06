import axios from 'axios'
import apiClient from './apiClient'
import type { Invitation, InvitationSummary, ReferralInviter } from '../models'

export const inviteService = {
  async getSummary(): Promise<InvitationSummary> {
    const { data } = await apiClient.get<InvitationSummary>('/invites/summary')
    return data
  },

  async getMine(): Promise<Invitation[]> {
    const { data } = await apiClient.get<Invitation[]>('/invites/mine')
    return data
  },

  /**
   * Resolves a ?ref= code for the sign-up banner. Uses a bare axios call, not apiClient: the
   * visitor has no session, so apiClient's refresh-on-401 interceptor has nothing to refresh and
   * would only add a pointless round trip. An unknown code 404s and is not an error worth showing —
   * the sign-up form works identically without the banner.
   */
  async getInviter(code: string): Promise<ReferralInviter | null> {
    try {
      const { data } = await axios.get<ReferralInviter>(
        `${apiClient.defaults.baseURL}/invites/ref/${encodeURIComponent(code)}`,
      )
      return data
    } catch {
      return null
    }
  },
}
