import apiClient from './apiClient'
import type {
  Diet,
  DietDetail,
  DietActiveInfo,
  CreateDietPayload,
  UpdateDietPayload,
  CompleteEarlyPayload,
  AddDietBeehivesPayload,
  CompleteFeedingEntryPayload,
} from '../models'

export interface DietFilters {
  apiaryId?: number
  beehiveId?: number
  year?: number
}

export const dietService = {
  async getAll(filters: DietFilters = {}): Promise<Diet[]> {
    const { data } = await apiClient.get<Diet[]>('/feedings', { params: filters })
    return data
  },

  /**
   * Active programmes for a whole apiary — one flat row per (hive × programme), grouped by the
   * caller. One request serves every hive badge on a list; never call this per hive.
   */
  async getActive(apiaryId: number): Promise<DietActiveInfo[]> {
    const { data } = await apiClient.get<DietActiveInfo[]>('/feedings/active', { params: { apiaryId } })
    return data
  },

  async getById(id: number): Promise<DietDetail> {
    const { data } = await apiClient.get<DietDetail>(`/feedings/${id}`)
    return data
  },

  async create(payload: CreateDietPayload): Promise<DietDetail> {
    const { data } = await apiClient.post<DietDetail>('/feedings', payload)
    return data
  },

  async update(id: number, payload: UpdateDietPayload): Promise<DietDetail> {
    const { data } = await apiClient.put<DietDetail>(`/feedings/${id}`, payload)
    return data
  },

  async remove(id: number): Promise<void> {
    await apiClient.delete(`/feedings/${id}`)
  },

  async addBeehives(id: number, payload: AddDietBeehivesPayload): Promise<DietDetail> {
    const { data } = await apiClient.post<DietDetail>(`/feedings/${id}/beehives`, payload)
    return data
  },

  /** Soft-removes the hive: the link stays as history with its removal date. */
  async removeBeehive(id: number, beehiveId: number): Promise<DietDetail> {
    const { data } = await apiClient.delete<DietDetail>(`/feedings/${id}/beehives/${beehiveId}`)
    return data
  },

  async completeEarly(id: number, payload: CompleteEarlyPayload): Promise<DietDetail> {
    const { data } = await apiClient.post<DietDetail>(`/feedings/${id}/complete-early`, payload)
    return data
  },

  async completeFeedingEntry(
    dietId: number,
    entryId: number,
    payload: CompleteFeedingEntryPayload = {},
  ): Promise<DietDetail> {
    const { data } = await apiClient.post<DietDetail>(
      `/feedings/${dietId}/feeding-entries/${entryId}/complete`,
      payload,
    )
    return data
  },
}
