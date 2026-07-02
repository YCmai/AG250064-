import api from './api'

export interface ApiResponse<T> {
  success: boolean
  message: string
  data?: T
}

export interface PdaWorkOrderOption {
  orderNumber: string
  materialNumber: string
  materialName: string
  displayLabel: string
}

export interface PdaBindingResponse {
  bindingId: number
  taskId: number
  requestCode: string
}

const pdaBindingService = {
  getWorkOrders: () =>
    api.get<ApiResponse<PdaWorkOrderOption[]>>('/pda-bindings/work-orders'),

  createBinding: (payload: { orderNumber: string; palletNumber: string; barcode: string }) =>
    api.post<ApiResponse<PdaBindingResponse>>('/pda-bindings', payload),
}

export default pdaBindingService
