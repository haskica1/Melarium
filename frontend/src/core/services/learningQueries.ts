import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { learningService, type LearningFilters } from './learningService'
import type { GenerateDraftPayload, SaveLearningTopicPayload } from '../models'

export const learningQueryKeys = {
  all: ['learning-topics'] as const,
  list: (filters: LearningFilters) => ['learning-topics', 'list', filters] as const,
  detail: (id: number) => ['learning-topics', id] as const,
  adminAll: ['learning-topics', 'admin'] as const,
  adminDetail: (id: number) => ['learning-topics', 'admin', id] as const,
}

export const useLearningTopics = (filters: LearningFilters = {}) =>
  useQuery({
    queryKey: learningQueryKeys.list(filters),
    queryFn: () => learningService.getAll(filters),
  })

export const useLearningTopic = (id: number) =>
  useQuery({
    queryKey: learningQueryKeys.detail(id),
    queryFn: () => learningService.getById(id),
    enabled: id > 0,
  })

export const useMarkTopicRead = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => learningService.markRead(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: learningQueryKeys.all }),
  })
}

// ── Authoring (SystemAdmin) ──

export const useAdminLearningTopics = () =>
  useQuery({
    queryKey: learningQueryKeys.adminAll,
    queryFn: () => learningService.adminGetAll(),
  })

export const useAdminLearningTopic = (id: number) =>
  useQuery({
    queryKey: learningQueryKeys.adminDetail(id),
    queryFn: () => learningService.adminGetById(id),
    enabled: id > 0,
  })

export const useCreateLearningTopic = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: SaveLearningTopicPayload) => learningService.adminCreate(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: learningQueryKeys.all }),
  })
}

export const useUpdateLearningTopic = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: SaveLearningTopicPayload) => learningService.adminUpdate(id, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: learningQueryKeys.all }),
  })
}

export const useDeleteLearningTopic = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => learningService.adminDelete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: learningQueryKeys.all }),
  })
}

export const useSetTopicPublished = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, isPublished }: { id: number; isPublished: boolean }) =>
      learningService.adminSetPublished(id, isPublished),
    onSuccess: () => qc.invalidateQueries({ queryKey: learningQueryKeys.all }),
  })
}

export const useGenerateDraft = () =>
  useMutation({
    mutationFn: (payload: GenerateDraftPayload) => learningService.adminGenerateDraft(payload),
  })
