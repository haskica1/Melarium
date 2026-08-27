import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { beehiveMergeService } from './beehiveMergeService'
import { beehiveService } from './beehiveService'
import type { CreateBeehiveMergePayload } from '../models'

export const mergeQueryKeys = {
  receivedByBeehive: (beehiveId: number) => ['beehive-merges', 'beehive', beehiveId] as const,
  preview: (sourceId: number, targetId?: number) =>
    ['beehive-merges', 'preview', sourceId, targetId ?? null] as const,
  mergedByApiary: (apiaryId: number) => ['beehives', 'merged', apiaryId] as const,
}

/**
 * A merge moves a hive out of the apiary and rewrites its queen, todos, feeding and treatment
 * annotations in one shot — so everything that counts or lists hives goes stale at once. Undo has
 * exactly the same blast radius, hence one shared invalidator.
 */
const invalidateAfterMerge = (qc: ReturnType<typeof useQueryClient>) => {
  qc.invalidateQueries({ queryKey: ['beehives'] })
  qc.invalidateQueries({ queryKey: ['apiaries'] })
  qc.invalidateQueries({ queryKey: ['beehive-merges'] })
  qc.invalidateQueries({ queryKey: ['queens'] })
  qc.invalidateQueries({ queryKey: ['todos'] })
  qc.invalidateQueries({ queryKey: ['diets'] })
  qc.invalidateQueries({ queryKey: ['treatments'] })
  qc.invalidateQueries({ queryKey: ['calendar'] })
  qc.invalidateQueries({ queryKey: ['stats'] })
  // The hive count feeds the plan limit — a merged hive frees its slot (SPEC-19 §1).
  qc.invalidateQueries({ queryKey: ['my-plan'] })
}

/** Merges this hive received. Empty for every hive that never took in a colony. */
export const useMergesByBeehive = (beehiveId: number) =>
  useQuery({
    queryKey: mergeQueryKeys.receivedByBeehive(beehiveId),
    queryFn: () => beehiveMergeService.getReceivedByBeehive(beehiveId),
    enabled: beehiveId > 0,
  })

/** The apiary's archive of merged-away hives. */
export const useMergedBeehives = (apiaryId: number, options: { enabled?: boolean } = {}) =>
  useQuery({
    queryKey: mergeQueryKeys.mergedByApiary(apiaryId),
    queryFn: () => beehiveService.getMergedByApiary(apiaryId),
    enabled: (options.enabled ?? true) && apiaryId > 0,
  })

/**
 * Consequence numbers for the confirm dialog; refetched as the receiving hive is chosen.
 *
 * `staleTime: 0` because this is what the beekeeper confirms an irreversible write against — a
 * queen added or a feeding stopped a minute ago has to be in it, so the dialog re-reads it every
 * time it opens instead of serving the cached copy.
 */
export const useMergePreview = (
  sourceBeehiveId: number,
  targetBeehiveId: number | undefined,
  options: { enabled?: boolean } = {},
) =>
  useQuery({
    queryKey: mergeQueryKeys.preview(sourceBeehiveId, targetBeehiveId),
    queryFn: () => beehiveMergeService.getPreview(sourceBeehiveId, targetBeehiveId),
    enabled: (options.enabled ?? true) && sourceBeehiveId > 0,
    staleTime: 0,
    // Only the queen summaries depend on the chosen target; everything else describes the hive
    // leaving. Holding the previous payload keeps the todo/feeding/treatment lines on screen while
    // the next one loads — the caller hides the queen part for as long as `isFetching` is true.
    placeholderData: keepPreviousData,
  })

export const useCreateBeehiveMerge = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateBeehiveMergePayload) => beehiveMergeService.create(payload),
    onSuccess: () => invalidateAfterMerge(qc),
  })
}

export const useUndoBeehiveMerge = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (mergeId: number) => beehiveMergeService.undo(mergeId),
    onSuccess: () => invalidateAfterMerge(qc),
  })
}
