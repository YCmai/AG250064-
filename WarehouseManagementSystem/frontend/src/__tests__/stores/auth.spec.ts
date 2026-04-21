import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'

describe('Auth Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('初始状态应该是未认证', () => {
    const authStore = useAuthStore()
    expect(authStore.token).toBeNull()
    expect(authStore.user).toBeNull()
    expect(authStore.isAuthenticated).toBe(false)
  })

  it('设置令牌后应该保存到localStorage', () => {
    const authStore = useAuthStore()
    const testToken = 'test-token-123'

    authStore.setToken(testToken)

    expect(authStore.token).toBe(testToken)
    expect(localStorage.getItem('auth_token')).toBe(testToken)
  })

  it('设置用户信息后应该保存到localStorage', () => {
    const authStore = useAuthStore()
    const testUser = {
      id: 1,
      username: 'testuser',
      displayName: 'Test User',
      email: 'test@example.com',
      isActive: true,
      permissions: [],
    }

    authStore.setUser(testUser)

    expect(authStore.user).toEqual(testUser)
    expect(localStorage.getItem('auth_user')).toBe(JSON.stringify(testUser))
  })

  it('登出后应该清除令牌和用户信息', () => {
    const authStore = useAuthStore()
    const testToken = 'test-token-123'
    const testUser = {
      id: 1,
      username: 'testuser',
      displayName: 'Test User',
      email: 'test@example.com',
      isActive: true,
      permissions: [],
    }

    authStore.setToken(testToken)
    authStore.setUser(testUser)

    expect(authStore.isAuthenticated).toBe(true)

    authStore.logout()

    expect(authStore.token).toBeNull()
    expect(authStore.user).toBeNull()
    expect(authStore.isAuthenticated).toBe(false)
    expect(localStorage.getItem('auth_token')).toBeNull()
    expect(localStorage.getItem('auth_user')).toBeNull()
  })

  it('设置加载状态', () => {
    const authStore = useAuthStore()

    authStore.setLoading(true)
    expect(authStore.isLoading).toBe(true)

    authStore.setLoading(false)
    expect(authStore.isLoading).toBe(false)
  })

  it('设置错误信息', () => {
    const authStore = useAuthStore()
    const errorMsg = 'Login failed'

    authStore.setError(errorMsg)
    expect(authStore.error).toBe(errorMsg)

    authStore.setError(null)
    expect(authStore.error).toBeNull()
  })
})
