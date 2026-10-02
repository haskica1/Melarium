import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { harvestService, type HarvestFilters } from './harvestService'
import type { CreateHarvestPayload, UpdateHarvestPayload } from '../models'

export const harvestQueryKeys = {
  all: ['harvests'] as const,
  list: (filters: HarvestFilters) => ['harvests', 'list', filters] as const,
  detail: (id: number) => ['harvests', id] as const,
  hiveYield: (beehiveId: number) => ['harvests', 'hive-yield', beehiveId] as const,
  hiveSummary: (beehiveId: number) => ['harvests', 'hive-summary', beehiveId] as const,
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

/** Every product a hive gave, per year (SPEC-30) — the hive card. */
export const useHiveHarvestSummary = (beehiveId: number) =>
  useQuery({
    queryKey: harvestQueryKeys.hiveSummary(beehiveId),
    queryFn: () => harvestService.getHiveSummary(beehiveId),
    enabled: !!beehiveId,
  })

/** A harvest feeds the stats page, the dashboard and the season report, so they refetch with the list. */
function useInvalidateHarvests() {
  const qc = useQueryClient()
  return () => {
    qc.invalidateQueries({ queryKey: harvestQueryKeys.all })
    qc.invalidateQueries({ queryKey: ['stats'] })
    qc.invalidateQueries({ queryKey: ['dashboard'] })
    qc.invalidateQueries({ queryKey: ['reports'] })
  }
}

export const useCreateHarvest = () => {
  const invalidate = useInvalidateHarvests()
  return useMutation({
    mutationFn: (payload: CreateHarvestPayload) => harvestService.create(payload),
    onSuccess: invalidate,
  })
}

export const useUpdateHarvest = (id: number) => {
  const invalidate = useInvalidateHarvests()
  return useMutation({
    mutationFn: (payload: UpdateHarvestPayload) => harvestService.update(id, payload),
    onSuccess: invalidate,
  })
}

export const useDeleteHarvest = () => {
  const invalidate = useInvalidateHarvests()
  return useMutation({
    mutationFn: (id: number) => harvestService.remove(id),
    onSuccess: invalidate,
  })
}
