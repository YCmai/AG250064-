import api from './api';

export interface SystemSetting {
  key: string;
  value: string;
  description?: string;
  type?: string;
  updatedAt?: string;
}

export interface ConnectionSettings {
  ipAddress?: string;
  port?: number;
  database?: string;
  username?: string;
}

const unwrapPayload = <T = any>(response: any): T => {
  if (response && response.data !== undefined) {
    return response.data as T;
  }
  return response as T;
};

const normalizeSetting = (raw: any): SystemSetting => ({
  key: raw?.key ?? raw?.Key ?? '',
  value: raw?.value ?? raw?.Value ?? '',
  description: raw?.description ?? raw?.Description,
  type: raw?.type ?? raw?.Type,
  updatedAt: raw?.updatedAt ?? raw?.UpdatedAt,
});

export const settingService = {
  getAllSettings: async (): Promise<SystemSetting[]> => {
    const response = await api.get('/setting');
    const payload = unwrapPayload<any[]>(response);
    console.log('[SettingService] Raw response:', response);

    if (Array.isArray(payload)) {
      return payload.map(normalizeSetting);
    }

    return [];
  },

  getSetting: async (key: string): Promise<SystemSetting> => {
    const response = await api.get(`/setting/${key}`);
    return normalizeSetting(unwrapPayload(response));
  },

  updateSetting: async (key: string, value: string): Promise<void> => {
    await api.put(`/setting/${key}`, { value });
  },

  getConnectionSettings: async (): Promise<ConnectionSettings> => {
    const response = await api.get('/setting/connection');
    return unwrapPayload<ConnectionSettings>(response);
  },

  saveConnectionSettings: async (settings: ConnectionSettings): Promise<void> => {
    await api.post('/setting/connection', settings);
  },

  getSystemInfo: async (): Promise<any> => {
    const response = await api.get('/setting/system-info');
    return unwrapPayload(response);
  },

  getDatabaseStatus: async (): Promise<any> => {
    const response = await api.get('/setting/database-status');
    return unwrapPayload(response);
  },

  testDatabaseConnection: async (settings: ConnectionSettings): Promise<boolean> => {
    const response = await api.post('/setting/test-connection', settings);
    return unwrapPayload<boolean>(response);
  },

  backupDatabase: async (): Promise<{ success: boolean; message: string; path: string }> => {
    const response = await api.post('/setting/backup');
    return response;
  },

  exportSettings: async (): Promise<Blob> => {
    const response = await api.get('/setting/export', {
      responseType: 'blob',
    });
    return response.data;
  },

  importSettings: async (file: File): Promise<void> => {
    const formData = new FormData();
    formData.append('file', file);
    await api.post('/setting/import', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
  },

  openBackupFolder: async (): Promise<void> => {
    await api.post('/setting/open-backup-folder');
  },
};
