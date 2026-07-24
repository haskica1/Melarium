import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { orgService } from './orgService'
import type { UpdateBeehiveAssignmentsPayload, UpdateApiaryAssignmentPayload, CreateOrgMemberPayload } from '../models'

export const orgQueryKeys = {
  members: ['org', 'members'] as const,
  member: (id: number) => ['org', 'members', id] as const,
  availableBeehives: ['org', 'available-beehives'] as const,
  availableApiaries: ['org', 'available-apiaries'] as const,
}

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
