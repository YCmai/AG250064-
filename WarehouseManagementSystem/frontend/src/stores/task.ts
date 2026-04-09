import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface Task {
  id: number
  requestCode: string
  taskStatus: number
  createdTime: string
  sourcePosition: string
  targetPosition: string
}

export const useTaskStore = defineStore('task', () => {
  const tasks = ref<Task[]>([])
  const total = ref(0)
  const page = ref(1)
  const pageSize = ref(20)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const setTasks = (newTasks: Task[], newTotal: number) => {
    tasks.value = newTasks
    total.value = newTotal
  }

  const setPage = (newPage: number) => {
    page.value = newPage
  }

  const setPageSize = (newPageSize: number) => {
    pageSize.value = newPageSize
  }

  const setLoading = (loading: boolean) => {
    isLoading.value = loading
  }

  const setError = (err: string | null) => {
    error.value = err
  }

  const addTask = (task: Task) => {
    tasks.value.push(task)
  }

  const updateTask = (task: Task) => {
    const index = tasks.value.findIndex((t) => t.id === task.id)
    if (index !== -1) {
      tasks.value[index] = task
    }
  }

  const removeTask = (id: number) => {
    tasks.value = tasks.value.filter((t) => t.id !== id)
  }

  return {
    tasks,
    total,
    page,
    pageSize,
    isLoading,
    error,
    setTasks,
    setPage,
    setPageSize,
    setLoading,
    setError,
    addTask,
    updateTask,
    removeTask,
  }
})
