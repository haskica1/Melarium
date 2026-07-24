import { useQuery } from '@tanstack/react-query'
import apiClient from './apiClient'
import { PlanType, type MyPlan } from '../models'

/**
 * Informational pricing (SPEC-09 v1 — no payment flow yet). Prices are communicated monthly
 * but collected annually with a "2 mjeseca gratis" discount.
 */
export const PLAN_PRICING: Record<PlanType, { monthly: number | null; yearly: number | null }> = {
  [PlanType.Free]:     { monthly: 0, yearly: 0 },
  [PlanType.Standard]: { monthly: 20, yearly: 200 },
  [PlanType.Pro]:      { monthly: 35, yearly: 350 },
  [PlanType.Max]:      { monthly: 50, yearly: 500 },
  [PlanType.Partner]:  { monthly: null, yearly: null },
}

/** Contact for manual upgrades (v1). */
export const UPGRADE_EMAIL = 'asim.haskic.ah123@gmail.com'

export const planService = {
  getMyPlan: async (): Promise<MyPlan> => {
    const res = await apiClient.get<MyPlan>('/organizations/my-plan')
    return res.data
  },
}

export const useMyPlan = () =>
  useQuery({
    queryKey: ['my-plan'],
    queryFn: planService.getMyPlan,
    staleTime: 5 * 60_000,
    // The org-less SystemAdmin has no plan (404) — don't hammer or surface it as an error everywhere.
    retry: false,
  })

/** True when the effective plan can't use the given AI/pasture feature (proactive UI gating). */
export function isFeatureLocked(plan: MyPlan | undefined, feature: 'voice' | 'advisor' | 'pastures' | 'photoAnalysis'): boolean {
  if (!plan) return false // unknown → let the 402 backstop handle it rather than false-blocking
  const eff = plan.effectivePlan
  if (feature === 'photoAnalysis') return eff < PlanType.Pro
  return eff < PlanType.Standard
}
