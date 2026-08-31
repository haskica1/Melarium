import apiClient from './apiClient'
import type {
  OrgMember,
  OrgAvailableBeehive,
  OrgAvailableApiary,
  UpdateBeehiveAssignmentsPayload,
  UpdateApiaryAssignmentPayload,
  CreateOrgMemberPayload,
  MyOrganization,
  UpdateMyOrganizationPayload,
} from '../models'

export const orgService = {
  // ── Moja organizacija (SPEC-22) ──

  async getMyOrganization(): Promise<MyOrganization> {
    const { data } = await apiClient.get<MyOrganization>('/organizations/my')
    return data
  },

  async updateMyOrganization(payload: UpdateMyOrganizationPayload): Promise<MyOrganization> {
    const { data } = await apiClient.put<MyOrganization>('/organizations/my', payload)
    return data
  },

  async uploadLogo(file: File): Promise<MyOrganization> {
    const form = new FormData()
    form.append('file', file)
    const { data } = await apiClient.post<MyOrganization>('/organizations/my/logo', form, {
      // Left to the browser so it can set the multipart boundary (inspection-photo precedent).
      headers: { 'Content-Type': undefined },
      timeout: 60_000,
    })
    return data
  },

  async deleteLogo(): Promise<MyOrganization> {
    const { data } = await apiClient.delete<MyOrganization>('/organizations/my/logo')
    return data
  },

  /**
   * Fetches the logo bytes through apiClient — an <img src> cannot carry the Bearer header.
   * Callers turn the blob into an object URL and must revoke it on unmount.
   */
  async fetchLogoBlob(): Promise<Blob> {
    const { data } = await apiClient.get<Blob>('/organizations/my/logo', { responseType: 'blob' })
    return data
  },

  async getMembers(): Promise<OrgMember[]> {
    const { data } = await apiClient.get<OrgMember[]>('/org/members')
    return data
  },

  async getMember(id: number): Promise<OrgMember> {
    const { data } = await apiClient.get<OrgMember>(`/org/members/${id}`)
    return data
  },

  async updateBeehiveAssignments(id: number, payload: UpdateBeehiveAssignmentsPayload): Promise<OrgMember> {
    const { data } = await apiClient.put<OrgMember>(`/org/members/${id}/beehive-assignments`, payload)
    return data
  },

  async updateApiaryAssignment(id: number, payload: UpdateApiaryAssignmentPayload): Promise<OrgMember> {
    const { data } = await apiClient.put<OrgMember>(`/org/members/${id}/apiary-assignment`, payload)
    return data
  },

  async getAvailableBeehives(): Promise<OrgAvailableBeehive[]> {
    const { data } = await apiClient.get<OrgAvailableBeehive[]>('/org/available-beehives')
    return data
  },

  async getAvailableApiaries(): Promise<OrgAvailableApiary[]> {
    const { data } = await apiClient.get<OrgAvailableApiary[]>('/org/available-apiaries')
    return data
  },

  async createMember(payload: CreateOrgMemberPayload): Promise<OrgMember> {
    const { data } = await apiClient.post<OrgMember>('/org/members', payload)
    return data
  },

  /**
   * Hands the organization over to an existing member. Returns nothing on purpose: the server
   * revokes both sessions, so the caller has to sign out rather than carry on with a token that
   * still claims OrganizationAdmin.
   */
  async transferOwnership(memberId: number): Promise<void> {
    await apiClient.post('/org/transfer-ownership', { memberId })
  },
}
