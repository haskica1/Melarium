import apiClient from './apiClient'
import type {
  Harvest,
  HarvestDetail,
  HiveYield,
  CreateHarvestPayload,
  UpdateHarvestPayload,
} from '../models'

export interface HarvestFilters {
  apiaryId?: number
  beehiveId?: number
  year?: number
}

export const harvestService = {
  async getAll(filters: HarvestFilters = {}): Promise<Harvest[]> {
    const { data } = await apiClient.get<Harvest[]>('/harvests', { params: filters })
    return data
  },

  async getById(id: number): Promise<HarvestDetail> {
    const { data } = await apiClient.get<HarvestDetail>(`/harvests/${id}`)
    return data
  },

  async create(payload: CreateHarvestPayload): Promise<HarvestDetail> {
    const { data } = await apiClient.post<HarvestDetail>('/harvests', payload)
    return data
  },

  async update(id: number, payload: UpdateHarvestPayload): Promise<HarvestDetail> {
    const { data } = await apiClient.put<HarvestDetail>(`/harvests/${id}`, payload)
    return data
  },

  async remove(id: number): Promise<void> {
    await apiClient.delete(`/harvests/${id}`)
  },

  async getHiveYield(beehiveId: number): Promise<HiveYield> {
    const { data } = await apiClient.get<HiveYield>(`/harvests/hive/${beehiveId}/yield`)
    return data
  },
}
