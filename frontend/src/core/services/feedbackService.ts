import apiClient from './apiClient'
import type {
  AdminFeedback,
  CreateFeedbackPayload,
  Feedback,
  FeedbackStatus,
  FeedbackSummary,
  FeedbackType,
  UpdateFeedbackStatusPayload,
} from '../models'

export interface AdminFeedbackFilters {
  type?: FeedbackType
  status?: FeedbackStatus
}

export const feedbackService = {
  async create(payload: CreateFeedbackPayload): Promise<Feedback> {
    const { data } = await apiClient.post<Feedback>('/feedback', payload)
    return data
  },

  async getMine(): Promise<Feedback[]> {
    const { data } = await apiClient.get<Feedback[]>('/feedback/mine')
    return data
  },

  /** Attaches the screenshot after the feedback row exists — the create call stays plain JSON. */
  async uploadScreenshot(id: number, file: File): Promise<Feedback> {
    const form = new FormData()
    form.append('file', file)
    const { data } = await apiClient.post<Feedback>(`/feedback/${id}/screenshot`, form)
    return data
  },

  // ── SystemAdmin ──
  async getAllForAdmin(filters: AdminFeedbackFilters = {}): Promise<AdminFeedback[]> {
    const { data } = await apiClient.get<AdminFeedback[]>('/admin/feedback', { params: filters })
    return data
  },

  async getSummary(): Promise<FeedbackSummary> {
    const { data } = await apiClient.get<FeedbackSummary>('/admin/feedback/summary')
    return data
  },

  async updateStatus(id: number, payload: UpdateFeedbackStatusPayload): Promise<AdminFeedback> {
    const { data } = await apiClient.put<AdminFeedback>(`/admin/feedback/${id}/status`, payload)
    return data
  },

  async remove(id: number): Promise<void> {
    await apiClient.delete(`/admin/feedback/${id}`)
  },

  /**
   * Fetches the screenshot bytes through apiClient — an <img src> can't carry the Bearer header
   * (same reason as inspection photos). Callers turn the blob into an object URL and revoke it.
   */
  async fetchScreenshotBlob(id: number): Promise<Blob> {
    const res = await apiClient.get<Blob>(`/feedback/${id}/screenshot`, {
      responseType: 'blob',
      timeout: 60_000,
    })
    return res.data
  },
}
