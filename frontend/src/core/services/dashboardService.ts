import apiClient from './apiClient'
import type { ApiaryWeather, Dashboard } from '../models'

/** The start page (SPEC-29). Weather is its own request so a slow forecast never holds up the page. */
export const dashboardService = {
  async get(): Promise<Dashboard> {
    const { data } = await apiClient.get<Dashboard>('/dashboard')
    return data
  },

  async getWeather(): Promise<ApiaryWeather[]> {
    const { data } = await apiClient.get<ApiaryWeather[]>('/dashboard/weather')
    return data
  },
}
