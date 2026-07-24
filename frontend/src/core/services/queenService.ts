import apiClient from './apiClient'
import type { Queen, CreateQueenPayload, UpdateQueenPayload, QueenEditLog } from '../models'

export const queenService = {
  getByBeehive: async (beehiveId: number): Promise<Queen[]> => {
    const res = await apiClient.get<Queen[]>(`/beehives/${beehiveId}/queens`)
    return res.data
  },

  getEditHistory: async (queenId: number): Promise<QueenEditLog[]> => {
    const res = await apiClient.get<QueenEditLog[]>(`/queens/${queenId}/history`)
    return res.data
  },

  create: async (beehiveId: number, payload: CreateQueenPayload): Promise<Queen> => {
    const res = await apiClient.post<Queen>(`/beehives/${beehiveId}/queens`, payload)
    return res.data
  },

  update: async (id: number, payload: UpdateQueenPayload): Promise<Queen> => {
    const res = await apiClient.put<Queen>(`/queens/${id}`, payload)
    return res.data
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/queens/${id}`)
  },
}
