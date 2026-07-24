import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminService } from './adminService'
import type {
  CreateOrganizationPayload,
  UpdateOrganizationPayload,
  UpdateOrganizationPlanPayload,
  CreateAdminUserPayload,
  UpdateAdminUserPayload,
} from '../models'

export const adminQueryKeys = {
  organizations: ['admin', 'organizations'] as const,
  organization: (id: number) => ['admin', 'organizations', id] as const,
  apiariesByOrg: (orgId: number) => ['admin', 'organizations', orgId, 'apiaries'] as const,
  beehivesByOrg: (orgId: number) => ['admin', 'organizations', orgId, 'beehives'] as const,
  users: ['admin', 'users'] as const,
  user: (id: number) => ['admin', 'users', id] as const,
}

// ── Organization Hooks ────────────────────────────────────────────────────────

export const useAdminOrganizations = () =>
  useQuery({ queryKey: adminQueryKeys.organizations, queryFn: adminService.getOrganizations })

export const useAdminOrganization = (id: number) =>
  useQuery({
    queryKey: adminQueryKeys.organization(id),
    queryFn: () => adminService.getOrganization(id),
    enabled: !!id,
  })

export const useCreateOrganization = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateOrganizationPayload) => adminService.createOrganization(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminQueryKeys.organizations }),
  })
}

export const useUpdateOrganization = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateOrganizationPayload) => adminService.updateOrganization(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: adminQueryKeys.organizations })
      qc.invalidateQueries({ queryKey: adminQueryKeys.organization(id) })
    },
  })
}

export const useUpdateOrganizationPlan = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateOrganizationPlanPayload) => adminService.updateOrganizationPlan(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: adminQueryKeys.organizations })
      qc.invalidateQueries({ queryKey: adminQueryKeys.organization(id) })
    },
  })
}

export const useApiariesByOrganization = (orgId: number) =>
  useQuery({
    queryKey: adminQueryKeys.apiariesByOrg(orgId),
    queryFn: () => adminService.getApiariesByOrganization(orgId),
    enabled: orgId > 0,
  })

export const useBeehivesByOrganization = (orgId: number) =>
  useQuery({
    queryKey: adminQueryKeys.beehivesByOrg(orgId),
    queryFn: () => adminService.getBeehivesByOrganization(orgId),
    enabled: orgId > 0,
  })

export const useDeleteOrganization = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => adminService.deleteOrganization(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminQueryKeys.organizations }),
  })
}

// ── User Hooks ────────────────────────────────────────────────────────────────

export const useAdminUsers = () =>
  useQuery({ queryKey: adminQueryKeys.users, queryFn: adminService.getUsers })

export const useAdminUser = (id: number) =>
  useQuery({
    queryKey: adminQueryKeys.user(id),
    queryFn: () => adminService.getUser(id),
    enabled: !!id,
  })

export const useCreateAdminUser = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateAdminUserPayload) => adminService.createUser(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminQueryKeys.users }),
  })
}

export const useUpdateAdminUser = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateAdminUserPayload) => adminService.updateUser(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: adminQueryKeys.users })
      qc.invalidateQueries({ queryKey: adminQueryKeys.user(id) })
    },
  })
}

export const useDeleteAdminUser = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => adminService.deleteUser(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminQueryKeys.users }),
  })
}
