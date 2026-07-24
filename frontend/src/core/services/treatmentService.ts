import apiClient from './apiClient'
import type {
  Treatment,
  TreatmentDetail,
  CreateTreatmentPayload,
  UpdateTreatmentPayload,
} from '../models'

export interface TreatmentFilters {
  apiaryId?: number
  beehiveId?: number
  year?: number
}

export const treatmentService = {
  async getAll(filters: TreatmentFilters = {}): Promise<Treatment[]> {
    const { data } = await apiClient.get<Treatment[]>('/treatments', { params: filters })
    return data
  },

  async getById(id: number): Promise<TreatmentDetail> {
    const { data } = await apiClient.get<TreatmentDetail>(`/treatments/${id}`)
    return data
  },

  async create(payload: CreateTreatmentPayload): Promise<TreatmentDetail> {
    const { data } = await apiClient.post<TreatmentDetail>('/treatments', payload)
    return data
  },

  async update(id: number, payload: UpdateTreatmentPayload): Promise<TreatmentDetail> {
    const { data } = await apiClient.put<TreatmentDetail>(`/treatments/${id}`, payload)
    return data
  },

  async remove(id: number): Promise<void> {
    await apiClient.delete(`/treatments/${id}`)
  },
}
