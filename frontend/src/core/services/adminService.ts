import apiClient from './apiClient'
import type {
  AdminOrganization,
  AdminUser,
  AdminApiaryListItem,
  AdminBeehiveListItem,
  CreateOrganizationPayload,
  UpdateOrganizationPayload,
  UpdateOrganizationPlanPayload,
  CreateAdminUserPayload,
  UpdateAdminUserPayload,
} from '../models'

export const adminService = {
  // ── Organizations ────────────────────────────────────────────────────────────
  async getOrganizations(): Promise<AdminOrganization[]> {
    const { data } = await apiClient.get<AdminOrganization[]>('/admin/organizations')
    return data
  },

  async getOrganization(id: number): Promise<AdminOrganization> {
    const { data } = await apiClient.get<AdminOrganization>(`/admin/organizations/${id}`)
    return data
  },

  async createOrganization(payload: CreateOrganizationPayload): Promise<AdminOrganization> {
    const { data } = await apiClient.post<AdminOrganization>('/admin/organizations', payload)
    return data
  },

  async updateOrganization(id: number, payload: UpdateOrganizationPayload): Promise<AdminOrganization> {
    const { data } = await apiClient.put<AdminOrganization>(`/admin/organizations/${id}`, payload)
    return data
  },

  async updateOrganizationPlan(id: number, payload: UpdateOrganizationPlanPayload): Promise<AdminOrganization> {
    const { data } = await apiClient.put<AdminOrganization>(`/admin/organizations/${id}/plan`, payload)
    return data
  },

  async deleteOrganization(id: number): Promise<void> {
    await apiClient.delete(`/admin/organizations/${id}`)
  },

  async getApiariesByOrganization(orgId: number): Promise<AdminApiaryListItem[]> {
    const { data } = await apiClient.get<AdminApiaryListItem[]>(`/admin/organizations/${orgId}/apiaries`)
    return data
  },

  async getBeehivesByOrganization(orgId: number): Promise<AdminBeehiveListItem[]> {
    const { data } = await apiClient.get<AdminBeehiveListItem[]>(`/admin/organizations/${orgId}/beehives`)
    return data
  },

  // ── Users ────────────────────────────────────────────────────────────────────
  async getUsers(): Promise<AdminUser[]> {
    const { data } = await apiClient.get<AdminUser[]>('/admin/users')
    return data
  },

  async getUser(id: number): Promise<AdminUser> {
    const { data } = await apiClient.get<AdminUser>(`/admin/users/${id}`)
    return data
  },

  async createUser(payload: CreateAdminUserPayload): Promise<AdminUser> {
    const { data } = await apiClient.post<AdminUser>('/admin/users', payload)
    return data
  },

  async updateUser(id: number, payload: UpdateAdminUserPayload): Promise<AdminUser> {
    const { data } = await apiClient.put<AdminUser>(`/admin/users/${id}`, payload)
    return data
  },

  async deleteUser(id: number): Promise<void> {
    await apiClient.delete(`/admin/users/${id}`)
  },
}
