import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiaryService } from '../services/apiaryService'
import { beehiveService, inspectionService } from '../services/beehiveService'
import { inspectionPhotoService } from '../services/inspectionPhotoService'
import { queenService } from '../services/queenService'
import { todoService } from '../services/todoService'
import { dietService } from '../services/dietService'
import { statsService } from '../services/statsService'
import { calendarService } from '../services/calendarService'
import type {
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
  CreateDietPayload,
  UpdateDietPayload,
  CompleteEarlyPayload,
  CopyDietPayload,
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
  dietsByBeehive:     (beehiveId: number) => ['diets', 'beehive', beehiveId] as const,
  diet:               (id: number) => ['diets', id] as const,
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

export const useAllBeehives = () =>
  useQuery({ queryKey: queryKeys.allBeehives, queryFn: beehiveService.getAll, staleTime: 1000 * 60 * 2 })

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
    },
  })
}

export const useDeleteBeehive = (apiaryId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => beehiveService.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.apiary(apiaryId) })
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

export const useCreateInspection = (beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateInspectionPayload) => inspectionService.create(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.beehive(beehiveId) })
      qc.invalidateQueries({ queryKey: queryKeys.inspectionsByHive(beehiveId) })
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

// ── Diet Hooks ────────────────────────────────────────────────────────────────

export const useDietsByBeehive = (beehiveId: number) =>
  useQuery({
    queryKey: queryKeys.dietsByBeehive(beehiveId),
    queryFn:  () => dietService.getByBeehive(beehiveId),
    enabled:  !!beehiveId,
  })

export const useDiet = (id: number) =>
  useQuery({
    queryKey: queryKeys.diet(id),
    queryFn:  () => dietService.getById(id),
    enabled:  !!id,
  })

export const useCreateDiet = (beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateDietPayload) => dietService.create(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.dietsByBeehive(beehiveId) })
      qc.invalidateQueries({ queryKey: queryKeys.calendarEvents })
    },
  })
}

export const useCopyDiet = (sourceDietId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CopyDietPayload) => dietService.copy(sourceDietId, payload),
    onSuccess: (_created, payload) => {
      // Each target hive's diet list is now stale; refresh them + the calendar.
      payload.targetBeehiveIds.forEach(bId =>
        qc.invalidateQueries({ queryKey: queryKeys.dietsByBeehive(bId) }),
      )
      qc.invalidateQueries({ queryKey: queryKeys.calendarEvents })
    },
  })
}

export const useUpdateDiet = (id: number, beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateDietPayload) => dietService.update(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.diet(id) })
      qc.invalidateQueries({ queryKey: queryKeys.dietsByBeehive(beehiveId) })
      qc.invalidateQueries({ queryKey: queryKeys.calendarEvents })
    },
  })
}

export const useDeleteDiet = (beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => dietService.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.dietsByBeehive(beehiveId) })
      qc.invalidateQueries({ queryKey: queryKeys.calendarEvents })
    },
  })
}

export const useCompleteEarlyDiet = (id: number, beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CompleteEarlyPayload) => dietService.completeEarly(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.diet(id) })
      qc.invalidateQueries({ queryKey: queryKeys.dietsByBeehive(beehiveId) })
      qc.invalidateQueries({ queryKey: queryKeys.calendarEvents })
    },
  })
}

export const useCompleteFeedingEntry = (dietId: number, beehiveId: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (entryId: number) => dietService.completeFeedingEntry(dietId, entryId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.diet(dietId) })
      qc.invalidateQueries({ queryKey: queryKeys.dietsByBeehive(beehiveId) })
      qc.invalidateQueries({ queryKey: queryKeys.calendarEvents })
    },
  })
}

// ── Stats Hook ────────────────────────────────────────────────────────────────

export const useStats = () =>
  useQuery({
    queryKey: queryKeys.stats,
    queryFn:  statsService.get,
    staleTime: 1000 * 60 * 5,
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
