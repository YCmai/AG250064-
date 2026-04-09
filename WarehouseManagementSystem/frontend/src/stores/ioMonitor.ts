import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { ioMonitorService, IODevice, IOSignal, IOTask } from '../services/ioMonitor';

export const useIOMonitorStore = defineStore('ioMonitor', () => {
  const devices = ref<IODevice[]>([]);
  const signals = ref<IOSignal[]>([]);
  const tasks = ref<IOTask[]>([]);
  const selectedDevice = ref<IODevice | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const deviceCount = computed(() => devices.value.length);
  const enabledDeviceCount = computed(() => devices.value.filter(d => d.isEnabled).length);
  const signalCount = computed(() => signals.value.length);
  const taskCount = computed(() => tasks.value.length);

  const fetchDevices = async () => {
    loading.value = true;
    error.value = null;
    try {
      const data = await ioMonitorService.getAllDevices();
      console.log('Fetched Devices:', data);
      // 处理 PascalCase 映射问题
      devices.value = data.map((d: any) => ({
        id: d.id || d.Id,
        name: d.name || d.Name,
        ip: d.ip || d.IP,
        port: d.port || d.Port,
        isEnabled: d.isEnabled !== undefined ? d.isEnabled : d.IsEnabled,
        createdTime: d.createdTime || d.CreatedTime,
        updatedTime: d.updatedTime || d.UpdatedTime
      }));
    } catch (err) {
      console.error('Fetch Devices Error:', err);
      error.value = err instanceof Error ? err.message : '获取设备列表失败';
    } finally {
      loading.value = false;
    }
  };

  const fetchSignals = async () => {
    loading.value = true;
    error.value = null;
    try {
      const data = await ioMonitorService.getLatestSignals();
      console.log('Fetched Signals:', data);
      // 处理 PascalCase 映射问题
      signals.value = data.map((s: any) => ({
        id: s.id || s.Id,
        deviceId: s.deviceId || s.DeviceId,
        name: s.name || s.Name,
        address: s.address || s.Address,
        value: s.value !== undefined ? s.value : s.Value,
        description: s.description || s.Description,
        createdTime: s.createdTime || s.CreatedTime,
        updatedTime: s.updatedTime || s.UpdatedTime
      }));
    } catch (err) {
      console.error('Fetch Signals Error:', err);
      error.value = err instanceof Error ? err.message : '获取信号列表失败';
    } finally {
      loading.value = false;
    }
  };

  const fetchTasks = async () => {
    loading.value = true;
    error.value = null;
    try {
      const data = await ioMonitorService.getIOTasks();
      console.log('Fetched Tasks:', data);
      tasks.value = data.map((t: any) => ({
        id: t.id || t.Id,
        taskType: t.taskType || t.TaskType,
        deviceIP: t.deviceIP || t.DeviceIP,
        signalAddress: t.signalAddress || t.SignalAddress,
        value: t.value !== undefined ? t.value : t.Value,
        taskId: t.taskId || t.TaskId,
        status: t.status || t.Status,
        createdTime: t.createdTime || t.CreatedTime,
        completedTime: t.completedTime || t.CompletedTime,
        lastUpdatedTime: t.lastUpdatedTime || t.LastUpdatedTime
      }));
    } catch (err) {
      console.error('Fetch Tasks Error:', err);
      error.value = err instanceof Error ? err.message : '获取任务列表失败';
    } finally {
      loading.value = false;
    }
  };

  const addDevice = async (device: Omit<IODevice, 'id' | 'createdTime' | 'updatedTime'>) => {
    try {
      await ioMonitorService.addDevice(device);
      await fetchDevices();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '添加设备失败';
      throw err;
    }
  };

  const addSignal = async (signal: Omit<IOSignal, 'id' | 'createdTime' | 'updatedTime'>) => {
    try {
      await ioMonitorService.addSignal(signal);
      await fetchSignals();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '添加信号失败';
      throw err;
    }
  };

  const updateDevice = async (device: IODevice) => {
    try {
      await ioMonitorService.updateDevice(device);
      await fetchDevices();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '更新设备失败';
      throw err;
    }
  };

  const deleteDevice = async (id: number) => {
    try {
      await ioMonitorService.deleteDevice(id);
      await fetchDevices();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '删除设备失败';
      throw err;
    }
  };

  const deleteSignal = async (id: number) => {
    try {
      await ioMonitorService.deleteSignal(id);
      await fetchSignals();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '删除信号失败';
      throw err;
    }
  };

  const toggleDevice = async (id: number, isEnabled: boolean) => {
    try {
      await ioMonitorService.toggleDevice(id, isEnabled);
      await fetchDevices();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '切换设备状态失败';
      throw err;
    }
  };

  const readSignal = async (ip: string, address: string) => {
    try {
      return await ioMonitorService.readSignal(ip, address);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '读取信号失败';
      throw err;
    }
  };

  const writeSignal = async (ip: string, address: string, value: number) => {
    try {
      return await ioMonitorService.writeSignal(ip, address, value);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '写入信号失败';
      throw err;
    }
  };

  const addTask = async (task: Omit<IOTask, 'id' | 'createdTime'>) => {
    try {
      await ioMonitorService.addIOTask(task);
      await fetchTasks();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '添加任务失败';
      throw err;
    }
  };

  const selectDevice = (device: IODevice) => {
    selectedDevice.value = device;
  };

  const clearError = () => {
    error.value = null;
  };

  return {
    devices,
    signals,
    tasks,
    selectedDevice,
    loading,
    error,
    deviceCount,
    enabledDeviceCount,
    signalCount,
    taskCount,
    fetchDevices,
    fetchSignals,
    fetchTasks,
    addDevice,
    addSignal,
    updateDevice,
    deleteDevice,
    deleteSignal,
    toggleDevice,
    readSignal,
    writeSignal,
    addTask,
    selectDevice,
    clearError,
  };
});
