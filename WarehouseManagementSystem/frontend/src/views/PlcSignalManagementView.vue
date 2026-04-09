<template>
  <div class="plc-signal-management">
    <div class="view-header">
      <div class="header-left">
        <h3>{{ t('plc.title') }}</h3>
      </div>
    </div>

    <a-row :gutter="16" class="summary-row">
      <a-col :xs="24" :sm="12" :md="6">
        <a-card size="small">
          <a-statistic :title="t('plc.totalDevices')" :value="devices.length" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card size="small">
          <a-statistic :title="t('plc.enabledDevices')" :value="enabledDeviceCount" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card size="small">
          <a-statistic :title="t('plc.healthyDevices')" :value="healthyDeviceCount" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card size="small">
          <a-statistic :title="t('plc.staleDevices')" :value="staleDeviceCount" />
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="16" class="h-100">
      <a-col :span="6" class="h-100">
        <a-card :title="t('plc.deviceList')" class="h-100 device-list-card" :bodyStyle="{ padding: '0', height: 'calc(100% - 57px)', overflowY: 'auto' }">
          <template #extra>
            <a-button type="primary" size="small" @click="showDeviceModal()">
              <plus-outlined /> {{ t('common.add') }}
            </a-button>
          </template>

          <a-list item-layout="vertical" :data-source="devices">
            <template #renderItem="{ item }">
              <a-list-item
                class="device-item"
                :class="{ active: selectedDevice?.id === item.id }"
                @click="selectDevice(item)"
              >
                <template #actions>
                  <a-space>
                    <a-button type="text" size="small" @click.stop="showDeviceModal(item)">
                      <template #icon><edit-outlined /></template>
                    </a-button>
                    <a-popconfirm
                      :title="t('plc.confirmDeleteDevice')"
                      @confirm="deleteDevice(item.id)"
                      :ok-text="t('plc.yes')"
                      :cancel-text="t('plc.no')"
                    >
                      <a-button type="text" danger size="small" @click.stop>
                        <template #icon><delete-outlined /></template>
                      </a-button>
                    </a-popconfirm>
                  </a-space>
                </template>

                <div class="device-content">
                  <div class="device-header-row">
                    <span class="device-ip">{{ item.ipAddress }}</span>
                    <a-tag :color="item.isEnabled ? 'success' : 'default'" class="status-tag">
                      {{ item.isEnabled ? t('plc.enabled') : t('plc.disabled') }}
                    </a-tag>
                    <a-tag :color="getDeviceHealthColor(item)">
                      {{ getDeviceHealthText(item) }}
                    </a-tag>
                  </div>

                  <div class="device-remark" v-if="item.remark">
                    {{ item.remark }}
                  </div>

                  <div class="device-tags">
                    <a-tag color="blue" v-if="item.brand">{{ item.brand }}</a-tag>
                    <a-tag v-if="item.port"><global-outlined /> {{ item.port }}</a-tag>
                    <a-tag color="orange" v-if="item.moduleAddress"><database-outlined /> {{ item.moduleAddress }}</a-tag>
                    <a-tag color="cyan" v-if="item.stationPoint"><environment-outlined /> {{ item.stationPoint }}</a-tag>
                  </div>

                  <div class="device-meta-row">
                    <span>{{ t('plc.lastSignalUpdate') }}: {{ getDeviceLastUpdateText(item) }}</span>
                  </div>
                  <div class="device-meta-row">
                    <span>{{ t('plc.abnormalSignalCount') }}: {{ getDeviceAbnormalSignalCount(item) }}</span>
                  </div>
                </div>
              </a-list-item>
            </template>
          </a-list>
        </a-card>
      </a-col>

      <a-col :span="18" class="h-100">
        <a-card class="h-100" :title="selectedDevice ? `${selectedDevice.remark || selectedDevice.ipAddress} - ${t('plc.signalList')}` : t('plc.selectDevice')">
          <template #extra v-if="selectedDevice">
            <a-space>
              <a-button @click="showDeviceModal(selectedDevice)">
                <edit-outlined /> {{ t('plc.editDevice') }}
              </a-button>
              <a-button type="primary" @click="showSignalModal()">
                <plus-outlined /> {{ t('plc.addSignal') }}
              </a-button>
            </a-space>
          </template>

          <div v-if="selectedDevice" class="selected-device-summary">
            <a-space wrap>
              <a-tag :color="selectedDevice.isEnabled ? 'success' : 'default'">
                {{ selectedDevice.isEnabled ? t('plc.deviceEnabled') : t('plc.deviceDisabled') }}
              </a-tag>
              <a-tag :color="getDeviceHealthColor(selectedDevice)">
                {{ getDeviceHealthText(selectedDevice) }}
              </a-tag>
              <span>{{ t('plc.lastSignalUpdate') }}: {{ getDeviceLastUpdateText(selectedDevice) }}</span>
              <span>{{ t('plc.abnormalSignalCount') }}: {{ getDeviceAbnormalSignalCount(selectedDevice) }}</span>
            </a-space>
          </div>

          <div v-if="!selectedDevice" class="empty-state">
            <a-empty :description="t('plc.selectDeviceTip')" />
          </div>

          <a-table
            v-else
            :columns="signalColumns"
            :data-source="signals"
            :loading="loadingSignals"
            row-key="id"
            :pagination="pagination"
            @change="handleTableChange"
            size="middle"
            :scroll="{ y: 'calc(100vh - 340px)' }"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'currentValue'">
                <a-tag v-if="isSignalAbnormal(record)" color="error">
                  {{ record.currentValue || t('common.unknown') }}
                </a-tag>
                <span v-else>{{ record.currentValue || '-' }}</span>
              </template>

              <template v-else-if="column.key === 'lastUpdateTime'">
                {{ formatTime(record.lastUpdateTime) }}
              </template>

              <template v-else-if="column.key === 'signalStatus'">
                <a-tag :color="isSignalAbnormal(record) ? 'error' : 'success'">
                  {{ isSignalAbnormal(record) ? t('plc.signalAbnormal') : t('plc.signalNormal') }}
                </a-tag>
              </template>

              <template v-else-if="column.key === 'action'">
                <a-space>
                  <a-button type="link" size="small" @click="showSignalModal(record)">{{ t('common.edit') }}</a-button>
                  <a-popconfirm
                    :title="t('plc.confirmDeleteSignal')"
                    @confirm="deleteSignal(record.id)"
                    :ok-text="t('plc.yes')"
                    :cancel-text="t('plc.no')"
                  >
                    <a-button type="link" danger size="small">{{ t('common.delete') }}</a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>
    </a-row>

    <a-modal
      v-model:open="deviceModalVisible"
      :title="editingDevice ? t('plc.editDevice') : t('plc.addDevice')"
      @ok="handleDeviceSubmit"
      :confirmLoading="submitting"
      width="600px"
    >
      <a-form :model="deviceForm" layout="vertical">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('plc.ipAddress')" name="ipAddress" :rules="[{ required: true, message: t('plc.inputIp') }]">
              <a-input v-model:value="deviceForm.ipAddress" placeholder="例如: 192.168.1.10" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('plc.port')" name="port">
              <a-input-number v-model:value="deviceForm.port" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('plc.brand')" name="brand">
              <a-select v-model:value="deviceForm.brand">
                <a-select-option value="西门子">西门子</a-select-option>
                <a-select-option value="欧姆龙">欧姆龙</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('plc.status')" name="isEnabled">
              <a-switch v-model:checked="deviceForm.isEnabled" :checked-children="t('plc.enabled')" :un-checked-children="t('plc.disabled')" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('plc.dbAddress')" name="moduleAddress">
              <a-input v-model:value="deviceForm.moduleAddress" placeholder="例如: DB10001" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('plc.stationPoint')" name="stationPoint">
              <a-input v-model:value="deviceForm.stationPoint" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('plc.signalRequestPoint')" name="signalRequestPoint">
              <a-input v-model:value="deviceForm.signalRequestPoint" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('plc.leaveResetPoint')" name="leaveResetPoint">
              <a-input v-model:value="deviceForm.leaveResetPoint" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-form-item :label="t('plc.remark')" name="remark">
          <a-input v-model:value="deviceForm.remark" placeholder="设备名称或备注" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="signalModalVisible"
      :title="editingSignal ? t('plc.editSignal') : t('plc.addSignal')"
      @ok="handleSignalSubmit"
      :confirmLoading="submitting"
    >
      <a-form :model="signalForm" layout="vertical">
        <a-form-item :label="t('plc.signalName')" name="name" :rules="[{ required: true, message: t('plc.inputName') }]">
          <a-input v-model:value="signalForm.name" />
        </a-form-item>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('plc.offset')" name="offset">
              <a-input v-model:value="signalForm.offset" placeholder="例如: 0.0" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('plc.dbBlock')" name="plcTypeDb">
              <a-input v-model:value="signalForm.plcTypeDb" placeholder="例如: DB1" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('plc.dataType')" name="dataType">
              <a-select v-model:value="signalForm.dataType">
                <a-select-option value="Bool">Bool</a-select-option>
                <a-select-option value="Int">Int</a-select-option>
                <a-select-option value="DInt">DInt</a-select-option>
                <a-select-option value="Real">Real</a-select-option>
                <a-select-option value="String">String</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('plc.writer')" name="writer">
              <a-select v-model:value="signalForm.writer">
                <a-select-option value="AGV">AGV</a-select-option>
                <a-select-option value="PLC">PLC</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('plc.currentValue')" name="currentValue">
              <a-input v-model:value="signalForm.currentValue" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-form-item :label="t('plc.remark')" name="remark">
          <a-textarea v-model:value="signalForm.remark" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive, computed } from 'vue';
import { message } from 'ant-design-vue';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  EnvironmentOutlined,
  DatabaseOutlined,
  GlobalOutlined
} from '@ant-design/icons-vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
const staleThresholdSeconds = 15;

interface PlcDevice {
  id: number;
  ipAddress: string;
  port?: number;
  isEnabled?: boolean;
  brand?: string;
  stationPoint?: string;
  signalRequestPoint?: string;
  leaveResetPoint?: string;
  moduleAddress?: string;
  remark?: string;
  lastSignalUpdateTime?: string | null;
}

interface PlcSignal {
  id: number;
  name: string;
  offset?: string;
  dataType?: string;
  writer?: string;
  currentValue?: string;
  remark?: string;
  plcDeviceId: string;
  plcTypeDb?: string;
  lastUpdateTime?: string | null;
}

const devices = ref<PlcDevice[]>([]);
const signals = ref<PlcSignal[]>([]);
const allSignals = ref<PlcSignal[]>([]);
const selectedDevice = ref<PlcDevice | null>(null);
const loadingSignals = ref(false);
const submitting = ref(false);

const pagination = reactive({
  current: 1,
  pageSize: 15,
  showSizeChanger: true,
  showQuickJumper: true
});

const deviceModalVisible = ref(false);
const editingDevice = ref<PlcDevice | null>(null);
const deviceForm = reactive({
  ipAddress: '',
  port: 102,
  isEnabled: true,
  brand: '西门子',
  stationPoint: '',
  signalRequestPoint: '',
  leaveResetPoint: '',
  moduleAddress: '',
  remark: ''
});

const signalModalVisible = ref(false);
const editingSignal = ref<PlcSignal | null>(null);
const signalForm = reactive({
  name: '',
  offset: '',
  dataType: 'Bool',
  writer: 'PLC',
  currentValue: '',
  remark: '',
  plcTypeDb: ''
});

const signalColumns = computed(() => [
  { title: 'ID', dataIndex: 'id', width: 60 },
  { title: t('plc.dataType'), dataIndex: 'dataType', width: 100 },
  { title: t('plc.offset'), dataIndex: 'offset', width: 110 },
  { title: t('plc.dbBlock'), dataIndex: 'plcTypeDb', width: 100 },
  { title: t('plc.signalName'), dataIndex: 'name', width: 180 },
  { title: t('plc.writer'), dataIndex: 'writer', width: 100 },
  { title: t('plc.currentValue'), key: 'currentValue', width: 160 },
  { title: t('plc.lastSignalUpdate'), key: 'lastUpdateTime', width: 180 },
  { title: t('plc.signalStatus'), key: 'signalStatus', width: 120 },
  { title: t('common.remark'), dataIndex: 'remark' },
  { title: t('common.operation'), key: 'action', width: 130, align: 'center' }
]);

const API_BASE = '/api/plcsignal';
const API_STATUS = '/api/plc-signal-status';

const enabledDeviceCount = computed(() => devices.value.filter((item) => item.isEnabled).length);
const healthyDeviceCount = computed(() => devices.value.filter((item) => getDeviceHealthStatus(item) === 'healthy').length);
const staleDeviceCount = computed(() => devices.value.filter((item) => getDeviceHealthStatus(item) === 'stale').length);

const normalizeSignal = (raw: any): PlcSignal => ({
  id: raw?.id ?? raw?.Id ?? 0,
  name: raw?.name ?? raw?.Name ?? '',
  offset: raw?.offset ?? raw?.Offset ?? raw?.address ?? raw?.Address ?? '',
  dataType: raw?.dataType ?? raw?.DataType ?? '',
  writer: raw?.writer ?? raw?.Writer ?? '',
  currentValue: String(raw?.currentValue ?? raw?.CurrentValue ?? raw?.value ?? raw?.Value ?? ''),
  remark: raw?.remark ?? raw?.Remark ?? '',
  plcDeviceId: raw?.plcDeviceId ?? raw?.PlcDeviceId ?? '',
  plcTypeDb: raw?.plcTypeDb ?? raw?.PLCTypeDb ?? raw?.dbBlock ?? raw?.DbBlock ?? '',
  lastUpdateTime: raw?.lastUpdateTime ?? raw?.LastUpdateTime ?? null
});

const isSignalAbnormal = (signal: PlcSignal) => {
  const value = (signal.currentValue || '').toString().toLowerCase();
  return value.includes('网络连接失败') || value.includes('network') || value.includes('fail') || value.includes('error');
};

const getSignalsByDevice = (device: PlcDevice) => {
  return allSignals.value.filter(
    (signal) => signal.plcDeviceId === device.ipAddress && (signal.plcTypeDb || '') === (device.moduleAddress || '')
  );
};

const getDeviceLastUpdate = (device: PlcDevice) => {
  if (device.lastSignalUpdateTime) {
    return new Date(device.lastSignalUpdateTime);
  }

  const signalTimes = getSignalsByDevice(device)
    .map((signal) => signal.lastUpdateTime)
    .filter(Boolean)
    .map((value) => new Date(value as string).getTime())
    .filter((value) => !Number.isNaN(value));

  return signalTimes.length ? new Date(Math.max(...signalTimes)) : null;
};

const getDeviceHealthStatus = (device: PlcDevice) => {
  if (!device.isEnabled) return 'disabled';
  const lastUpdate = getDeviceLastUpdate(device);
  if (!lastUpdate) return 'no-signal';
  const ageSeconds = Math.floor((Date.now() - lastUpdate.getTime()) / 1000);
  return ageSeconds <= staleThresholdSeconds ? 'healthy' : 'stale';
};

const getDeviceHealthColor = (device: PlcDevice) => {
  const status = getDeviceHealthStatus(device);
  if (status === 'healthy') return 'success';
  if (status === 'stale') return 'warning';
  if (status === 'disabled') return 'default';
  return 'error';
};

const getDeviceHealthText = (device: PlcDevice) => {
  const status = getDeviceHealthStatus(device);
  if (status === 'healthy') return t('plc.deviceHealthy');
  if (status === 'stale') return t('plc.deviceStale');
  if (status === 'disabled') return t('plc.deviceDisabled');
  return t('plc.deviceNoSignal');
};

const getDeviceLastUpdateText = (device: PlcDevice) => {
  const lastUpdate = getDeviceLastUpdate(device);
  return lastUpdate ? lastUpdate.toLocaleString() : t('common.unknown');
};

const getDeviceAbnormalSignalCount = (device: PlcDevice) => {
  return getSignalsByDevice(device).filter(isSignalAbnormal).length;
};

const formatTime = (value?: string | null) => {
  if (!value) return '-';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : date.toLocaleString();
};

const fetchDevices = async () => {
  try {
    const res = await fetch(API_BASE);
    const data = await res.json();
    if (data.success) {
      devices.value = data.data || [];
    } else {
      message.error(data.message || t('api.fetchFail'));
    }
  } catch (_error) {
    message.error(t('api.fetchFail'));
  }
};

const fetchAllSignalDiagnostics = async () => {
  try {
    const res = await fetch(`${API_STATUS}/signals`);
    const data = await res.json();
    if (data.success) {
      allSignals.value = (data.data || []).map(normalizeSignal);
    }
  } catch (_error) {
    allSignals.value = [];
  }
};

const selectDevice = async (device: PlcDevice) => {
  selectedDevice.value = device;
  pagination.current = 1;
  loadingSignals.value = true;
  try {
    const dbBlock = device.moduleAddress || '';
    const res = await fetch(`${API_BASE}/signals/${device.ipAddress}?dbBlock=${encodeURIComponent(dbBlock)}`);
    const data = await res.json();
    if (data.success) {
      signals.value = (data.data || []).map(normalizeSignal);
    } else {
      message.error(data.message || t('api.fetchFail'));
      signals.value = [];
    }
  } catch (_error) {
    message.error(t('api.fetchFail'));
    signals.value = [];
  } finally {
    loadingSignals.value = false;
  }
};

const showDeviceModal = (device?: PlcDevice) => {
  if (device) {
    editingDevice.value = device;
    deviceForm.ipAddress = device.ipAddress;
    deviceForm.port = device.port || 102;
    deviceForm.isEnabled = device.isEnabled !== false;
    deviceForm.brand = device.brand || '西门子';
    deviceForm.stationPoint = device.stationPoint || '';
    deviceForm.signalRequestPoint = device.signalRequestPoint || '';
    deviceForm.leaveResetPoint = device.leaveResetPoint || '';
    deviceForm.moduleAddress = device.moduleAddress || '';
    deviceForm.remark = device.remark || '';
  } else {
    editingDevice.value = null;
    deviceForm.ipAddress = '';
    deviceForm.port = 102;
    deviceForm.isEnabled = true;
    deviceForm.brand = '西门子';
    deviceForm.stationPoint = '';
    deviceForm.signalRequestPoint = '';
    deviceForm.leaveResetPoint = '';
    deviceForm.moduleAddress = '';
    deviceForm.remark = '';
  }
  deviceModalVisible.value = true;
};

const handleTableChange = (pag: any) => {
  pagination.current = pag.current;
  pagination.pageSize = pag.pageSize;
};

const handleDeviceSubmit = async () => {
  if (!deviceForm.ipAddress) {
    message.warning(t('plc.inputIp'));
    return;
  }

  submitting.value = true;
  try {
    const url = editingDevice.value ? `${API_BASE}/${editingDevice.value.id}` : `${API_BASE}/device`;
    const method = editingDevice.value ? 'PUT' : 'POST';
    const payload = { ...deviceForm, id: editingDevice.value?.id };

    const res = await fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    const data = await res.json();
    if (data.success) {
      message.success(t('common.success'));
      deviceModalVisible.value = false;
      await fetchDevices();
      await fetchAllSignalDiagnostics();
    } else {
      message.error(data.message || t('common.fail'));
    }
  } catch (_error) {
    message.error(t('common.fail'));
  } finally {
    submitting.value = false;
  }
};

const deleteDevice = async (id: number) => {
  try {
    const res = await fetch(`${API_BASE}/${id}`, { method: 'DELETE' });
    const data = await res.json();
    if (data.success) {
      message.success(t('common.success'));
      if (selectedDevice.value?.id === id) {
        selectedDevice.value = null;
        signals.value = [];
      }
      await fetchDevices();
      await fetchAllSignalDiagnostics();
    } else {
      message.error(data.message || t('common.fail'));
    }
  } catch (_error) {
    message.error(t('common.fail'));
  }
};

const showSignalModal = (signal?: PlcSignal) => {
  if (signal) {
    editingSignal.value = signal;
    signalForm.name = signal.name;
    signalForm.offset = signal.offset || '';
    signalForm.dataType = signal.dataType || 'Bool';
    signalForm.writer = signal.writer || 'PLC';
    signalForm.currentValue = signal.currentValue || '';
    signalForm.remark = signal.remark || '';
    signalForm.plcTypeDb = signal.plcTypeDb || '';
  } else {
    editingSignal.value = null;
    signalForm.name = '';
    signalForm.offset = '';
    signalForm.dataType = 'Bool';
    signalForm.writer = 'PLC';
    signalForm.currentValue = '';
    signalForm.remark = '';
    signalForm.plcTypeDb = '';
  }
  signalModalVisible.value = true;
};

const handleSignalSubmit = async () => {
  if (!selectedDevice.value) return;

  submitting.value = true;
  try {
    const url = editingSignal.value ? `${API_BASE}/signal/${editingSignal.value.id}` : `${API_BASE}/signal`;
    const method = editingSignal.value ? 'PUT' : 'POST';
    const payload = {
      ...signalForm,
      id: editingSignal.value?.id,
      plcDeviceId: selectedDevice.value.ipAddress
    };

    const res = await fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    const data = await res.json();
    if (data.success) {
      message.success(t('common.success'));
      signalModalVisible.value = false;
      await selectDevice(selectedDevice.value);
      await fetchAllSignalDiagnostics();
    } else {
      message.error(data.message || t('common.fail'));
    }
  } catch (_error) {
    message.error(t('common.fail'));
  } finally {
    submitting.value = false;
  }
};

const deleteSignal = async (id: number) => {
  try {
    const res = await fetch(`${API_BASE}/signal/${id}`, { method: 'DELETE' });
    const data = await res.json();
    if (data.success) {
      message.success(t('common.success'));
      if (selectedDevice.value) {
        await selectDevice(selectedDevice.value);
      }
      await fetchAllSignalDiagnostics();
    } else {
      message.error(data.message || t('common.fail'));
    }
  } catch (_error) {
    message.error(t('common.fail'));
  }
};

onMounted(async () => {
  await fetchDevices();
  await fetchAllSignalDiagnostics();
});
</script>

<style scoped>
.view-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 16px;
  margin-bottom: 16px;
  border-bottom: 1px solid #f0f0f0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.summary-row {
  margin-bottom: 16px;
}

.plc-signal-management {
  height: calc(100vh - 100px);
  padding: 16px;
  display: flex;
  flex-direction: column;
}

.h-100 {
  height: 100%;
}

.device-list-card :deep(.ant-card-body) {
  padding: 0;
  height: calc(100% - 57px);
  overflow-y: auto;
}

.device-item {
  padding: 12px 16px;
  cursor: pointer;
  transition: background-color 0.2s;
  border-bottom: 1px solid #f0f0f0;
}

.device-item:hover {
  background-color: #f5f5f5;
}

.device-item.active {
  background-color: #e6f7ff;
  border-right: 3px solid #1890ff;
}

.device-content {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.device-header-row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.device-ip {
  font-weight: bold;
  font-size: 14px;
  color: #1890ff;
}

.device-remark {
  font-size: 12px;
  color: #666;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.device-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.device-meta-row {
  font-size: 12px;
  color: #666;
}

.selected-device-summary {
  margin-bottom: 12px;
  color: #666;
}

.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100%;
  color: #999;
}
</style>
