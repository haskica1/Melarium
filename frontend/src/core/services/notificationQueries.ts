import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { notificationService } from './notificationService'
import type { NotificationSettings } from '../models'

// Not under ['notifications']: the bell invalidates that prefix on every read, and the settings
// have nothing to do with which notifications are unread.
export const notificationQueryKeys = {
  settings: ['notification-settings'] as const,
}

export const useNotificationSettings = () =>
  useQuery({ queryKey: notificationQueryKeys.settings, queryFn: notificationService.getSettings })

export const useUpdateNotificationSettings = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: NotificationSettings) => notificationService.updateSettings(payload),
    onSuccess: saved => queryClient.setQueryData(notificationQueryKeys.settings, saved),
  })
}
