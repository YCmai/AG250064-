import axios, { AxiosInstance, AxiosError } from 'axios'
import { useAuthStore } from '@/stores/auth'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5003/api'

const api: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
})

// 请求拦截器
api.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore()
    if (authStore.token) {
      config.headers.Authorization = `Bearer ${authStore.token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// 响应拦截器
api.interceptors.response.use(
  (response) => {
    // 处理所有成功的响应（包括 2xx 和 3xx）
    return response.data
  },
  (error: AxiosError) => {
    // 处理 401 未授权
    if (error.response?.status === 401) {
      // 如果已经在登录页，或者是登录接口本身的401，则不跳转
      const isLoginRequest = error.config?.url?.includes('/auth/login');
      const isLoginPage = window.location.pathname === '/login';

      if (!isLoginRequest && !isLoginPage) {
        const authStore = useAuthStore()
        authStore.logout()
        window.location.href = '/login'
      }
    }
    
    // 处理 403 禁止访问
    if (error.response?.status === 403) {
      console.error('无权限访问此资源')
    }
    
    // 处理 500 服务器错误
    if (error.response?.status === 500) {
      console.error('服务器错误:', error.response.data)
    }
    
    // 处理 400 请求错误
    if (error.response?.status === 400) {
      console.error('请求错误:', error.response.data)
    }
    
    return Promise.reject(error)
  }
)

export default api
