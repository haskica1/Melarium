import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { dietService, type DietFilters } from './dietService'
import type {
  CreateDietPayload,
  UpdateDietPayload,
  CompleteEarlyPayload,
  AddDietBeehivesPayload,
  CompleteFeedingEntryPayload,
} from '../models'

export const dietQueryKeys = {
  all: ['diets'] as const,
  list: (filters: DietFilters) => ['diets', 'list', filters] as const,
  detail: (id: number) => ['diets', id] as const,
  active: (apiaryId: number) => ['diets', 'active', apiaryId] as const,
}

/**
 * Every mutation also invalidates ['calendar'] — a programme's rounds are the same obligations the
 * calendar renders, so leaving it stale shows feedings that are already done.
 */
const invalidateAll = (qc: ReturnType<typeof useQueryClient>) => {
  qc.invalidateQueries({ queryKey: dietQueryKeys.all })
  qc.invalidateQueries({ queryKey: ['calendar'] })
}

/** Round ticks and hive changes also move the hive badges and the stats counters. */
const invalidateAllAndBadges = (qc: ReturnType<typeof useQueryClient>) => {
  invalidateAll(qc)
  qc.invalidateQueries({ queryKey: ['diets', 'active'] })
  qc.invalidateQueries({ queryKey: ['stats'] })
}

export const useDiets = (filters: DietFilters = {}, options: { enabled?: boolean } = {}) =>
  useQuery({
    queryKey: dietQueryKeys.list(filters),
    queryFn: () => dietService.getAll(filters),
    enabled: options.enabled ?? true,
  })

export const useDiet = (id: number) =>
  useQuery({
    queryKey: dietQueryKeys.detail(id),
    queryFn: () => dietService.getById(id),
    enabled: id > 0,
  })

/** One request per apiary, not per hive — see dietService.getActive. */
export const useActiveDiets = (apiaryId: number, options: { enabled?: boolean } = {}) =>
  useQuery({
    queryKey: dietQueryKeys.active(apiaryId),
    queryFn: () => dietService.getActive(apiaryId),
    enabled: (options.enabled ?? true) && apiaryId > 0,
  })

export const useCreateDiet = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateDietPayload) => dietService.create(payload),
    onSuccess: () => invalidateAllAndBadges(qc),
  })
}

export const useUpdateDiet = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateDietPayload) => dietService.update(id, payload),
    onSuccess: () => invalidateAllAndBadges(qc),
  })
}

export const useDeleteDiet = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => dietService.remove(id),
    onSuccess: () => invalidateAllAndBadges(qc),
  })
}

export const useAddDietBeehives = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: AddDietBeehivesPayload) => dietService.addBeehives(id, payload),
    onSuccess: () => invalidateAllAndBadges(qc),
  })
}

export const useRemoveDietBeehive = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (beehiveId: number) => dietService.removeBeehive(id, beehiveId),
    onSuccess: () => invalidateAllAndBadges(qc),
  })
}

export const useCompleteEarlyDiet = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CompleteEarlyPayload) => dietService.completeEarly(id, payload),
    onSuccess: () => invalidateAllAndBadges(qc),
  })
}

export const useCompleteFeedingEntry = (dietId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vars: { entryId: number; payload?: CompleteFeedingEntryPayload }) =>
      dietService.completeFeedingEntry(dietId, vars.entryId, vars.payload),
    onSuccess: () => invalidateAllAndBadges(qc),
  })
}
