import apiClient from './apiClient'
import type {
  Pasture,
  SavePasturePayload,
  ApiaryMove,
  CreateApiaryMovePayload,
} from '../models'

export const pastureService = {
  async getAll(): Promise<Pasture[]> {
    const { data } = await apiClient.get<Pasture[]>('/pastures')
    return data
  },

  async create(payload: SavePasturePayload): Promise<Pasture> {
    const { data } = await apiClient.post<Pasture>('/pastures', payload)
    return data
  },

  async update(id: number, payload: SavePasturePayload): Promise<Pasture> {
    const { data } = await apiClient.put<Pasture>(`/pastures/${id}`, payload)
    return data
  },

  async remove(id: number): Promise<void> {
    await apiClient.delete(`/pastures/${id}`)
  },

  // ── Apiary moves (selidbe) ──

  async getMoves(apiaryId: number): Promise<ApiaryMove[]> {
    const { data } = await apiClient.get<ApiaryMove[]>(`/apiaries/${apiaryId}/moves`)
    return data
  },

  async createMove(apiaryId: number, payload: CreateApiaryMovePayload): Promise<ApiaryMove> {
    const { data } = await apiClient.post<ApiaryMove>(`/apiaries/${apiaryId}/moves`, payload)
    return data
  },

  async removeMove(apiaryId: number, moveId: number): Promise<void> {
    await apiClient.delete(`/apiaries/${apiaryId}/moves/${moveId}`)
  },

  async returnHome(apiaryId: number): Promise<ApiaryMove> {
    const { data } = await apiClient.post<ApiaryMove>(`/apiaries/${apiaryId}/moves/return-home`)
    return data
  },

  async setHomeLocation(apiaryId: number, latitude: number, longitude: number): Promise<void> {
    await apiClient.put(`/apiaries/${apiaryId}/moves/home-location`, { latitude, longitude })
  },
}
