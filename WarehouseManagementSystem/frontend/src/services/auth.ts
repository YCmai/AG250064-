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
  login: (request: LoginRequest) =>
    api.post<ApiResponse<LoginResponse>>('/auth/login', request),

  logout: () => api.post<ApiResponse>('/auth/logout'),

  getProfile: () => api.get<ApiResponse<UserInfo>>('/auth/profile'),

  refreshToken: () => api.post<ApiResponse<{ token: string }>>('/auth/refresh-token'),
}

export default authService
