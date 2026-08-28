import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { announcementService, type AnnouncementFilters } from './announcementService'
import type { SaveAnnouncementPayload } from '../models'

export const announcementQueryKeys = {
  all: ['announcements'] as const,
  list: (filters: AnnouncementFilters) => ['announcements', 'list', filters] as const,
  banner: ['announcements', 'banner'] as const,
  adminAll: ['announcements', 'admin'] as const,
  adminDetail: (id: number) => ['announcements', 'admin', id] as const,
}

export const useAnnouncements = (filters: AnnouncementFilters = {}) =>
  useQuery({
    queryKey: announcementQueryKeys.list(filters),
    queryFn: () => announcementService.getAll(filters),
  })

/**
 * Feeds the banner and the menu badge from one request. Runs on every page, so it is deliberately
 * cached for 5 minutes and never polled — announcements change a few times a month, unlike the
 * notification bell (30 s).
 */
export const useAnnouncementBanner = () =>
  useQuery({
    queryKey: announcementQueryKeys.banner,
    queryFn: () => announcementService.getBanner(),
    staleTime: 5 * 60 * 1000,
  })

export const useMarkAnnouncementRead = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => announcementService.markRead(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: announcementQueryKeys.all }),
  })
}

// ── Authoring (SystemAdmin) ──

export const useAdminAnnouncements = () =>
  useQuery({
    queryKey: announcementQueryKeys.adminAll,
    queryFn: () => announcementService.adminGetAll(),
  })

export const useAdminAnnouncement = (id: number) =>
  useQuery({
    queryKey: announcementQueryKeys.adminDetail(id),
    queryFn: () => announcementService.adminGetById(id),
    enabled: id > 0,
  })

export const useCreateAnnouncement = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: SaveAnnouncementPayload) => announcementService.adminCreate(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: announcementQueryKeys.all }),
  })
}

export const useUpdateAnnouncement = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: SaveAnnouncementPayload) => announcementService.adminUpdate(id, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: announcementQueryKeys.all }),
  })
}

export const useDeleteAnnouncement = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => announcementService.adminDelete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: announcementQueryKeys.all }),
  })
}

export const useSetAnnouncementPublished = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, isPublished }: { id: number; isPublished: boolean }) =>
      announcementService.adminSetPublished(id, isPublished),
    onSuccess: () => qc.invalidateQueries({ queryKey: announcementQueryKeys.all }),
  })
}
