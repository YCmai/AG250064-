import api from './api'

export interface ApiResponse<T> {
  success: boolean
  message: string
  data?: T
}

export interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface WorkOrderRow {
  id: number
  orderNumber: string
  materialNumber: string
  materialName: string
  msgType: string
  createTime: string
  processStatus: number
  remarks?: string
}

export interface AgvCommandInboxRow {
  id: number
  taskNumber: string
  priority: number
  rawJson?: string
  processStatus: number
  errorMsg?: string
  createTime: string
  updateTime?: string
  processTime?: string
}

export interface AgvCommandInboxItemRow {
  id: number
  inboxId: number
  taskNumber: string
  seq: number
  palletNumber?: string
  binNumber?: string
  fromStation?: string
  toStation: string
  taskType: number
  createTime: string
}

export interface AgvOutboundQueueRow {
  id: number
  eventType: number
  taskNumber: string
  businessKey: string
  requestBody: string
  processStatus: number
  retryCount: number
  lastError: string
  nextRetryTime?: string
  createTime: string
  processTime?: string
  updateTime: string
}

export interface AgvOutboundQueueUpsertRequest {
  eventType: number
  taskNumber: string
  businessKey: string
  requestBody: string
  processStatus: number
  retryCount: number
  lastError: string
  nextRetryTime?: string
  processTime?: string
}

const integrationDataService = {
  getWorkOrders: (page = 1, pageSize = 20, orderNumber?: string) =>
    api.get<ApiResponse<PaginatedResponse<WorkOrderRow>>>('/integration-data/workorders', {
      params: { page, pageSize, orderNumber }
    }),

  getAgvCommandInbox: (page = 1, pageSize = 20, taskNumber?: string) =>
    api.get<ApiResponse<PaginatedResponse<AgvCommandInboxRow>>>('/integration-data/agv-command-inbox', {
      params: { page, pageSize, taskNumber }
    }),

  getAgvCommandInboxItems: (page = 1, pageSize = 20, inboxId?: number, taskNumber?: string) =>
    api.get<ApiResponse<PaginatedResponse<AgvCommandInboxItemRow>>>('/integration-data/agv-command-inbox-items', {
      params: { page, pageSize, inboxId, taskNumber }
    }),

  getAgvOutboundQueue: (
    page = 1,
    pageSize = 20,
    taskNumber?: string,
    eventType?: number,
    processStatus?: number
  ) =>
    api.get<ApiResponse<PaginatedResponse<AgvOutboundQueueRow>>>('/integration-data/agv-outbound-queue', {
      params: { page, pageSize, taskNumber, eventType, processStatus }
    }),

  createAgvOutboundQueue: (payload: AgvOutboundQueueUpsertRequest) =>
    api.post<ApiResponse<null>>('/integration-data/agv-outbound-queue', payload),

  updateAgvOutboundQueue: (id: number, payload: AgvOutboundQueueUpsertRequest) =>
    api.put<ApiResponse<null>>(`/integration-data/agv-outbound-queue/${id}`, payload),

  deleteAgvOutboundQueue: (id: number) =>
    api.delete<ApiResponse<null>>(`/integration-data/agv-outbound-queue/${id}`)
}

export default integrationDataService
