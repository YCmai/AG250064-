import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { plcSignalService, PlcDevice, PlcSignal } from '../services/plcSignal';

export const usePlcSignalStore = defineStore('plcSignal', () => {
  const devices = ref<PlcDevice[]>([]);
  const signals = ref<PlcSignal[]>([]);
  const selectedDevice = ref<PlcDevice | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const deviceCount = computed(() => devices.value.length);
  const signalCount = computed(() => signals.value.length);

  const fetchDevices = async () => {
    loading.value = true;
    error.value = null;
    try {
      devices.value = await plcSignalService.getAllDevices();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取设备列表失败';
    } finally {
      loading.value = false;
    }
  };

  const fetchSignalsByDevice = async (deviceId: string) => {
    loading.value = true;
    error.value = null;
    try {
      signals.value = await plcSignalService.getSignalsByDevice(deviceId);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取信号列表失败';
    } finally {
      loading.value = false;
    }
  };

  const addDevice = async (device: Omit<PlcDevice, 'id' | 'createdTime' | 'updatedTime'>) => {
    try {
      const id = await plcSignalService.addDevice(device);
      await fetchDevices();
      return id;
    } catch (err) {
      error.value = err instanceof Error ? err.message : '添加设备失败';
      throw err;
    }
  };

  const addSignal = async (signal: Omit<PlcSignal, 'id' | 'createdTime' | 'updatedTime'>) => {
    try {
      const id = await plcSignalService.addSignal(signal);
      if (selectedDevice.value) {
        await fetchSignalsByDevice(selectedDevice.value.id.toString());
      }
      return id;
    } catch (err) {
      error.value = err instanceof Error ? err.message : '添加信号失败';
      throw err;
    }
  };

  const updateDevice = async (device: PlcDevice) => {
    try {
      await plcSignalService.updateDevice(device);
      await fetchDevices();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '更新设备失败';
      throw err;
    }
  };

  const updateSignal = async (signal: PlcSignal) => {
    try {
      await plcSignalService.updateSignal(signal);
      if (selectedDevice.value) {
        await fetchSignalsByDevice(selectedDevice.value.id.toString());
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : '更新信号失败';
      throw err;
    }
  };

  const deleteDevice = async (id: number) => {
    try {
      await plcSignalService.deleteDevice(id);
      await fetchDevices();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '删除设备失败';
      throw err;
    }
  };

  const deleteSignal = async (id: number) => {
    try {
      await plcSignalService.deleteSignal(id);
      if (selectedDevice.value) {
        await fetchSignalsByDevice(selectedDevice.value.id.toString());
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : '删除信号失败';
      throw err;
    }
  };

  const selectDevice = (device: PlcDevice) => {
    selectedDevice.value = device;
  };

  const clearError = () => {
    error.value = null;
  };

  return {
    devices,
    signals,
    selectedDevice,
    loading,
    error,
    deviceCount,
    signalCount,
    fetchDevices,
    fetchSignalsByDevice,
    addDevice,
    addSignal,
    updateDevice,
    updateSignal,
    deleteDevice,
    deleteSignal,
    selectDevice,
    clearError,
  };
});
