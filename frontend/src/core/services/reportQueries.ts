import { useQuery } from '@tanstack/react-query'
import { reportService, type SeasonReportFilters } from './reportService'

export const reportQueryKeys = {
  all: ['reports'] as const,
  season: (filters: SeasonReportFilters) => ['reports', 'season', filters] as const,
}

/**
 * The report is only fetched once a period is actually chosen — `enabled` keeps the page from
 * asking the server for a half-typed range while the user is still picking dates.
 */
export const useSeasonReport = (filters: SeasonReportFilters | null) =>
  useQuery({
    queryKey: reportQueryKeys.season(filters ?? { from: '', to: '' }),
    queryFn: () => reportService.getSeasonReport(filters!),
    enabled: filters !== null,
  })
