import apiClient from './apiClient'
import type { HiveProductType } from '../models'

export interface NameValue { name: string; value: number }

/** Current-year totals of one product, honey included (SPEC-30). Never summed with another product. */
export interface ProductTypeTotal {
  productType: HiveProductType
  name: string
  kg: number
  estimatedRevenue: number
  unpricedKg: number
  recordCount: number
}

/** `apiaryId` null = the row of shared records, which comes last. */
export interface ApiaryProductTotals {
  apiaryId: number | null
  apiaryName: string
  items: { productType: HiveProductType; name: string; kg: number }[]
}

/** A pasture's or a hive's products, each on its own (SPEC-30). */
export interface NamedProductTotals {
  name: string
  items: { productType: HiveProductType; name: string; kg: number }[]
}
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
  // Apiary feeding (SPEC-12 Phase E) — BAM attributed to programmes starting this year
  feedingCost: number
  // Prinosi (SPEC-30) — current year, every product incl. honey, in enum order
  harvestsByProduct: ProductTypeTotal[]
  // The other products broken down like honey; by pasture is empty without moves
  hiveProductsByApiary: ApiaryProductTotals[]
  hiveProductsByPasture: NamedProductTotals[]
  hiveProductsByBeehive: NamedProductTotals[]
}

export const statsService = {
  async get(): Promise<StatsData> {
    const { data } = await apiClient.get<StatsData>('/stats')
    return data
  },
}
