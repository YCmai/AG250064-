import api from './api'

export interface LoginRequest {
  username: string
  password: string
  rememberMe?: boolean
}

export interface LoginResponse {
  token: string
  user: UserInfo
}

export interface UserInfo {
  id: number
  username: string
  displayName: string
  email: string
  isActive: boolean
  permissions: string[]
}

export interface ApiResponse<T> {
  success: boolean
  message: string
  data?: T
}

const authService = {
  login: async (request: LoginRequest): Promise<ApiResponse<LoginResponse>> => {
    const response = await api.post<ApiResponse<LoginResponse>>('/auth/login', request)
    return response as unknown as ApiResponse<LoginResponse>
  },

  logout: async (): Promise<ApiResponse<void>> => {
    const response = await api.post<ApiResponse<void>>('/auth/logout')
    return response as unknown as ApiResponse<void>
  },

  getProfile: async (): Promise<ApiResponse<UserInfo>> => {
    const response = await api.get<ApiResponse<UserInfo>>('/auth/profile')
    return response as unknown as ApiResponse<UserInfo>
  },

  refreshToken: async (): Promise<ApiResponse<{ token: string }>> => {
    const response = await api.post<ApiResponse<{ token: string }>>('/auth/refresh-token')
    return response as unknown as ApiResponse<{ token: string }>
  },
}

export default authService
