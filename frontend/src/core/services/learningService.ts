import apiClient from './apiClient'
import type {
  LearningTopicSummary,
  LearningTopicDetail,
  AdminLearningTopic,
  SaveLearningTopicPayload,
  GenerateDraftPayload,
  LearningDraft,
  LearningCategory,
  MyLearningSubmission,
  LearningSubmissionSummary,
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

  // ── Proposing a topic (SPEC-26, any role) ──

  async getMySubmissions(): Promise<MyLearningSubmission[]> {
    const { data } = await apiClient.get<MyLearningSubmission[]>('/learning-topics/submissions')
    return data
  },

  async getMySubmission(id: number): Promise<MyLearningSubmission> {
    const { data } = await apiClient.get<MyLearningSubmission>(`/learning-topics/submissions/${id}`)
    return data
  },

  async submit(payload: SaveLearningTopicPayload): Promise<MyLearningSubmission> {
    const { data } = await apiClient.post<MyLearningSubmission>('/learning-topics/submissions', payload)
    return data
  },

  async updateSubmission(id: number, payload: SaveLearningTopicPayload): Promise<MyLearningSubmission> {
    const { data } = await apiClient.put<MyLearningSubmission>(`/learning-topics/submissions/${id}`, payload)
    return data
  },

  async withdrawSubmission(id: number): Promise<void> {
    await apiClient.delete(`/learning-topics/submissions/${id}`)
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

  async adminGetSubmissionSummary(): Promise<LearningSubmissionSummary> {
    const { data } = await apiClient.get<LearningSubmissionSummary>('/admin/learning-topics/submissions/summary')
    return data
  },

  async adminApprove(id: number): Promise<AdminLearningTopic> {
    const { data } = await apiClient.put<AdminLearningTopic>(`/admin/learning-topics/${id}/approve`)
    return data
  },

  async adminReject(id: number, reason: string): Promise<AdminLearningTopic> {
    const { data } = await apiClient.put<AdminLearningTopic>(`/admin/learning-topics/${id}/reject`, { reason })
    return data
  },

  /** Groq drafts a full article — matches the backend's 60 s budget, not apiClient's 10 s default. */
  async adminGenerateDraft(payload: GenerateDraftPayload): Promise<LearningDraft> {
    const { data } = await apiClient.post<LearningDraft>('/admin/learning-topics/generate-draft', payload, {
      timeout: 60_000,
    })
    return data
  },
}
