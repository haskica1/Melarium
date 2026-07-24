import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { pastureService } from './pastureService'
import type { CreateApiaryMovePayload, SavePasturePayload } from '../models'

export const pastureQueryKeys = {
  all: ['pastures'] as const,
  moves: (apiaryId: number) => ['apiary-moves', apiaryId] as const,
}

export const usePastures = () =>
  useQuery({
    queryKey: pastureQueryKeys.all,
    queryFn: () => pastureService.getAll(),
  })

export const useCreatePasture = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: SavePasturePayload) => pastureService.create(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: pastureQueryKeys.all }),
  })
}

export const useUpdatePasture = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: SavePasturePayload }) =>
      pastureService.update(id, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: pastureQueryKeys.all }),
  })
}

export const useDeletePasture = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => pastureService.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: pastureQueryKeys.all }),
  })
}

// ── Apiary moves (selidbe) ──

export const useApiaryMoves = (apiaryId: number) =>
  useQuery({
    queryKey: pastureQueryKeys.moves(apiaryId),
    queryFn: () => pastureService.getMoves(apiaryId),
    enabled: apiaryId > 0,
  })

/** A move changes the apiary's pasture and coordinates — invalidate the apiary too. */
export const useCreateApiaryMove = (apiaryId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateApiaryMovePayload) => pastureService.createMove(apiaryId, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: pastureQueryKeys.moves(apiaryId) })
      qc.invalidateQueries({ queryKey: pastureQueryKeys.all })
      qc.invalidateQueries({ queryKey: ['apiaries'] })
    },
  })
}

export const useDeleteApiaryMove = (apiaryId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (moveId: number) => pastureService.removeMove(apiaryId, moveId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: pastureQueryKeys.moves(apiaryId) })
      qc.invalidateQueries({ queryKey: pastureQueryKeys.all })
      qc.invalidateQueries({ queryKey: ['apiaries'] })
    },
  })
}

export const useReturnHomeApiaryMove = (apiaryId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => pastureService.returnHome(apiaryId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: pastureQueryKeys.moves(apiaryId) })
      qc.invalidateQueries({ queryKey: pastureQueryKeys.all })
      qc.invalidateQueries({ queryKey: ['apiaries'] })
    },
  })
}

export const useSetHomeLocation = (apiaryId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ latitude, longitude }: { latitude: number; longitude: number }) =>
      pastureService.setHomeLocation(apiaryId, latitude, longitude),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['apiaries'] }),
  })
}
