import apiClient from './apiClient'
import type { Todo, CreateTodoPayload, UpdateTodoPayload, AssignableUser } from '../models'

export const todoService = {
  getAllOpen: async (): Promise<Todo[]> => {
    const res = await apiClient.get<Todo[]>('/todos/all-open')
    return res.data
  },

  getByApiary: async (apiaryId: number): Promise<Todo[]> => {
    const res = await apiClient.get<Todo[]>(`/todos/by-apiary/${apiaryId}`)
    return res.data
  },

  getByBeehive: async (beehiveId: number): Promise<Todo[]> => {
    const res = await apiClient.get<Todo[]>(`/todos/by-beehive/${beehiveId}`)
    return res.data
  },

  create: async (payload: CreateTodoPayload): Promise<Todo> => {
    const res = await apiClient.post<Todo>('/todos', payload)
    return res.data
  },

  update: async (id: number, payload: UpdateTodoPayload): Promise<Todo> => {
    const res = await apiClient.put<Todo>(`/todos/${id}`, payload)
    return res.data
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/todos/${id}`)
  },

  getAssignableUsersForBeehive: async (beehiveId: number): Promise<AssignableUser[]> => {
    const res = await apiClient.get<AssignableUser[]>(`/todos/assignable-users/${beehiveId}`)
    return res.data
  },
}
