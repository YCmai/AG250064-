import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export interface UserInfo {
  id: number
  username: string
  displayName: string
  email: string
  isActive: boolean
  permissions?: string[]
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('auth_token'))
  const user = ref<UserInfo | null>(
    localStorage.getItem('auth_user') ? JSON.parse(localStorage.getItem('auth_user')!) : null
  )
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const isAuthenticated = computed(() => !!token.value)

  const hasPermission = (permissionCode: string) => {
    if (!user.value || !user.value.permissions) {
      // 如果没有权限列表，暂时默认为false，或者根据具体需求处理
      // 考虑到管理员可能没有明确分配权限但有特权，这里可能需要IsAdmin字段
      // 但目前后端只返回了Permissions列表
      return false
    }
    return user.value.permissions.includes(permissionCode)
  }

  const setToken = (newToken: string) => {
    token.value = newToken
    localStorage.setItem('auth_token', newToken)
  }

  const setUser = (newUser: UserInfo) => {
    user.value = newUser
    localStorage.setItem('auth_user', JSON.stringify(newUser))
  }

  const setLoading = (loading: boolean) => {
    isLoading.value = loading
  }

  const setError = (err: string | null) => {
    error.value = err
  }

  const logout = () => {
    token.value = null
    user.value = null
    localStorage.removeItem('auth_token')
    localStorage.removeItem('auth_user')
  }

  return {
    token,
    user,
    isLoading,
    error,
    isAuthenticated,
    setToken,
    setUser,
    setLoading,
    setError,
    logout,
    hasPermission,
  }
})
