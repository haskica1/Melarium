import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { expenseService } from './expenseService'
import type { CreateExpensePayload, UpdateExpensePayload } from '../models'

export const expenseQueryKeys = {
  all: ['expenses'] as const,
  detail: (id: number) => ['expenses', id] as const,
}

export const useExpenses = () =>
  useQuery({ queryKey: expenseQueryKeys.all, queryFn: expenseService.getAll })

export const useExpense = (id: number) =>
  useQuery({
    queryKey: expenseQueryKeys.detail(id),
    queryFn: () => expenseService.getById(id),
    enabled: id > 0,
  })

export const useCreateExpense = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateExpensePayload) => expenseService.create(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: expenseQueryKeys.all })
      // An item may have been attributed to a programme — its cost block is now stale (SPEC-12).
      qc.invalidateQueries({ queryKey: ['diets'] })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}

export const useUpdateExpense = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateExpensePayload) => expenseService.update(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: expenseQueryKeys.all })
      qc.invalidateQueries({ queryKey: expenseQueryKeys.detail(id) })
      qc.invalidateQueries({ queryKey: ['diets'] })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}

export const useDeleteExpense = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => expenseService.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: expenseQueryKeys.all })
      qc.invalidateQueries({ queryKey: ['diets'] })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}
