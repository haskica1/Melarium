import axios from 'axios'
import apiClient from './apiClient'
import type {
  Beehive,
  BeehiveDetail,
  BeehiveQr,
  BeehiveNumberMatchResult,
  CreateBeehivePayload,
  UpdateBeehivePayload,
  Inspection,
  CreateInspectionPayload,
  UpdateInspectionPayload,
  ParseVoiceResult,
} from '../models'

export interface BeehiveScanInfo {
  id: number
  name: string
  apiaryId: number
}

// Raw axios instance for unauthenticated calls (no auth redirect interceptor)
const publicClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  headers: { 'Content-Type': 'application/json' },
  timeout: 10_000,
})

// ── Beehive Service ───────────────────────────────────────────────────────────

export const beehiveService = {
  getAll: async (): Promise<Beehive[]> => {
    const res = await apiClient.get<Beehive[]>('/beehives/all')
    return res.data
  },

  getByApiary: async (apiaryId: number): Promise<Beehive[]> => {
    const res = await apiClient.get<Beehive[]>(`/beehives/by-apiary/${apiaryId}`)
    return res.data
  },

  /** QR codes for label printing — loaded only when the user exports, not with the list. */
  getQrCodesByApiary: async (apiaryId: number): Promise<BeehiveQr[]> => {
    const res = await apiClient.get<BeehiveQr[]>(`/beehives/by-apiary/${apiaryId}/qr-codes`)
    return res.data
  },

  getById: async (id: number): Promise<BeehiveDetail> => {
    const res = await apiClient.get<BeehiveDetail>(`/beehives/${id}`)
    return res.data
  },

  create: async (payload: CreateBeehivePayload): Promise<Beehive> => {
    const res = await apiClient.post<Beehive>('/beehives', payload)
    return res.data
  },

  update: async (id: number, payload: UpdateBeehivePayload): Promise<Beehive> => {
    const res = await apiClient.put<Beehive>(`/beehives/${id}`, payload)
    return res.data
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/beehives/${id}`)
  },

  /** Public — no auth required. Resolves a scan uniqueId to {id, name, apiaryId}. Returns null if not found. */
  scanLookup: async (uniqueId: string): Promise<BeehiveScanInfo | null> => {
    try {
      const res = await publicClient.get<BeehiveScanInfo>(`/beehives/scan/${uniqueId}`)
      return res.data
    } catch (err: any) {
      if (err.response?.status === 404) return null
      throw err
    }
  },

  /** Authenticated — asks the backend whether the current user can access this beehive. */
  checkAccess: async (id: number): Promise<boolean> => {
    const res = await apiClient.get<{ hasAccess: boolean }>(`/beehives/${id}/has-access`)
    return res.data.hasAccess
  },

  /** Scan-by-number cheap path: resolve an on-device–recognised number to the caller's hives. */
  resolveByNumber: async (number: string): Promise<BeehiveNumberMatchResult> => {
    const res = await apiClient.post<BeehiveNumberMatchResult>('/beehives/resolve-by-number', { number })
    return res.data
  },

  /** Scan-by-number fallback: send the photo so the Groq vision model reads the number, then match. */
  scanByNumber: async (image: Blob): Promise<BeehiveNumberMatchResult> => {
    const formData = new FormData()
    formData.append('file', image, 'hive.jpg')
    const res = await apiClient.post<BeehiveNumberMatchResult>('/beehives/scan-by-number', formData, {
      headers: { 'Content-Type': undefined },
      // Matches the backend's 90 s Groq HttpClient budget — a shorter client timeout would abandon
      // an upload + inference that is still in flight on a slow mobile connection.
      timeout: 90_000,
    })
    return res.data
  },
}

// ── Inspection Service ────────────────────────────────────────────────────────

export const inspectionService = {
  getByBeehive: async (beehiveId: number): Promise<Inspection[]> => {
    const res = await apiClient.get<Inspection[]>(`/inspections/by-beehive/${beehiveId}`)
    return res.data
  },

  getById: async (id: number): Promise<Inspection> => {
    const res = await apiClient.get<Inspection>(`/inspections/${id}`)
    return res.data
  },

  create: async (payload: CreateInspectionPayload): Promise<Inspection> => {
    const res = await apiClient.post<Inspection>('/inspections', payload)
    return res.data
  },

  update: async (id: number, payload: UpdateInspectionPayload): Promise<Inspection> => {
    const res = await apiClient.put<Inspection>(`/inspections/${id}`, payload)
    return res.data
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/inspections/${id}`)
  },

  parseVoice: async (audioBlob: Blob): Promise<ParseVoiceResult> => {
    const ext = audioBlob.type.includes('mp4') ? 'mp4' : audioBlob.type.includes('ogg') ? 'ogg' : 'webm'
    const formData = new FormData()
    formData.append('audio', audioBlob, `recording.${ext}`)
    const res = await apiClient.post<ParseVoiceResult>('/inspections/parse-voice', formData, {
      headers: { 'Content-Type': undefined },
      timeout: 30_000,
    })
    return res.data
  },
}
