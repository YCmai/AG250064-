import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { userManagementService, User, Permission } from '../services/userManagement';

export const useUserManagementStore = defineStore('userManagement', () => {
  const users = ref<User[]>([]);
  const permissions = ref<Permission[]>([]);
  const selectedUser = ref<User | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const userCount = computed(() => users.value.length);
  const adminCount = computed(() => users.value.filter(u => u.isAdmin).length);
  const activeUserCount = computed(() => users.value.filter(u => u.isActive).length);
  const permissionCount = computed(() => permissions.value.length);

  const fetchUsers = async () => {
    loading.value = true;
    error.value = null;
    try {
      users.value = await userManagementService.getAllUsers();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取用户列表失败';
    } finally {
      loading.value = false;
    }
  };

  const fetchPermissions = async () => {
    loading.value = true;
    error.value = null;
    try {
      permissions.value = await userManagementService.getAllPermissions();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取权限列表失败';
    } finally {
      loading.value = false;
    }
  };

  const getUserById = async (id: number) => {
    try {
      return await userManagementService.getUserById(id);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取用户信息失败';
      throw err;
    }
  };

  const createUser = async (user: Omit<User, 'id' | 'createdAt' | 'updatedAt'> & { password: string }) => {
    try {
      await userManagementService.createUser(user);
      await fetchUsers();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '创建用户失败';
      throw err;
    }
  };

  const updateUser = async (user: User) => {
    try {
      await userManagementService.updateUser(user);
      await fetchUsers();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '更新用户失败';
      throw err;
    }
  };

  const deleteUser = async (id: number) => {
    try {
      await userManagementService.deleteUser(id);
      await fetchUsers();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '删除用户失败';
      throw err;
    }
  };

  const getUserPermissions = async (userId: number) => {
    try {
      return await userManagementService.getUserPermissions(userId);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取用户权限失败';
      throw err;
    }
  };

  const assignPermissions = async (userId: number, permissionIds: number[]) => {
    try {
      await userManagementService.assignPermissions(userId, permissionIds);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '分配权限失败';
      throw err;
    }
  };

  const resetPassword = async (userId: number, newPassword: string) => {
    try {
      await userManagementService.resetPassword(userId, newPassword);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '重置密码失败';
      throw err;
    }
  };

  const toggleUserStatus = async (userId: number, isActive: boolean) => {
    try {
      await userManagementService.toggleUserStatus(userId, isActive);
      await fetchUsers();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '切换用户状态失败';
      throw err;
    }
  };

  const selectUser = (user: User) => {
    selectedUser.value = user;
  };

  const clearError = () => {
    error.value = null;
  };

  return {
    users,
    permissions,
    selectedUser,
    loading,
    error,
    userCount,
    adminCount,
    activeUserCount,
    permissionCount,
    fetchUsers,
    fetchPermissions,
    getUserById,
    createUser,
    updateUser,
    deleteUser,
    getUserPermissions,
    assignPermissions,
    resetPassword,
    toggleUserStatus,
    selectUser,
    clearError,
  };
});
