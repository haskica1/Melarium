import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { treatmentService, type TreatmentFilters } from './treatmentService'
import type { CreateTreatmentPayload, UpdateTreatmentPayload } from '../models'

export const treatmentQueryKeys = {
  all: ['treatments'] as const,
  list: (filters: TreatmentFilters) => ['treatments', 'list', filters] as const,
  detail: (id: number) => ['treatments', id] as const,
}

export const useTreatments = (filters: TreatmentFilters = {}, options: { enabled?: boolean } = {}) =>
  useQuery({
    queryKey: treatmentQueryKeys.list(filters),
    queryFn: () => treatmentService.getAll(filters),
    enabled: options.enabled ?? true,
  })

export const useTreatment = (id: number) =>
  useQuery({
    queryKey: treatmentQueryKeys.detail(id),
    queryFn: () => treatmentService.getById(id),
    enabled: id > 0,
  })

export const useCreateTreatment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateTreatmentPayload) => treatmentService.create(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: treatmentQueryKeys.all }),
  })
}

export const useUpdateTreatment = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateTreatmentPayload) => treatmentService.update(id, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: treatmentQueryKeys.all }),
  })
}

export const useDeleteTreatment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => treatmentService.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: treatmentQueryKeys.all }),
  })
}
