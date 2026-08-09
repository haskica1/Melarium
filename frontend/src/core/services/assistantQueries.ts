import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { assistantService } from './assistantService'
import type {
  AssistantTurnPayload,
  ConfirmActionsPayload,
  StartAssistantSessionPayload,
} from '../models'

export const assistantQueryKeys = {
  sessions: ['assistant', 'sessions'] as const,
  session: (id: number) => ['assistant', 'sessions', id] as const,
}

export const useAssistantSessions = () =>
  useQuery({
    queryKey: assistantQueryKeys.sessions,
    queryFn: assistantService.getSessions,
  })

export const useAssistantSession = (id: number) =>
  useQuery({
    queryKey: assistantQueryKeys.session(id),
    queryFn: () => assistantService.getSession(id),
    enabled: id > 0,
  })

export const useStartAssistantSession = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: StartAssistantSessionPayload) => assistantService.start(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: assistantQueryKeys.sessions }),
  })
}

export const useAddAssistantTurn = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: AssistantTurnPayload) => assistantService.addTurn(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: assistantQueryKeys.session(id) })
      qc.invalidateQueries({ queryKey: assistantQueryKeys.sessions })
    },
  })
}

/**
 * A confirmation creates inspections and todos through the normal services, so every list that could
 * show them is invalidated — otherwise the record exists but the page the user lands on is stale.
 */
export const useConfirmAssistantActions = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ turnId, payload }: { turnId: number; payload: ConfirmActionsPayload }) =>
      assistantService.confirm(turnId, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: assistantQueryKeys.sessions })
      qc.invalidateQueries({ queryKey: ['inspections'] })
      qc.invalidateQueries({ queryKey: ['todos'] })
      qc.invalidateQueries({ queryKey: ['beehives'] })
      qc.invalidateQueries({ queryKey: ['calendar'] })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}

export const useRejectAssistantActions = () =>
  useMutation({ mutationFn: (turnId: number) => assistantService.reject(turnId) })

export const useDeleteAssistantSession = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => assistantService.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: assistantQueryKeys.sessions }),
  })
}
