import apiClient from './apiClient'
import type {
  OrgMember,
  OrgAvailableBeehive,
  OrgAvailableApiary,
  UpdateBeehiveAssignmentsPayload,
  UpdateApiaryAssignmentPayload,
  CreateOrgMemberPayload,
} from '../models'

export const orgService = {
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
}
