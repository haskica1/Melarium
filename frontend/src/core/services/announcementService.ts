import apiClient from './apiClient'
import type {
  AnnouncementBanner,
  AnnouncementDetail,
  AnnouncementList,
  AdminAnnouncement,
  AnnouncementType,
  SaveAnnouncementPayload,
} from '../models'

export interface AnnouncementFilters {
  type?: AnnouncementType
}

export const announcementService = {
  async getAll(filters: AnnouncementFilters = {}): Promise<AnnouncementList> {
    const { data } = await apiClient.get<AnnouncementList>('/announcements', { params: filters })
    return data
  },

  async getBanner(): Promise<AnnouncementBanner> {
    const { data } = await apiClient.get<AnnouncementBanner>('/announcements/banner')
    return data
  },

  async getById(id: number): Promise<AnnouncementDetail> {
    const { data } = await apiClient.get<AnnouncementDetail>(`/announcements/${id}`)
    return data
  },

  async markRead(id: number): Promise<void> {
    await apiClient.post(`/announcements/${id}/read`)
  },

  // ── Authoring (SystemAdmin) ──

  async adminGetAll(): Promise<AdminAnnouncement[]> {
    const { data } = await apiClient.get<AdminAnnouncement[]>('/admin/announcements')
    return data
  },

  async adminGetById(id: number): Promise<AdminAnnouncement> {
    const { data } = await apiClient.get<AdminAnnouncement>(`/admin/announcements/${id}`)
    return data
  },

  async adminCreate(payload: SaveAnnouncementPayload): Promise<AdminAnnouncement> {
    const { data } = await apiClient.post<AdminAnnouncement>('/admin/announcements', payload)
    return data
  },

  async adminUpdate(id: number, payload: SaveAnnouncementPayload): Promise<AdminAnnouncement> {
    const { data } = await apiClient.put<AdminAnnouncement>(`/admin/announcements/${id}`, payload)
    return data
  },

  async adminDelete(id: number): Promise<void> {
    await apiClient.delete(`/admin/announcements/${id}`)
  },

  async adminSetPublished(id: number, isPublished: boolean): Promise<AdminAnnouncement> {
    const { data } = await apiClient.put<AdminAnnouncement>(`/admin/announcements/${id}/publish`, { isPublished })
    return data
  },
}
