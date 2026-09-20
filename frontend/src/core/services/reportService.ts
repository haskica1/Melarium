import apiClient from './apiClient'
import type { SeasonReport } from '../models'

export interface SeasonReportFilters {
  /** yyyy-MM-dd, inclusive on both ends. */
  from: string
  to: string
  /** Omit for every apiary the caller can reach. */
  apiaryId?: number
}

export const reportService = {
  async getSeasonReport(filters: SeasonReportFilters): Promise<SeasonReport> {
    const { data } = await apiClient.get<SeasonReport>('/reports/season', { params: filters })
    return data
  },
}
