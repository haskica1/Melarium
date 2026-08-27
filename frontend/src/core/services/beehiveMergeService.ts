import apiClient from './apiClient'
import type { BeehiveMerge, CreateBeehiveMergePayload, MergePreview } from '../models'

/** Colony merge — sastavljanje društava (SPEC-19). */
export const beehiveMergeService = {
  /** Merges the source hive's colony into the target hive. The source leaves the apiary. */
  create: async (payload: CreateBeehiveMergePayload): Promise<BeehiveMerge> => {
    const res = await apiClient.post<BeehiveMerge>('/beehive-merges', payload)
    return res.data
  },

  /** Reverses a merge inside the 24h window. The server owns the deadline, not the client. */
  undo: async (id: number): Promise<BeehiveMerge> => {
    const res = await apiClient.post<BeehiveMerge>(`/beehive-merges/${id}/undo`)
    return res.data
  },

  /** Merges this hive received (it is the receiving hive), newest first. */
  getReceivedByBeehive: async (beehiveId: number): Promise<BeehiveMerge[]> => {
    const res = await apiClient.get<BeehiveMerge[]>(`/beehive-merges/by-beehive/${beehiveId}`)
    return res.data
  },

  /** Real numbers for the confirm dialog. `targetBeehiveId` adds the receiving hive's queen. */
  getPreview: async (sourceBeehiveId: number, targetBeehiveId?: number): Promise<MergePreview> => {
    const res = await apiClient.get<MergePreview>('/beehive-merges/preview', {
      params: { sourceBeehiveId, targetBeehiveId },
    })
    return res.data
  },
}
