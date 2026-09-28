import apiClient from './apiClient'
import type { NotificationPriority, NotificationSettings } from '../models'

export interface Notification {
  id: number
  title: string
  message: string
  type: string
  isRead: boolean
  createdAt: string
  relatedEntityId: number | null
  relatedEntityType: string | null
  /** SPEC-29. Stored per row — frost is Critical in April and Normal in August. */
  priority?: NotificationPriority
}

export interface NotificationList {
  notifications: Notification[]
  unreadCount: number
}

export const notificationService = {
  async getAll(): Promise<NotificationList> {
    const { data } = await apiClient.get<NotificationList>('/notifications')
    return data
  },

  async markAllRead(): Promise<void> {
    await apiClient.patch('/notifications/mark-all-read')
  },

  async markRead(id: number): Promise<void> {
    await apiClient.patch(`/notifications/${id}/read`)
  },

  // ── Settings (SPEC-29) ──

  async getSettings(): Promise<NotificationSettings> {
    const { data } = await apiClient.get<NotificationSettings>('/notifications/settings')
    return data
  },

  async updateSettings(payload: NotificationSettings): Promise<NotificationSettings> {
    const { data } = await apiClient.put<NotificationSettings>('/notifications/settings', payload)
    return data
  },
}
