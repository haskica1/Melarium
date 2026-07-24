import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { advisorService } from './advisorService'
import type { CreateConversationPayload, SendMessagePayload } from '../models'

export const advisorQueryKeys = {
  conversations: ['advisor', 'conversations'] as const,
  conversation: (id: number) => ['advisor', 'conversations', id] as const,
}

export const useAdvisorConversations = () =>
  useQuery({
    queryKey: advisorQueryKeys.conversations,
    queryFn: advisorService.getConversations,
  })

export const useAdvisorConversation = (id: number) =>
  useQuery({
    queryKey: advisorQueryKeys.conversation(id),
    queryFn: () => advisorService.getConversation(id),
    enabled: id > 0,
  })

export const useCreateAdvisorConversation = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateConversationPayload) => advisorService.create(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: advisorQueryKeys.conversations }),
  })
}

export const useSendAdvisorMessage = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: SendMessagePayload) => advisorService.sendMessage(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: advisorQueryKeys.conversation(id) })
      qc.invalidateQueries({ queryKey: advisorQueryKeys.conversations })
    },
  })
}

export const useDeleteAdvisorConversation = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => advisorService.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: advisorQueryKeys.conversations }),
  })
}
