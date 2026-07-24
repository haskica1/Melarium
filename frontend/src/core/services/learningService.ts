import apiClient from './apiClient'
import type {
  LearningTopicSummary,
  LearningTopicDetail,
  AdminLearningTopic,
  SaveLearningTopicPayload,
  GenerateDraftPayload,
  LearningDraft,
  LearningCategory,
} from '../models'

export interface LearningFilters {
  category?: LearningCategory
  month?: number
}

export const learningService = {
  async getAll(filters: LearningFilters = {}): Promise<LearningTopicSummary[]> {
    const { data } = await apiClient.get<LearningTopicSummary[]>('/learning-topics', { params: filters })
    return data
  },

  async getById(id: number): Promise<LearningTopicDetail> {
    const { data } = await apiClient.get<LearningTopicDetail>(`/learning-topics/${id}`)
    return data
  },

  async markRead(id: number): Promise<void> {
    await apiClient.post(`/learning-topics/${id}/read`)
  },

  // ── Authoring (SystemAdmin) ──

  async adminGetAll(): Promise<AdminLearningTopic[]> {
    const { data } = await apiClient.get<AdminLearningTopic[]>('/admin/learning-topics')
    return data
  },

  async adminGetById(id: number): Promise<AdminLearningTopic> {
    const { data } = await apiClient.get<AdminLearningTopic>(`/admin/learning-topics/${id}`)
    return data
  },

  async adminCreate(payload: SaveLearningTopicPayload): Promise<AdminLearningTopic> {
    const { data } = await apiClient.post<AdminLearningTopic>('/admin/learning-topics', payload)
    return data
  },

  async adminUpdate(id: number, payload: SaveLearningTopicPayload): Promise<AdminLearningTopic> {
    const { data } = await apiClient.put<AdminLearningTopic>(`/admin/learning-topics/${id}`, payload)
    return data
  },

  async adminDelete(id: number): Promise<void> {
    await apiClient.delete(`/admin/learning-topics/${id}`)
  },

  async adminSetPublished(id: number, isPublished: boolean): Promise<AdminLearningTopic> {
    const { data } = await apiClient.put<AdminLearningTopic>(`/admin/learning-topics/${id}/publish`, { isPublished })
    return data
  },

  async adminGenerateDraft(payload: GenerateDraftPayload): Promise<LearningDraft> {
    const { data } = await apiClient.post<LearningDraft>('/admin/learning-topics/generate-draft', payload)
    return data
  },
}
