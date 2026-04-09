import api from './api'

export interface Task {
  id: number
  requestCode: string
  taskStatus: number
  createdTime: string
  sourcePosition: string
  targetPosition: string
  taskType: number
  robotCode: string
  runTaskId: string
  creatTime: string
  endTime: string
}

export interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ApiResponse<T> {
  success: boolean
  message: string
  data?: T
}

const taskService = {
  getTasks: (page = 1, pageSize = 20, filterDate?: string, endDate?: string) => {
    const params: any = { page, pageSize }
    if (filterDate) {
      params.filterDate = filterDate
    }
    if (endDate) {
      params.endDate = endDate
    }
    return api.get<ApiResponse<PaginatedResponse<Task>>>('/task', { params })
  },

  getTaskById: (id: number) => api.get<ApiResponse<Task>>(`/task/${id}`),

  createTask: (data: {
    sourcePosition: string
    targetPosition: string
    materialCode?: string
    taskType?: number
    priority?: number
  }) => api.post<ApiResponse<{ id: number; sourcePosition: string; targetPosition: string }>>('/task', data),

  checkDuplicateTask: (sourcePosition: string, targetPosition: string) =>
    api.get<ApiResponse<{ isDuplicate: boolean }>>('/task/check-duplicate', {
      params: { sourcePosition, targetPosition },
    }),

  getAvailableLocations: () =>
    api.get<ApiResponse<Array<{ id: number; name: string; nodeRemark: string; group: string; isEmpty: boolean }>>>('/task/available-locations'),

  cancelTask: (id: number) => api.post<ApiResponse>(`/task/${id}/cancel`),

  getTaskStatistics: () => api.get<ApiResponse<any>>('/task/statistics'),
}

export default taskService
