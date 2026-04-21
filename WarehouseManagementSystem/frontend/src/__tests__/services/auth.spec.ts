import { describe, it, expect, vi, beforeEach } from 'vitest'
import authService, { LoginRequest, UserInfo } from '@/services/auth'

// Mock axios
vi.mock('@/services/api', () => ({
  default: {
    post: vi.fn(),
    get: vi.fn(),
  },
}))

import api from '@/services/api'

describe('Auth Service', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('login', () => {
    it('应该发送正确的登录请求', async () => {
      const loginRequest: LoginRequest = {
        username: 'testuser',
        password: 'password123',
        rememberMe: true,
      }

      const mockResponse = {
        success: true,
        message: '登录成功',
        data: {
          token: 'test-token',
          user: {
            id: 1,
            username: 'testuser',
            displayName: 'Test User',
            email: 'test@example.com',
            isActive: true,
          },
        },
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const result = await authService.login(loginRequest)

      expect(api.post).toHaveBeenCalledWith('/auth/login', loginRequest)
      expect(result).toEqual(mockResponse)
    })

    it('应该处理登录失败', async () => {
      const loginRequest: LoginRequest = {
        username: 'testuser',
        password: 'wrongpassword',
      }

      const mockResponse = {
        success: false,
        message: '用户名或密码错误',
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const result = await authService.login(loginRequest)

      expect(result.success).toBe(false)
      expect(result.message).toBe('用户名或密码错误')
    })
  })

  describe('logout', () => {
    it('应该发送登出请求', async () => {
      const mockResponse = {
        success: true,
        message: '登出成功',
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const result = await authService.logout()

      expect(api.post).toHaveBeenCalledWith('/auth/logout')
      expect(result).toEqual(mockResponse)
    })
  })

  describe('getProfile', () => {
    it('应该获取用户信息', async () => {
      const mockUser: UserInfo = {
        id: 1,
        username: 'testuser',
        displayName: 'Test User',
        email: 'test@example.com',
        isActive: true,
        permissions: [],
      }

      const mockResponse = {
        success: true,
        message: '获取成功',
        data: mockUser,
      }

      vi.mocked(api.get).mockResolvedValueOnce(mockResponse)

      const result = await authService.getProfile()

      expect(api.get).toHaveBeenCalledWith('/auth/profile')
      expect(result.data).toEqual(mockUser)
    })
  })

  describe('refreshToken', () => {
    it('应该刷新令牌', async () => {
      const mockResponse = {
        success: true,
        message: '令牌刷新成功',
        data: {
          token: 'new-token',
        },
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const result = await authService.refreshToken()

      expect(api.post).toHaveBeenCalledWith('/auth/refresh-token')
      expect(result.data?.token).toBe('new-token')
    })
  })
})
