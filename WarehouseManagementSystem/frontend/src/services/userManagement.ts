import api from './api';

export interface User {
  id: number;
  username: string;
  email?: string;
  displayName?: string;
  isAdmin: boolean;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
  loading?: boolean;
}

export interface Permission {
  id: number;
  code?: string;
  name: string;
  description?: string;
}

export interface UserPermission {
  userId: number;
  permissionId: number;
  assignedAt?: string;
}

export const userManagementService = {
  // 获取所有用户
  getAllUsers: async (): Promise<User[]> => {
    const response: any = await api.get('/usermanagement/users');
    const data = response.data || [];
    return data.map((item: any) => ({
      id: item.Id || item.id,
      username: item.Username || item.username,
      email: item.Email || item.email,
      displayName: item.DisplayName || item.displayName,
      isAdmin: item.IsAdmin || item.isAdmin,
      isActive: item.IsActive || item.isActive,
      createdAt: item.CreatedAt || item.createdAt,
      updatedAt: item.UpdatedAt || item.updatedAt
    }));
  },

  // 获取用户详情
  getUserById: async (id: number): Promise<User> => {
    const response: any = await api.get(`/usermanagement/user/${id}`);
    const item = response.data;
    return {
      id: item.Id || item.id,
      username: item.Username || item.username,
      email: item.Email || item.email,
      displayName: item.DisplayName || item.displayName,
      isAdmin: item.IsAdmin || item.isAdmin,
      isActive: item.IsActive || item.isActive,
      createdAt: item.CreatedAt || item.createdAt,
      updatedAt: item.UpdatedAt || item.updatedAt
    };
  },

  // 创建用户
  createUser: async (user: Omit<User, 'id' | 'createdAt' | 'updatedAt'> & { password: string }): Promise<User> => {
    const response: any = await api.post('/usermanagement/user', user);
    const item = response.data;
    // 返回数据可能也需要映射，视后端返回而定
    return {
      id: item.Id || item.id,
      username: item.Username || item.username,
      email: item.Email || item.email,
      displayName: item.DisplayName || item.displayName,
      isAdmin: item.IsAdmin || item.isAdmin,
      isActive: item.IsActive || item.isActive,
      createdAt: item.CreatedAt || item.createdAt,
      updatedAt: item.UpdatedAt || item.updatedAt
    };
  },

  // 更新用户
  updateUser: async (user: User): Promise<void> => {
    await api.put(`/usermanagement/user/${user.id}`, user);
  },

  // 删除用户
  deleteUser: async (id: number): Promise<void> => {
    await api.delete(`/usermanagement/user/${id}`);
  },

  // 获取所有权限
  getAllPermissions: async (): Promise<Permission[]> => {
    const response: any = await api.get('/usermanagement/permissions');
    const data = response.data || [];
    return data.map((item: any) => ({
      id: item.Id || item.id,
      code: item.Code || item.code,
      name: item.Name || item.name,
      description: item.Description || item.description
    }));
  },

  // 获取用户权限
  getUserPermissions: async (userId: number): Promise<Permission[]> => {
    const response: any = await api.get(`/usermanagement/user/${userId}/permissions`);
    const data = response.data || [];
    return data.map((item: any) => ({
      id: item.Id || item.id,
      code: item.Code || item.code,
      name: item.Name || item.name,
      description: item.Description || item.description
    }));
  },

  // 分配权限
  assignPermission: async (userId: number, permissionId: number): Promise<void> => {
    await api.post(`/usermanagement/user/${userId}/permission/${permissionId}`, {});
  },

  // 移除权限
  removePermission: async (userId: number, permissionId: number): Promise<void> => {
    await api.delete(`/usermanagement/user/${userId}/permission/${permissionId}`);
  },

  // 批量分配权限
  assignPermissions: async (userId: number, permissionIds: number[]): Promise<void> => {
    await api.post(`/usermanagement/user/${userId}/permissions`, { permissionIds });
  },

  // 重置用户密码
  resetPassword: async (userId: number, newPassword: string): Promise<void> => {
    await api.put(`/usermanagement/user/${userId}/reset-password`, { newPassword });
  },

  // 启用/禁用用户
  toggleUserStatus: async (userId: number, isActive: boolean): Promise<void> => {
    await api.post(`/usermanagement/user/${userId}/toggle-status`, { isActive });
  },
};
