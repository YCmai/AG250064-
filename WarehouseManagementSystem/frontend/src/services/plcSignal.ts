import api from './api';

export interface PlcDevice {
  id: number;
  name: string;
  ipAddress: string;
  brand: string;
  model?: string;
  createdTime?: string;
  updatedTime?: string;
}

export interface PlcSignal {
  id: number;
  plcDeviceId: number;
  name: string;
  offset: string;
  dataType: string;
  description?: string;
  createdTime?: string;
  updatedTime?: string;
}

export const plcSignalService = {
  // 获取所有PLC设备
  getAllDevices: async (): Promise<PlcDevice[]> => {
    const response = await api.get('/plcsignal');
    return response.data.data || [];
  },

  // 获取设备详情
  getDeviceById: async (id: number): Promise<PlcDevice> => {
    const response = await api.get(`/plcsignal/${id}`);
    return response.data.data;
  },

  // 获取设备下的所有信号
  getSignalsByDevice: async (deviceId: string): Promise<PlcSignal[]> => {
    const response = await api.get(`/plcsignal/signals/${deviceId}`);
    return response.data.data || [];
  },

  // 获取信号详情
  getSignalById: async (id: number): Promise<PlcSignal> => {
    const response = await api.get(`/plcsignal/signal/${id}`);
    return response.data.data;
  },

  // 添加PLC设备
  addDevice: async (device: Omit<PlcDevice, 'id' | 'createdTime' | 'updatedTime'>): Promise<number> => {
    const response = await api.post('/plcsignal/device', device);
    return response.data.data;
  },

  // 添加PLC信号
  addSignal: async (signal: Omit<PlcSignal, 'id' | 'createdTime' | 'updatedTime'>): Promise<number> => {
    const response = await api.post('/plcsignal/signal', signal);
    return response.data.data;
  },

  // 更新PLC设备
  updateDevice: async (device: PlcDevice): Promise<void> => {
    await api.put(`/plcsignal/${device.id}`, device);
  },

  // 更新PLC信号
  updateSignal: async (signal: PlcSignal): Promise<void> => {
    await api.put(`/plcsignal/signal/${signal.id}`, signal);
  },

  // 删除PLC设备
  deleteDevice: async (id: number): Promise<void> => {
    await api.delete(`/plcsignal/${id}`);
  },

  // 删除PLC信号
  deleteSignal: async (id: number): Promise<void> => {
    await api.delete(`/plcsignal/signal/${id}`);
  },
};
