import api from './api';

export interface IODevice {
  id: number;
  name: string;
  ip: string;
  port?: number;
  isEnabled: boolean;
  createdTime?: string;
  updatedTime?: string;
}

export interface IOSignal {
  id: number;
  deviceId: number;
  name: string;
  address: string;
  value: number;
  createdTime?: string;
  updatedTime?: string;
}

export interface IOTask {
  id: number;
  taskType: string;
  deviceIP: string;
  signalAddress: string;
  value: number;
  taskId: string;
  status?: string;
  createdTime?: string;
  completedTime?: string;
  lastUpdatedTime?: string;
}

export const ioMonitorService = {
  // 获取所有IO设备
  getAllDevices: async (): Promise<IODevice[]> => {
    const response: any = await api.get('/iomonitor/devices');
    return response.data || [];
  },

  // 获取最新信号
  getLatestSignals: async (): Promise<IOSignal[]> => {
    const response: any = await api.get('/iomonitor/signals');
    return response.data || [];
  },

  // 添加IO设备
  addDevice: async (device: Omit<IODevice, 'id' | 'createdTime' | 'updatedTime'>): Promise<IODevice> => {
    const response: any = await api.post('/iomonitor/device', device);
    return response.data;
  },

  // 添加IO信号
  addSignal: async (signal: Omit<IOSignal, 'id' | 'createdTime' | 'updatedTime'>): Promise<number> => {
    const response: any = await api.post('/iomonitor/signal', signal);
    return response.data;
  },

  // 更新IO设备
  updateDevice: async (device: IODevice): Promise<void> => {
    await api.put(`/iomonitor/device/${device.id}`, device);
  },

  // 删除IO设备
  deleteDevice: async (id: number): Promise<void> => {
    await api.delete(`/iomonitor/device/${id}`);
  },

  // 删除IO信号
  deleteSignal: async (id: number): Promise<void> => {
    await api.delete(`/iomonitor/signal/${id}`);
  },

  // 切换设备状态
  toggleDevice: async (id: number, isEnabled: boolean): Promise<void> => {
    await api.post(`/iomonitor/device/${id}/toggle`, { isEnabled });
  },

  // 读取信号
  readSignal: async (ip: string, address: string): Promise<number> => {
    const response: any = await api.get(`/iomonitor/signal/read`, {
      params: { ip, address },
    });
    return response.data.value;
  },

  // 写入信号
  writeSignal: async (ip: string, address: string, value: number): Promise<number> => {
    const response: any = await api.post(`/iomonitor/signal/write`, {
      ip,
      address,
      value: value === 1,
    });
    return response.data;
  },

  // 获取IO任务列表
  getIOTasks: async (): Promise<IOTask[]> => {
    const response: any = await api.get('/iomonitor/tasks');
    return response.data || [];
  },

  // 添加IO任务
  addIOTask: async (task: Omit<IOTask, 'id' | 'createdTime'>): Promise<number> => {
    const response = await api.post('/iomonitor/task', task);
    return response.data.data;
  },
};
