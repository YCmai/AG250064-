<template>
  <div class="io-monitor-container">
    <a-row :gutter="16" class="mb-4">
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-statistic :title="t('io.deviceTotal')" :value="ioStore.deviceCount">
            <template #prefix><hdd-outlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-statistic :title="t('io.enabledDevices')" :value="ioStore.enabledDeviceCount" value-style="color: #3f8600">
            <template #prefix><check-circle-outlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-statistic :title="t('io.healthyDevices')" :value="healthyDeviceCount" value-style="color: #1677ff">
            <template #prefix><reload-outlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-statistic :title="t('io.staleDevices')" :value="staleDeviceCount" value-style="color: #faad14">
            <template #prefix><schedule-outlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
    </a-row>

    <a-card :title="t('io.title')" :bordered="false">
      <template #extra>
        <a-space>
          <span v-if="autoRefresh">
            <loading-outlined /> {{ t('common.autoRefreshing') }}
          </span>
          <span v-else>{{ t('common.autoRefreshPaused') }}</span>
          <a-switch v-model:checked="autoRefresh" size="small" />
          <a-button type="primary" @click="openAddDeviceModal">
            <template #icon><PlusOutlined /></template>
            {{ t('io.addDevice') }}
          </a-button>
        </a-space>
      </template>

      <a-collapse v-model:activeKey="activeKey" accordion :bordered="false" style="background: transparent;">
        <a-collapse-panel v-for="device in devicesWithSignals" :key="device.id" :showArrow="false">
          <template #header>
            <div class="device-header">
              <div class="device-info">
                <span class="device-status-indicator" :class="{ on: device.isEnabled, off: !device.isEnabled }"></span>
                <span class="device-name">{{ device.name }}</span>
                <span class="device-ip text-muted">({{ device.ip }}:{{ device.port || '-' }})</span>
                <a-tag :color="getDeviceHealthColor(device)">{{ getDeviceHealthText(device) }}</a-tag>
                <span class="device-meta">{{ t('io.signalTotal') }}: {{ device.signals.length }}</span>
                <span class="device-meta">{{ t('io.pendingTasks') }}: {{ device.pendingTaskCount }}</span>
                <span class="device-meta">{{ t('io.lastSignalUpdate') }}: {{ getLastSignalUpdateText(device) }}</span>
              </div>
              <div class="device-actions" @click.stop>
                <a-space>
                  <a-switch
                    :checked="device.isEnabled"
                    :checked-children="t('io.on')"
                    :un-checked-children="t('io.off')"
                    @change="toggleDevice(device.id, $event)"
                  />
                  <a-button type="link" size="small" @click="editDevice(device)">{{ t('common.edit') }}</a-button>
                  <a-popconfirm :title="t('io.deleteDeviceConfirm')" @confirm="deleteDevice(device.id)">
                    <a-button type="link" danger size="small">{{ t('common.delete') }}</a-button>
                  </a-popconfirm>
                  <a-button type="primary" size="small" ghost @click="showAddSignalModal(device)">{{ t('io.addSignal') }}</a-button>
                </a-space>
              </div>
            </div>
          </template>

          <a-table
            :columns="signalColumns"
            :data-source="device.signals"
            :pagination="false"
            size="small"
            rowKey="id"
            class="signal-table"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'value'">
                <a-tag :color="record.value == 1 ? 'green' : (record.value == 0 ? 'red' : 'default')">
                  {{ record.value == 1 ? t('io.on') : (record.value == 0 ? t('io.off') : t('io.unknown')) }}
                </a-tag>
              </template>
              <template v-else-if="column.key === 'updatedTime'">
                {{ formatDate(record.updatedTime) }}
              </template>
              <template v-else-if="column.key === 'action'">
                <a-space size="small">
                  <a-button size="small" @click="readSignal(record)">{{ t('io.read') }}</a-button>
                  <a-button size="small" type="primary" ghost @click="writeSignal(record, 1)" :disabled="isReadOnly(record)">{{ t('io.writeOne') }}</a-button>
                  <a-button size="small" danger ghost @click="writeSignal(record, 0)" :disabled="isReadOnly(record)">{{ t('io.writeZero') }}</a-button>
                  <a-popconfirm :title="t('io.deleteSignalConfirm')" @confirm="deleteSignal(record.id)">
                    <a-button type="link" danger size="small">{{ t('common.delete') }}</a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-collapse-panel>
      </a-collapse>

      <div v-if="devicesWithSignals.length === 0" class="text-center py-4 text-muted">
        {{ t('io.noDevices') }}
      </div>
    </a-card>

    <a-card :title="t('io.taskList')" :bordered="false" class="mt-4">
      <template #extra>
        <a-button type="link" @click="ioStore.fetchTasks">{{ t('common.reset') }}</a-button>
      </template>
      <a-table
        :columns="taskColumns"
        :data-source="ioStore.tasks"
        :loading="ioStore.loading"
        :pagination="{ pageSize: 10 }"
        rowKey="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="getTaskStatusColor(record.status)">
              {{ getTaskStatusText(record.status) }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'taskDiagnosis'">
            <a-tag :color="getTaskDiagnosisColor(record)">
              {{ getTaskDiagnosisText(record) }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'lastUpdatedTime' || column.key === 'createdTime' || column.key === 'completedTime'">
            {{ formatDate(record[column.key]) }}
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="showAddDeviceModal"
      :title="deviceForm.id ? t('io.editDevice') : t('io.addDevice')"
      :ok-text="t('common.save')"
      :cancel-text="t('common.cancel')"
      @ok="saveDevice"
    >
      <a-form :model="deviceForm" layout="vertical">
        <a-form-item :label="t('io.deviceName')" required>
          <a-input v-model:value="deviceForm.name" :placeholder="t('io.deviceName')" />
        </a-form-item>
        <a-form-item :label="t('io.ipAddress')" required>
          <a-input v-model:value="deviceForm.ip" :placeholder="t('io.ipAddress')" />
        </a-form-item>
        <a-form-item :label="t('io.port')">
          <a-input-number v-model:value="deviceForm.port" :placeholder="t('io.port')" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="showAddSignalModalVisible"
      :title="t('io.addSignal')"
      :ok-text="t('common.save')"
      :cancel-text="t('common.cancel')"
      @ok="saveSignal"
    >
      <a-form :model="signalForm" layout="vertical">
        <a-form-item :label="t('io.signalName')" required>
          <a-input v-model:value="signalForm.name" :placeholder="t('io.signalName')" />
        </a-form-item>
        <a-form-item :label="t('io.address')" required>
          <a-input v-model:value="signalForm.address" placeholder="e.g., DO1" />
          <div class="form-text text-muted">{{ t('io.addressHelp') }}</div>
        </a-form-item>
        <a-form-item :label="t('io.description')">
          <a-textarea v-model:value="signalForm.description" :placeholder="t('io.description')" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, onUnmounted, watch } from 'vue';
import { message, theme } from 'ant-design-vue';
import {
  PlusOutlined,
  HddOutlined,
  CheckCircleOutlined,
  ScheduleOutlined,
  LoadingOutlined,
  ReloadOutlined
} from '@ant-design/icons-vue';
import { useIOMonitorStore } from '../stores/ioMonitor';
import type { IODevice, IOSignal, IOTask } from '../services/ioMonitor';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
const { useToken } = theme;
const { token } = useToken();
const ioStore = useIOMonitorStore();
const activeKey = ref<number[]>([]);
const showAddDeviceModal = ref(false);
const showAddSignalModalVisible = ref(false);
const autoRefresh = ref(false);
const staleThresholdSeconds = 15;
let refreshTimer: number | null = null;

const deviceForm = ref<Partial<IODevice>>({ isEnabled: true });
const signalForm = ref<Partial<IOSignal>>({});
const currentDeviceForSignal = ref<IODevice | null>(null);

const devicesWithSignals = computed(() => {
  return ioStore.devices.map((device) => {
    const signals = ioStore.signals.filter((signal) => signal.deviceId === device.id);
    const tasks = ioStore.tasks.filter((task) => task.deviceIP === device.ip);
    return {
      ...device,
      signals,
      tasks,
      pendingTaskCount: tasks.filter((task) => task.status === 'Pending').length,
      latestTask: tasks.length
        ? [...tasks].sort((a, b) => new Date(b.lastUpdatedTime || b.createdTime || 0).getTime() - new Date(a.lastUpdatedTime || a.createdTime || 0).getTime())[0]
        : null
    };
  });
});

const healthyDeviceCount = computed(() => devicesWithSignals.value.filter((device) => getDeviceHealthStatus(device) === 'healthy').length);
const staleDeviceCount = computed(() => devicesWithSignals.value.filter((device) => getDeviceHealthStatus(device) === 'stale').length);

const signalColumns = computed(() => [
  { title: t('io.signalName'), dataIndex: 'name', key: 'name' },
  { title: t('io.address'), dataIndex: 'address', key: 'address' },
  { title: t('io.description'), dataIndex: 'description', key: 'description' },
  { title: t('plc.currentValue'), dataIndex: 'value', key: 'value', width: 120, align: 'center' },
  { title: t('io.lastSignalUpdate'), dataIndex: 'updatedTime', key: 'updatedTime', width: 180 },
  { title: t('common.operation'), key: 'action', width: 260, align: 'center' }
]);

const taskColumns = computed(() => [
  { title: t('task.taskId'), dataIndex: 'taskId', key: 'taskId' },
  { title: t('task.type'), dataIndex: 'taskType', key: 'taskType' },
  { title: t('io.ipAddress'), dataIndex: 'deviceIP', key: 'deviceIP' },
  { title: t('io.address'), dataIndex: 'signalAddress', key: 'signalAddress' },
  { title: t('plc.currentValue'), dataIndex: 'value', key: 'value' },
  { title: t('task.status'), dataIndex: 'status', key: 'status' },
  { title: t('io.taskDiagnosis'), key: 'taskDiagnosis', width: 150 },
  { title: t('common.createTime'), dataIndex: 'createdTime', key: 'createdTime' },
  { title: t('task.completeTime'), dataIndex: 'completedTime', key: 'completedTime' },
  { title: t('common.updateTime'), dataIndex: 'lastUpdatedTime', key: 'lastUpdatedTime' }
]);

const loadData = async () => {
  await Promise.all([
    ioStore.fetchDevices(),
    ioStore.fetchSignals(),
    ioStore.fetchTasks()
  ]);
};

onMounted(() => {
  loadData();
  startAutoRefresh();
});

onUnmounted(() => {
  stopAutoRefresh();
});

const startAutoRefresh = () => {
  stopAutoRefresh();
  if (autoRefresh.value) {
    refreshTimer = window.setInterval(() => {
      ioStore.fetchSignals();
      ioStore.fetchTasks();
    }, 3000);
  }
};

const stopAutoRefresh = () => {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
};

watch(autoRefresh, (val) => {
  if (val) startAutoRefresh();
  else stopAutoRefresh();
});

const editDevice = (device: IODevice) => {
  deviceForm.value = { ...device };
  showAddDeviceModal.value = true;
};

const openAddDeviceModal = () => {
  deviceForm.value = { isEnabled: true };
  showAddDeviceModal.value = true;
};

const saveDevice = async () => {
  try {
    if (deviceForm.value.id) {
      const updatePayload = {
        id: deviceForm.value.id,
        name: deviceForm.value.name,
        ip: deviceForm.value.ip,
        port: deviceForm.value.port,
        isEnabled: deviceForm.value.isEnabled
      };
      await ioStore.updateDevice(updatePayload as IODevice);
    } else {
      const addPayload = {
        Name: deviceForm.value.name,
        IP: deviceForm.value.ip,
        IsEnabled: deviceForm.value.isEnabled
      };
      await ioStore.addDevice(addPayload as any);
    }
    message.success(t('common.success'));
    showAddDeviceModal.value = false;
    deviceForm.value = { isEnabled: true };
  } catch (error) {
    console.error(error);
    message.error(t('common.fail'));
  }
};

const deleteDevice = async (id: number) => {
  try {
    await ioStore.deleteDevice(id);
    message.success(t('common.success'));
  } catch (_error) {
    message.error(t('common.fail'));
  }
};

const toggleDevice = async (id: number, isEnabled: boolean) => {
  try {
    await ioStore.toggleDevice(id, isEnabled);
    message.success(t('common.success'));
  } catch (_error) {
    message.error(t('common.fail'));
  }
};

const showAddSignalModal = (device: IODevice) => {
  currentDeviceForSignal.value = device;
  signalForm.value = { deviceId: device.id };
  showAddSignalModalVisible.value = true;
};

const saveSignal = async () => {
  try {
    if (!currentDeviceForSignal.value) return;

    const signalPayload = {
      DeviceId: currentDeviceForSignal.value.id,
      Name: signalForm.value.name,
      Address: signalForm.value.address,
      Description: signalForm.value.description || '',
      Value: 0
    };

    await ioStore.addSignal(signalPayload as any);
    message.success(t('common.success'));
    showAddSignalModalVisible.value = false;
    signalForm.value = {};
    await loadData();
  } catch (error) {
    console.error(error);
    message.error(t('common.fail'));
  }
};

const isReadOnly = (record: IOSignal) => {
  return Boolean(record.address && record.address.toUpperCase().startsWith('DI'));
};

const formatDate = (dateStr?: string) => {
  if (!dateStr) return '-';
  const date = new Date(dateStr);
  if (Number.isNaN(date.getTime())) return '-';
  return date.toLocaleString();
};

const getLatestSignalTime = (device: { signals: IOSignal[] }) => {
  const timestamps = device.signals
    .map((signal) => signal.updatedTime || signal.createdTime)
    .filter(Boolean)
    .map((value) => new Date(value as string).getTime())
    .filter((value) => !Number.isNaN(value));

  return timestamps.length ? new Date(Math.max(...timestamps)) : null;
};

const getDeviceHealthStatus = (device: { isEnabled: boolean; signals: IOSignal[] }) => {
  if (!device.isEnabled) return 'disabled';
  const latest = getLatestSignalTime(device);
  if (!latest) return 'no-signal';
  const ageSeconds = Math.floor((Date.now() - latest.getTime()) / 1000);
  return ageSeconds <= staleThresholdSeconds ? 'healthy' : 'stale';
};

const getDeviceHealthColor = (device: { isEnabled: boolean; signals: IOSignal[] }) => {
  const status = getDeviceHealthStatus(device);
  if (status === 'healthy') return 'success';
  if (status === 'stale') return 'warning';
  if (status === 'disabled') return 'default';
  return 'error';
};

const getDeviceHealthText = (device: { isEnabled: boolean; signals: IOSignal[] }) => {
  const status = getDeviceHealthStatus(device);
  if (status === 'healthy') return t('io.healthy');
  if (status === 'stale') return t('io.stale');
  if (status === 'disabled') return t('plc.deviceDisabled');
  return t('io.noSignal');
};

const getLastSignalUpdateText = (device: { signals: IOSignal[] }) => {
  const latest = getLatestSignalTime(device);
  return latest ? latest.toLocaleString() : t('common.fail');
};

const deleteSignal = async (id: number) => {
  try {
    await ioStore.deleteSignal(id);
    message.success(t('common.success'));
  } catch (_error) {
    message.error(t('common.fail'));
  }
};

const readSignal = async (signal: IOSignal) => {
  try {
    const device = ioStore.devices.find((item) => item.id === signal.deviceId);
    if (!device) {
      message.error(t('common.fail'));
      return;
    }
    const value = await ioStore.readSignal(device.ip, signal.address);
    ioStore.fetchSignals();
    message.success(`${t('io.read')} ${t('common.success')}: ${value}`);
  } catch (_error) {
    message.error(t('common.fail'));
  }
};

const writeSignal = async (signal: IOSignal, value: number) => {
  try {
    const device = ioStore.devices.find((item) => item.id === signal.deviceId);
    if (!device) {
      message.error(t('common.fail'));
      return;
    }
    await ioStore.writeSignal(device.ip, signal.address, value);
    message.success(t('common.success'));
    ioStore.fetchSignals();
  } catch (_error) {
    message.error(t('common.fail'));
  }
};

const getTaskStatusColor = (status?: string) => {
  if (status === 'Completed') return 'green';
  if (status === 'Pending') return 'orange';
  if (status === 'Failed') return 'red';
  return 'default';
};

const getTaskStatusText = (status?: string) => {
  if (!status) return t('task.unknown');
  if (status === 'Completed') return t('task.completed');
  if (status === 'Pending') return t('task.pending');
  if (status === 'Failed') return t('common.fail');
  return status;
};

const getTaskDiagnosisText = (task: IOTask) => {
  if (task.status === 'Failed') return t('io.taskFailed');
  if (task.status === 'Completed') return t('io.taskCompleted');
  return t('io.taskPending');
};

const getTaskDiagnosisColor = (task: IOTask) => {
  if (task.status === 'Failed') return 'error';
  if (task.status === 'Completed') return 'success';
  return 'processing';
};
</script>

<style scoped>
.io-monitor-container {
  padding: 16px;
  background-color: v-bind('token.colorBgLayout');
  min-height: 100vh;
}

.device-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
  padding: 4px 0;
}

.device-info {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.device-name {
  font-weight: 600;
  font-size: 16px;
  color: v-bind('token.colorText');
}

.device-ip,
.device-meta {
  font-size: 14px;
  color: v-bind('token.colorTextSecondary');
}

.device-status-indicator {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background-color: #d9d9d9;
}

.device-status-indicator.on {
  background-color: #52c41a;
  box-shadow: 0 0 4px #52c41a;
}

.device-status-indicator.off {
  background-color: #ff4d4f;
  box-shadow: 0 0 4px #ff4d4f;
}

:deep(.ant-collapse) {
  background-color: transparent;
}

:deep(.ant-collapse-item) {
  border-bottom: 1px solid v-bind('token.colorBorderSecondary');
}

:deep(.ant-collapse-header) {
  background-color: v-bind('token.colorBgContainer') !important;
  color: v-bind('token.colorText') !important;
}

:deep(.ant-collapse-content) {
  background-color: v-bind('token.colorBgContainer');
  border-top: 1px solid v-bind('token.colorBorderSecondary');
  color: v-bind('token.colorText');
}
</style>

