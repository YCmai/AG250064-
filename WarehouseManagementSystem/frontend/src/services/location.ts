import api from './api'

export interface Location {
  id: number
  name: string
  nodeRemark: string
  group: string
  materialCode: string
  palletID: string
  weight: string
  quanitity: string
  entryDate: string
  liftingHeight: number
  unloadHeight: number
  depth: number
  wattingNode: string
  isEmpty: boolean
  lock: boolean
  enabled: boolean
  createdTime: string
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

const locationService = {
  getLocations: (searchString = '', page = 1, pageSize = 20) => {
    const params: any = { page, pageSize }
    if (searchString && searchString.trim()) {
      params.searchString = searchString
    }
    return api.get<ApiResponse<PaginatedResponse<Location>>>('/location', { params })
  },

  getLocationById: (id: number) =>
    api.get<ApiResponse<Location>>(`/location/${id}`),

  createLocation: (data: Partial<Location>) =>
    api.post<ApiResponse<{ id: number; name: string }>>('/location', data),

  updateLocation: (id: number, data: Partial<Location>) =>
    api.put<ApiResponse<any>>(`/location/${id}`, data),

  deleteLocation: (id: number) => api.delete<ApiResponse<any>>(`/location/${id}`),

  clearMaterial: (id: number) => api.post<ApiResponse<any>>(`/location/${id}/clear-material`),

  toggleLock: (id: number, lockState: boolean) =>
    api.post<ApiResponse<any>>(`/location/${id}/toggle-lock`, { lockState }),

  toggleEnabled: (id: number, enabledState: boolean) =>
    api.post<ApiResponse<any>>(`/location/${id}/toggle-enabled`, { enabledState }),

  batchClearMaterial: (ids: number[]) =>
    api.post<ApiResponse<{ successCount: number; failCount: number }>>(
      '/location/batch/clear-material',
      { ids }
    ),

  batchToggleLock: (ids: number[], lockState: boolean) =>
    api.post<ApiResponse<{ successCount: number; failCount: number }>>(
      '/location/batch/toggle-lock',
      { ids, lockState }
    ),

  transferMaterial: (sourceLocationId: number, targetLocationId: number) =>
    api.post<ApiResponse<any>>('/location/transfer-material', {
      sourceLocationId,
      targetLocationId,
    }),

  relocateMaterial: (sourceLocationId: number, targetLocationId: number) =>
    api.post<ApiResponse<{ taskId: number; sourceLocation: string; targetLocation: string; materialCode: string }>>(
      '/location/relocate-material',
      { sourceLocationId, targetLocationId }
    ),

  batchImport: (locations: Partial<Location>[]) =>
    api.post<ApiResponse<{ successCount: number; failCount: number; errors: string[] }>>(
      '/location/batch/import',
      { locations }
    ),
}

export default locationService
