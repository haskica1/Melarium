import apiClient from './apiClient'
import type {
  AdvisorConversationSummary,
  AdvisorConversationDetail,
  AdvisorMessagePair,
  CreateConversationPayload,
  SendMessagePayload,
} from '../models'

export const advisorService = {
  async getConversations(): Promise<AdvisorConversationSummary[]> {
    const { data } = await apiClient.get<AdvisorConversationSummary[]>('/advisor/conversations')
    return data
  },

  async getConversation(id: number): Promise<AdvisorConversationDetail> {
    const { data } = await apiClient.get<AdvisorConversationDetail>(`/advisor/conversations/${id}`)
    return data
  },

  // Both calls below wait on a Groq chat completion. They must match the backend's 60 s
  // HttpClient budget — apiClient's 10 s default aborted the request while the server kept
  // going, so the answer was persisted and billed but shown to the user as an error.
  async create(payload: CreateConversationPayload): Promise<AdvisorConversationDetail> {
    const { data } = await apiClient.post<AdvisorConversationDetail>('/advisor/conversations', payload, {
      timeout: 60_000,
    })
    return data
  },

  async sendMessage(id: number, payload: SendMessagePayload): Promise<AdvisorMessagePair> {
    const { data } = await apiClient.post<AdvisorMessagePair>(`/advisor/conversations/${id}/messages`, payload, {
      timeout: 60_000,
    })
    return data
  },

  async remove(id: number): Promise<void> {
    await apiClient.delete(`/advisor/conversations/${id}`)
  },

  /** Records → transcript for review before sending (same review-before-commit UX as inspections). */
  async transcribe(audioBlob: Blob): Promise<string> {
    const ext = audioBlob.type.includes('mp4') ? 'mp4' : audioBlob.type.includes('ogg') ? 'ogg' : 'webm'
    const formData = new FormData()
    formData.append('audio', audioBlob, `recording.${ext}`)
    const { data } = await apiClient.post<{ transcript: string }>('/advisor/transcribe', formData, {
      headers: { 'Content-Type': undefined },
      timeout: 30_000,
    })
    return data.transcript
  },
}
