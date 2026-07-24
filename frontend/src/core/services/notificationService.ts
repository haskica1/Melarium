import apiClient from './apiClient'

export interface Notification {
  id: number
  title: string
  message: string
  type: string
  isRead: boolean
  createdAt: string
  relatedEntityId: number | null
  relatedEntityType: string | null
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
}
