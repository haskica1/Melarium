import apiClient from './apiClient'
import type {
  Diet,
  DietDetail,
  CreateDietPayload,
  UpdateDietPayload,
  CompleteEarlyPayload,
  CopyDietPayload,
} from '../models'

export const dietService = {
  getByBeehive: async (beehiveId: number): Promise<Diet[]> => {
    const res = await apiClient.get<Diet[]>(`/feedings/by-beehive/${beehiveId}`)
    return res.data
  },

  getById: async (id: number): Promise<DietDetail> => {
    const res = await apiClient.get<DietDetail>(`/feedings/${id}`)
    return res.data
  },

  create: async (payload: CreateDietPayload): Promise<DietDetail> => {
    const res = await apiClient.post<DietDetail>('/feedings', payload)
    return res.data
  },

  /** Copies an existing diet's programme onto other beehives. Returns the created diets. */
  copy: async (id: number, payload: CopyDietPayload): Promise<Diet[]> => {
    const res = await apiClient.post<Diet[]>(`/feedings/${id}/copy`, payload)
    return res.data
  },

  update: async (id: number, payload: UpdateDietPayload): Promise<DietDetail> => {
    const res = await apiClient.put<DietDetail>(`/feedings/${id}`, payload)
    return res.data
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/feedings/${id}`)
  },

  completeEarly: async (id: number, payload: CompleteEarlyPayload): Promise<DietDetail> => {
    const res = await apiClient.post<DietDetail>(`/feedings/${id}/complete-early`, payload)
    return res.data
  },

  completeFeedingEntry: async (dietId: number, entryId: number): Promise<DietDetail> => {
    const res = await apiClient.post<DietDetail>(
      `/feedings/${dietId}/feeding-entries/${entryId}/complete`,
    )
    return res.data
  },
}
