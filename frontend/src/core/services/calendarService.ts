import apiClient from './apiClient'
import type { CalendarEventsResponse, CalendarFeedInfo, CalendarSettings } from '../models'

export const calendarService = {
  getEvents: (): Promise<CalendarEventsResponse> =>
    apiClient.get<CalendarEventsResponse>('/calendar/events').then(r => r.data),

  // Calendar sync (SPEC-11)
  getFeedUrl: (): Promise<CalendarFeedInfo> =>
    apiClient.get<CalendarFeedInfo>('/calendar/feed-url').then(r => r.data),

  rotateFeedUrl: (): Promise<CalendarFeedInfo> =>
    apiClient.post<CalendarFeedInfo>('/calendar/feed-url/rotate').then(r => r.data),

  getSettings: (): Promise<CalendarSettings> =>
    apiClient.get<CalendarSettings>('/calendar/settings').then(r => r.data),

  updateSettings: (payload: CalendarSettings): Promise<CalendarSettings> =>
    apiClient.put<CalendarSettings>('/calendar/settings', payload).then(r => r.data),
}
