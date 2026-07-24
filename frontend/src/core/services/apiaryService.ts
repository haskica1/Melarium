import apiClient from './apiClient'
import type {
  Apiary,
  ApiaryDetail,
  CreateApiaryPayload,
  UpdateApiaryPayload,
  WeatherForecast,
} from '../models'

const BASE = '/apiaries'

export const apiaryService = {
  getAll: async (): Promise<Apiary[]> => {
    const res = await apiClient.get<Apiary[]>(BASE)
    return res.data
  },

  getById: async (id: number): Promise<ApiaryDetail> => {
    const res = await apiClient.get<ApiaryDetail>(`${BASE}/${id}`)
    return res.data
  },

  create: async (payload: CreateApiaryPayload): Promise<Apiary> => {
    const res = await apiClient.post<Apiary>(BASE, payload)
    return res.data
  },

  update: async (id: number, payload: UpdateApiaryPayload): Promise<Apiary> => {
    const res = await apiClient.put<Apiary>(`${BASE}/${id}`, payload)
    return res.data
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`${BASE}/${id}`)
  },

  getWeather: async (id: number): Promise<WeatherForecast> => {
    const res = await apiClient.get<WeatherForecast>(`${BASE}/${id}/weather`)
    return res.data
  },
}
