import apiClient from './apiClient'
import type {
  AssistantConfirmResponse,
  AssistantSessionDetail,
  AssistantSessionSummary,
  AssistantTurnPayload,
  ConfirmActionsPayload,
  StartAssistantSessionPayload,
} from '../models'

export const assistantService = {
  async getSessions(): Promise<AssistantSessionSummary[]> {
    const { data } = await apiClient.get<AssistantSessionSummary[]>('/assistant/sessions')
    return data
  },

  async getSession(id: number): Promise<AssistantSessionDetail> {
    const { data } = await apiClient.get<AssistantSessionDetail>(`/assistant/sessions/${id}`)
    return data
  },

  // Both calls below wait on a Groq completion, so they must match the backend's 60 s HttpClient
  // budget. apiClient's 10 s default aborts the request while the server keeps going — the work is
  // then done and billed but shown to the user as an error. This already happened once with the
  // advisor; see the same note in advisorService.ts.
  async start(payload: StartAssistantSessionPayload): Promise<AssistantSessionDetail> {
    const { data } = await apiClient.post<AssistantSessionDetail>('/assistant/sessions', payload, {
      timeout: 60_000,
    })
    return data
  },

  async addTurn(id: number, payload: AssistantTurnPayload): Promise<AssistantSessionDetail> {
    const { data } = await apiClient.post<AssistantSessionDetail>(`/assistant/sessions/${id}/turns`, payload, {
      timeout: 60_000,
    })
    return data
  },

  async confirm(turnId: number, payload: ConfirmActionsPayload): Promise<AssistantConfirmResponse> {
    const { data } = await apiClient.post<AssistantConfirmResponse>(
      `/assistant/turns/${turnId}/confirm`, payload, { timeout: 60_000 })
    return data
  },

  async reject(turnId: number): Promise<void> {
    await apiClient.post(`/assistant/turns/${turnId}/reject`)
  },

  async remove(id: number): Promise<void> {
    await apiClient.delete(`/assistant/sessions/${id}`)
  },

  /** Records → transcript, reviewed in the input before it is sent (same as inspections/advisor). */
  async transcribe(audioBlob: Blob): Promise<string> {
    const ext = audioBlob.type.includes('mp4') ? 'mp4' : audioBlob.type.includes('ogg') ? 'ogg' : 'webm'
    const formData = new FormData()
    formData.append('audio', audioBlob, `recording.${ext}`)
    const { data } = await apiClient.post<{ transcript: string }>('/assistant/transcribe', formData, {
      headers: { 'Content-Type': undefined },
      timeout: 30_000,
    })
    return data.transcript
  },
}
