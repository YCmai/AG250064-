import api from './api'

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

export interface WorkOrderRecord {
  id: number
  orderNumber: string
  materialNumber: string
  materialName: string
  msgType: string
  createTime: string
  processStatus: number
  remarks?: string
}

export interface AgvCommandRecord {
  id: number
  requestCode?: string
  taskCode?: string
  taskGroupNo?: string
  taskType: number
  taskStatus: number
  priority?: number
  userPriority?: number
  sourcePosition?: string
  targetPosition?: string
  palletNo?: string
  binNumber?: string
  robotCode?: string
  creatTime?: string
  remarks?: string
}

const externalApiService = {
  getWorkOrders: (pageIndex = 1, pageSize = 20, orderNumber?: string, materialNumber?: string) => {
    const params: Record<string, string | number> = { pageIndex, pageSize }
    if (orderNumber) params.orderNumber = orderNumber
    if (materialNumber) params.materialNumber = materialNumber
    return api.get<ApiResponse<PaginatedResponse<WorkOrderRecord>>>('/external-api-task/workorders', { params })
  },

  getAgvCommands: (pageIndex = 1, pageSize = 20, taskCode?: string, taskGroupNo?: string) => {
    const params: Record<string, string | number> = { pageIndex, pageSize }
    if (taskCode) params.taskCode = taskCode
    if (taskGroupNo) params.taskGroupNo = taskGroupNo
    return api.get<ApiResponse<PaginatedResponse<AgvCommandRecord>>>('/external-api-task/agv-commands', { params })
  },
}

export default externalApiService
