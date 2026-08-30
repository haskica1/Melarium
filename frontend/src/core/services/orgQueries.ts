import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { orgService } from './orgService'
import type {
  UpdateBeehiveAssignmentsPayload,
  UpdateApiaryAssignmentPayload,
  CreateOrgMemberPayload,
  UpdateMyOrganizationPayload,
} from '../models'

export const orgQueryKeys = {
  myOrganization: ['org', 'my'] as const,
  myOrganizationLogo: ['org', 'my', 'logo'] as const,
  members: ['org', 'members'] as const,
  member: (id: number) => ['org', 'members', id] as const,
  availableBeehives: ['org', 'available-beehives'] as const,
  availableApiaries: ['org', 'available-apiaries'] as const,
}

// ── Moja organizacija (SPEC-22) ───────────────────────────────────────────────

export const useMyOrganization = (enabled = true) =>
  useQuery({ queryKey: orgQueryKeys.myOrganization, queryFn: orgService.getMyOrganization, enabled })

/**
 * The logo bytes as an object URL. Kept in the query cache rather than in component state so the
 * page and the header do not each download the same image; the URL is revoked when the entry
 * leaves the cache.
 */
export const useMyOrganizationLogo = (enabled: boolean) =>
  useQuery({
    queryKey: orgQueryKeys.myOrganizationLogo,
    queryFn: async () => URL.createObjectURL(await orgService.fetchLogoBlob()),
    enabled,
    staleTime: Infinity,
    gcTime: 5 * 60_000,
  })

export const useUpdateMyOrganization = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateMyOrganizationPayload) => orgService.updateMyOrganization(payload),
    onSuccess: org => qc.setQueryData(orgQueryKeys.myOrganization, org),
  })
}

export const useUploadOrgLogo = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => orgService.uploadLogo(file),
    onSuccess: org => {
      qc.setQueryData(orgQueryKeys.myOrganization, org)
      revokeLogo(qc)
      qc.invalidateQueries({ queryKey: orgQueryKeys.myOrganizationLogo })
    },
  })
}

export const useDeleteOrgLogo = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => orgService.deleteLogo(),
    onSuccess: org => {
      qc.setQueryData(orgQueryKeys.myOrganization, org)
      revokeLogo(qc)
      qc.removeQueries({ queryKey: orgQueryKeys.myOrganizationLogo })
    },
  })
}

/** Releases the cached object URL before its entry is replaced — otherwise the blob leaks. */
function revokeLogo(qc: ReturnType<typeof useQueryClient>) {
  const cached = qc.getQueryData<string>(orgQueryKeys.myOrganizationLogo)
  if (cached) URL.revokeObjectURL(cached)
}

// ── Members ───────────────────────────────────────────────────────────────────

export const useOrgMembers = () =>
  useQuery({ queryKey: orgQueryKeys.members, queryFn: orgService.getMembers })

export const useOrgMember = (id: number) =>
  useQuery({
    queryKey: orgQueryKeys.member(id),
    queryFn: () => orgService.getMember(id),
    enabled: id > 0,
  })

export const useAvailableBeehives = () =>
  useQuery({ queryKey: orgQueryKeys.availableBeehives, queryFn: orgService.getAvailableBeehives })

export const useAvailableApiaries = (enabled = true) =>
  useQuery({
    queryKey: orgQueryKeys.availableApiaries,
    queryFn: orgService.getAvailableApiaries,
    enabled,
  })

export const useUpdateBeehiveAssignments = (memberId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateBeehiveAssignmentsPayload) =>
      orgService.updateBeehiveAssignments(memberId, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orgQueryKeys.members })
      qc.invalidateQueries({ queryKey: orgQueryKeys.member(memberId) })
    },
  })
}

export const useUpdateApiaryAssignment = (memberId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateApiaryAssignmentPayload) =>
      orgService.updateApiaryAssignment(memberId, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orgQueryKeys.members })
      qc.invalidateQueries({ queryKey: orgQueryKeys.member(memberId) })
    },
  })
}

export const useCreateOrgMember = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateOrgMemberPayload) => orgService.createMember(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: orgQueryKeys.members })
      qc.invalidateQueries({ queryKey: orgQueryKeys.availableBeehives })
      qc.invalidateQueries({ queryKey: orgQueryKeys.availableApiaries })
    },
  })
}
