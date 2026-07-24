import apiClient from './apiClient'
import type {
  Expense,
  ExpenseDetail,
  CreateExpensePayload,
  UpdateExpensePayload,
} from '../models'

export const expenseService = {
  async getAll(): Promise<Expense[]> {
    const { data } = await apiClient.get<Expense[]>('/expenses')
    return data
  },

  async getById(id: number): Promise<ExpenseDetail> {
    const { data } = await apiClient.get<ExpenseDetail>(`/expenses/${id}`)
    return data
  },

  async create(payload: CreateExpensePayload): Promise<ExpenseDetail> {
    const { data } = await apiClient.post<ExpenseDetail>('/expenses', payload)
    return data
  },

  async update(id: number, payload: UpdateExpensePayload): Promise<ExpenseDetail> {
    const { data } = await apiClient.put<ExpenseDetail>(`/expenses/${id}`, payload)
    return data
  },

  async remove(id: number): Promise<void> {
    await apiClient.delete(`/expenses/${id}`)
  },
}
