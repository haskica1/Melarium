import apiClient from './apiClient'
import type { InspectionPhoto, PhotoAnalysis } from '../models'

/** Client-side pre-checks mirroring the server rules (server stays the source of truth). */
export const MAX_PHOTOS_PER_INSPECTION = 5
export const MAX_PHOTO_SIZE_BYTES = 8 * 1024 * 1024
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

/** Returns a Bosnian error message, or null when the file passes the client-side checks. */
export function validatePhotoFile(file: File): string | null {
  if (file.size > MAX_PHOTO_SIZE_BYTES) return `"${file.name}" je veća od 8 MB.`
  if (file.type && !ALLOWED_TYPES.includes(file.type))
    return `"${file.name}" nije podržan format (dozvoljeni su JPEG, PNG i WebP).`
  return null
}

/** Safely parses analysisJson — malformed/absent input yields null instead of throwing. */
export function parsePhotoAnalysis(analysisJson?: string | null): PhotoAnalysis | null {
  if (!analysisJson) return null
  try {
    const parsed = JSON.parse(analysisJson) as PhotoAnalysis
    return { ...parsed, anomalies: Array.isArray(parsed.anomalies) ? parsed.anomalies : [] }
  } catch {
    return null
  }
}

export const inspectionPhotoService = {
  getByInspection: async (inspectionId: number): Promise<InspectionPhoto[]> => {
    const res = await apiClient.get<InspectionPhoto[]>(`/inspections/${inspectionId}/photos`)
    return res.data
  },

  upload: async (inspectionId: number, file: File, caption?: string): Promise<InspectionPhoto> => {
    const formData = new FormData()
    formData.append('file', file)
    if (caption) formData.append('caption', caption)
    const res = await apiClient.post<InspectionPhoto>(`/inspections/${inspectionId}/photos`, formData, {
      headers: { 'Content-Type': undefined },
      timeout: 60_000,
    })
    return res.data
  },

  delete: async (photoId: number): Promise<void> => {
    await apiClient.delete(`/inspections/photos/${photoId}`)
  },

  /** AI frame analysis (Phase 2) — returns the photo with fresh analysisJson. */
  analyze: async (photoId: number): Promise<InspectionPhoto> => {
    const res = await apiClient.post<InspectionPhoto>(`/inspections/photos/${photoId}/analyze`, null, {
      timeout: 90_000,
    })
    return res.data
  },

  /**
   * Fetches the image bytes through apiClient — an <img src> can't carry the Bearer
   * header. Callers turn the blob into an object URL and must revoke it on unmount.
   */
  fetchImageBlob: async (photoId: number): Promise<Blob> => {
    const res = await apiClient.get<Blob>(`/inspections/photos/${photoId}/file`, {
      responseType: 'blob',
      timeout: 60_000,
    })
    return res.data
  },
}
