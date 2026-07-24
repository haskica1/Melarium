import apiClient from './apiClient'

export interface NameValue { name: string; value: number }
export interface MonthCount { month: string; count: number }
export interface MonthTemp { month: string; avgTemp: number | null; minTemp: number | null; maxTemp: number | null }
export interface PriorityStats { priority: string; total: number; completed: number }

export interface StatsData {
  totalApiaries: number
  totalBeehives: number
  totalInspections: number
  activeDiets: number
  pendingTodos: number
  beehivesByType: NameValue[]
  beehivesByMaterial: NameValue[]
  honeyLevelDistribution: NameValue[]
  inspectionsByMonth: MonthCount[]
  temperatureByMonth: MonthTemp[]
  dietsByStatus: NameValue[]
  dietsByFoodType: NameValue[]
  topBeehivesByInspections: NameValue[]
  apiariesByBeehiveCount: NameValue[]
  todosByPriority: PriorityStats[]
  // Harvests (SPEC-02) — kg values arrive as JS numbers
  seasonTotalKg: number
  estimatedRevenue: number
  kgByApiary: NameValue[]
  kgByHoneyType: NameValue[]
  topHivesByYield: NameValue[]
  yearlyYield: NameValue[]
  // Pastures (SPEC-10) — empty when the organization has no moves
  kgByPasture: NameValue[]
}

export const statsService = {
  async get(): Promise<StatsData> {
    const { data } = await apiClient.get<StatsData>('/stats')
    return data
  },
}
