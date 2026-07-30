import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { feedbackService, type AdminFeedbackFilters } from './feedbackService'
import type { CreateFeedbackPayload, UpdateFeedbackStatusPayload } from '../models'

export const feedbackQueryKeys = {
  all: ['feedback'] as const,
  mine: ['feedback', 'mine'] as const,
  adminList: (filters: AdminFeedbackFilters) => ['feedback', 'admin', filters] as const,
  summary: ['feedback', 'summary'] as const,
}

export const useMyFeedback = () =>
  useQuery({ queryKey: feedbackQueryKeys.mine, queryFn: feedbackService.getMine })

export const useSubmitFeedback = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async ({ payload, screenshot }: { payload: CreateFeedbackPayload; screenshot?: File | null }) => {
      const created = await feedbackService.create(payload)
      if (!screenshot) return created
      // The report is already saved — a failed upload must surface as its own problem, not as
      // "sending failed", which would make the user submit the whole thing again.
      return feedbackService.uploadScreenshot(created.id, screenshot)
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: feedbackQueryKeys.all }),
  })
}

// ── SystemAdmin ──

export const useAdminFeedback = (filters: AdminFeedbackFilters = {}) =>
  useQuery({
    queryKey: feedbackQueryKeys.adminList(filters),
    queryFn: () => feedbackService.getAllForAdmin(filters),
  })

export const useFeedbackSummary = (options: { enabled?: boolean } = {}) =>
  useQuery({
    queryKey: feedbackQueryKeys.summary,
    queryFn: feedbackService.getSummary,
    enabled: options.enabled ?? true,
    refetchInterval: 60_000,
  })

export const useUpdateFeedbackStatus = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateFeedbackStatusPayload }) =>
      feedbackService.updateStatus(id, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: feedbackQueryKeys.all }),
  })
}

export const useDeleteFeedback = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => feedbackService.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: feedbackQueryKeys.all }),
  })
}
