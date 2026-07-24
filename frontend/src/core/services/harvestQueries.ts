import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { harvestService, type HarvestFilters } from './harvestService'
import type { CreateHarvestPayload, UpdateHarvestPayload } from '../models'

export const harvestQueryKeys = {
  all: ['harvests'] as const,
  list: (filters: HarvestFilters) => ['harvests', 'list', filters] as const,
  detail: (id: number) => ['harvests', id] as const,
  hiveYield: (beehiveId: number) => ['harvests', 'hive-yield', beehiveId] as const,
}

export const useHarvests = (filters: HarvestFilters = {}) =>
  useQuery({
    queryKey: harvestQueryKeys.list(filters),
    queryFn: () => harvestService.getAll(filters),
  })

export const useHarvest = (id: number) =>
  useQuery({
    queryKey: harvestQueryKeys.detail(id),
    queryFn: () => harvestService.getById(id),
    enabled: id > 0,
  })

export const useHiveYield = (beehiveId: number) =>
  useQuery({
    queryKey: harvestQueryKeys.hiveYield(beehiveId),
    queryFn: () => harvestService.getHiveYield(beehiveId),
    enabled: !!beehiveId,
  })

export const useCreateHarvest = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateHarvestPayload) => harvestService.create(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: harvestQueryKeys.all })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}

export const useUpdateHarvest = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateHarvestPayload) => harvestService.update(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: harvestQueryKeys.all })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}

export const useDeleteHarvest = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => harvestService.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: harvestQueryKeys.all })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}
