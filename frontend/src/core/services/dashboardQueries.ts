import { useQuery } from '@tanstack/react-query'
import { dashboardService } from './dashboardService'

export const dashboardQueryKeys = {
  dashboard: ['dashboard'] as const,
  weather: ['dashboard', 'weather'] as const,
}

export const useDashboard = () =>
  useQuery({ queryKey: dashboardQueryKeys.dashboard, queryFn: dashboardService.get })

/** The server caches forecasts for an hour, so refetching more often than that gains nothing. */
export const useDashboardWeather = () =>
  useQuery({
    queryKey: dashboardQueryKeys.weather,
    queryFn: dashboardService.getWeather,
    staleTime: 30 * 60_000,
  })
