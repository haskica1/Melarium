import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiaryService } from '../services/apiaryService'
import { beehiveService, inspectionService } from '../services/beehiveService'
import { inspectionPhotoService } from '../services/inspectionPhotoService'
import { queenService } from '../services/queenService'
import { todoService } from '../services/todoService'
import { statsService } from '../services/statsService'
import { calendarService } from '../services/calendarService'
import type {
  Beehive,
  CreateApiaryPayload,
  UpdateApiaryPayload,
  CreateBeehivePayload,
  UpdateBeehivePayload,
  CreateInspectionPayload,
  UpdateInspectionPayload,
  CreateQueenPayload,
  UpdateQueenPayload,
  CreateTodoPayload,
  UpdateTodoPayload,
} from '../models'

// ── Query Keys ────────────────────────────────────────────────────────────────

export const queryKeys = {
  calendarEvents:     ['calendar', 'events'] as const,
  calendarFeed:       ['calendar', 'feed'] as const,
  calendarSettings:   ['calendar', 'settings'] as const,
  stats:              ['stats'] as const,
  apiaries:           ['apiaries'] as const,
  apiary:             (id: number) => ['apiaries', id] as const,
  apiaryWeather:      (id: number) => ['apiaries', id, 'weather'] as const,
  allBeehives:        ['beehives', 'all'] as const,
  beehivesByApiary:   (apiaryId: number) => ['beehives', 'apiary', apiaryId] as const,
  beehive:            (id: number) => ['beehives', id] as const,
  inspectionsByHive:  (beehiveId: number) => ['inspections', 'beehive', beehiveId] as const,
  inspection:         (id: number) => ['inspections', id] as const,
  inspectionPhotos:   (inspectionId: number) => ['inspections', inspectionId, 'photos'] as const,
  queensByBeehive:    (beehiveId: number) => ['queens', 'beehive', beehiveId] as const,
  queenEditHistory:   (queenId: number) => ['queens', queenId, 'history'] as const,
  allOpenTodos:       ['todos', 'all-open'] as const,
  todosByApiary:      (apiaryId: number) => ['todos', 'apiary', apiaryId] as const,
  todosByBeehive:     (beehiveId: number) => ['todos', 'beehive', beehiveId] as const,
  assignableUsersForBeehive: (beehiveId: number) => ['todos', 'assignable-users', 'beehive', beehiveId] as const,
}

// ── Apiary Hooks ──────────────────────────────────────────────────────────────

export const useApiaries = () =>
  useQuery({ queryKey: queryKeys.apiaries, queryFn: apiaryService.getAll })

export const useApiary = (id: number) =>
  useQuery({ queryKey: queryKeys.apiary(id), queryFn: () => apiaryService.getById(id), enabled: !!id })

export const useApiaryWeather = (id: number, hasLocation: boolean) =>
  useQuery({
    queryKey: queryKeys.apiaryWeather(id),
    queryFn: () => apiaryService.getWeather(id),
    enabled: !!id && hasLocation,
    staleTime: 1000 * 60 * 30, // weather data stays fresh for 30 minutes
  })

export const useCreateApiary = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateApiaryPayload) => apiaryService.create(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.apiaries }),
  })
}

export const useUpdateApiary = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateApiaryPayload) => apiaryService.update(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.apiaries })
      qc.invalidateQueries({ queryKey: queryKeys.apiary(id) })
    },
  })
}

export const useDeleteApiary = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiaryService.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.apiaries }),
  })
}

// ── Beehive Hooks ─────────────────────────────────────────────────────────────

/**
 * Every hive in the organization — the list behind the hive pickers (command palette, assistant,
 * sastavljanje društava). Kept fresh for two minutes; a caller that must not act on a stale list
 * passes `refetchOnMount: 'always'` so opening it always revalidates.
 */
export const useAllBeehives = (options: { refetchOnMount?: boolean | 'always' } = {}) =>
  useQuery({
    queryKey: queryKeys.allBeehives,
    queryFn: beehiveService.getAll,
    staleTime: 1000 * 60 * 2,
    refetchOnMount: options.refetchOnMount,
  })

export const useBeehivesByApiary = (apiaryId: number) =>
  useQuery({
    queryKey: queryKeys.beehivesByApiary(apiaryId),
    queryFn: () => beehiveService.getByApiary(apiaryId),
    enabled: !!apiaryId,
  })

export const useBeehive = (id: number) =>
  useQuery({ queryKey: queryKeys.beehive(id), queryFn: () => beehiveService.getById(id), enabled: !!id })

export const useCreateBeehive = (apiaryId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateBeehivePayload) => beehiveService.create(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.apiary(apiaryId) })
      qc.invalidateQueries({ queryKey: queryKeys.beehivesByApiary(apiaryId) })
      // The apiaries list carries each apiary's beehiveCount — without this the Pčelinjaci
      // dashboard kept showing the old count until a manual refresh.
      qc.invalidateQueries({ queryKey: queryKeys.apiaries })
      // The org-wide list feeds every hive picker (command palette, assistant, sastavljanje
      // društava). It is nobody's page, so nothing else ever refetches it: without this a hive
      // created here stayed missing from all of them until a full page reload.
      qc.invalidateQueries({ queryKey: queryKeys.allBeehives })
    },
  })
}

export const useUpdateBeehive = (id: number, apiaryId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateBeehivePayload) => beehiveService.update(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.beehive(id) })
      qc.invalidateQueries({ queryKey: queryKeys.apiary(apiaryId) })
      // A renamed hive has to read the same in the pickers as on its own page.
      qc.invalidateQueries({ queryKey: queryKeys.allBeehives })
    },
  })
}

export const useDeleteBeehive = (apiaryId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => beehiveService.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.apiary(apiaryId) })
      qc.invalidateQueries({ queryKey: queryKeys.apiaries })
      // A deleted hive must disappear from the pickers too — offering it as a merge target would
      // fail only at the server.
      qc.invalidateQueries({ queryKey: queryKeys.allBeehives })
    },
  })
}

// ── Inspection Hooks ──────────────────────────────────────────────────────────

export const useInspectionsByBeehive = (beehiveId: number) =>
  useQuery({
    queryKey: queryKeys.inspectionsByHive(beehiveId),
    queryFn: () => inspectionService.getByBeehive(beehiveId),
    enabled: !!beehiveId,
  })

// Inspection mutations only know beehiveId, but the parent apiary caches its own copy of each
// beehive's inspectionCount (used by ApiaryDetailPage's vitals and the Košnice grid) — without
// this, that page kept showing stale counts until a manual refresh. The beehive's apiaryId is
// immutable, so reading it out of whatever is already cached is safe even if slightly stale.
const invalidateParentApiary = (qc: ReturnType<typeof useQueryClient>, beehiveId: number) => {
  const apiaryId = qc.getQueryData<Beehive>(queryKeys.beehive(beehiveId))?.apiaryId
  if (apiaryId) qc.invalidateQueries({ queryKey: queryKeys.apiary(apiaryId) })
}

export const useCreateInspection = (beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateInspectionPayload) => inspectionService.create(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.beehive(beehiveId) })
      qc.invalidateQueries({ queryKey: queryKeys.inspectionsByHive(beehiveId) })
      invalidateParentApiary(qc, beehiveId)
    },
  })
}

export const useUpdateInspection = (id: number, beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateInspectionPayload) => inspectionService.update(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.inspectionsByHive(beehiveId) })
      qc.invalidateQueries({ queryKey: queryKeys.beehive(beehiveId) })
      invalidateParentApiary(qc, beehiveId)
    },
  })
}

export const useDeleteInspection = (beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => inspectionService.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.inspectionsByHive(beehiveId) })
      qc.invalidateQueries({ queryKey: queryKeys.beehive(beehiveId) })
      invalidateParentApiary(qc, beehiveId)
    },
  })
}

// ── Inspection Photo Hooks (SPEC-05) ──────────────────────────────────────────

export const useInspectionPhotos = (inspectionId: number, enabled = true) =>
  useQuery({
    queryKey: queryKeys.inspectionPhotos(inspectionId),
    queryFn: () => inspectionPhotoService.getByInspection(inspectionId),
    enabled: enabled && !!inspectionId,
    staleTime: 60_000,
  })

export const useDeleteInspectionPhoto = (inspectionId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (photoId: number) => inspectionPhotoService.delete(photoId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.inspectionPhotos(inspectionId) })
    },
  })
}

export const useAnalyzeInspectionPhoto = (inspectionId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (photoId: number) => inspectionPhotoService.analyze(photoId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.inspectionPhotos(inspectionId) })
    },
  })
}

// ── Queen Hooks ───────────────────────────────────────────────────────────────

export const useQueensByBeehive = (beehiveId: number) =>
  useQuery({
    queryKey: queryKeys.queensByBeehive(beehiveId),
    queryFn: () => queenService.getByBeehive(beehiveId),
    enabled: !!beehiveId,
  })

export const useCreateQueen = (beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateQueenPayload) => queenService.create(beehiveId, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.queensByBeehive(beehiveId) }),
  })
}

export const useUpdateQueen = (beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateQueenPayload }) =>
      queenService.update(id, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.queensByBeehive(beehiveId) }),
  })
}

export const useDeleteQueen = (beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => queenService.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.queensByBeehive(beehiveId) }),
  })
}

export const useQueenEditHistory = (queenId: number | null) =>
  useQuery({
    queryKey: queryKeys.queenEditHistory(queenId ?? 0),
    queryFn: () => queenService.getEditHistory(queenId!),
    enabled: !!queenId,
  })

// ── Todo Hooks ────────────────────────────────────────────────────────────────

export const useAllOpenTodos = () =>
  useQuery({ queryKey: queryKeys.allOpenTodos, queryFn: todoService.getAllOpen, staleTime: 1000 * 60 * 2 })

export const useAssignableUsersForBeehive = (beehiveId: number) =>
  useQuery({
    queryKey: queryKeys.assignableUsersForBeehive(beehiveId),
    queryFn: () => todoService.getAssignableUsersForBeehive(beehiveId),
    enabled: !!beehiveId,
    staleTime: 1000 * 60 * 5,
    retry: 0,
    throwOnError: false,
  })

export const useTodosByApiary = (apiaryId: number) =>
  useQuery({
    queryKey: queryKeys.todosByApiary(apiaryId),
    queryFn: () => todoService.getByApiary(apiaryId),
    enabled: !!apiaryId,
  })

export const useTodosByBeehive = (beehiveId: number) =>
  useQuery({
    queryKey: queryKeys.todosByBeehive(beehiveId),
    queryFn: () => todoService.getByBeehive(beehiveId),
    enabled: !!beehiveId,
  })

export const useCreateTodo = (invalidateKey: readonly unknown[]) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateTodoPayload) => todoService.create(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: invalidateKey })
      qc.invalidateQueries({ queryKey: queryKeys.calendarEvents })
    },
  })
}

export const useUpdateTodo = (invalidateKey: readonly unknown[]) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateTodoPayload }) =>
      todoService.update(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: invalidateKey })
      qc.invalidateQueries({ queryKey: queryKeys.calendarEvents })
    },
  })
}

export const useDeleteTodo = (invalidateKey: readonly unknown[]) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => todoService.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: invalidateKey })
      qc.invalidateQueries({ queryKey: queryKeys.calendarEvents })
    },
  })
}

// ── Stats Hook ────────────────────────────────────────────────────────────────

export const useStats = (options: { enabled?: boolean } = {}) =>
  useQuery({
    queryKey: queryKeys.stats,
    queryFn:  statsService.get,
    staleTime: 1000 * 60 * 5,
    enabled: options.enabled ?? true,
  })

// ── Calendar Hook ─────────────────────────────────────────────────────────────

export const useCalendarEvents = () =>
  useQuery({
    queryKey: queryKeys.calendarEvents,
    queryFn:  calendarService.getEvents,
    // Always refetch when the Calendar page is opened so newly added tasks/feedings
    // show up immediately — no manual refresh needed.
    staleTime: 0,
    refetchOnMount: 'always',
  })

// ── Calendar sync (SPEC-11) ─────────────────────────────────────────────────────

export const useCalendarFeedUrl = () =>
  useQuery({ queryKey: queryKeys.calendarFeed, queryFn: calendarService.getFeedUrl })

export const useRotateCalendarFeed = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: calendarService.rotateFeedUrl,
    onSuccess: (data) => qc.setQueryData(queryKeys.calendarFeed, data),
  })
}

export const useCalendarSettings = () =>
  useQuery({ queryKey: queryKeys.calendarSettings, queryFn: calendarService.getSettings })

export const useUpdateCalendarSettings = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: calendarService.updateSettings,
    onSuccess: (data) => {
      qc.setQueryData(queryKeys.calendarSettings, data)
      // Feed enable/disable state lives in settings too — refresh the feed card.
      qc.invalidateQueries({ queryKey: queryKeys.calendarFeed })
    },
  })
}
