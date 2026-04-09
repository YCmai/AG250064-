<template>
  <div class="plc-signal-display" :class="{ 'fullscreen-mode': isFullscreen }">
    <div class="view-header" v-if="!isFullscreen">
      <div class="header-left">
        <h3>{{ t('plc.monitorTitle') }}</h3>
        <span class="refresh-info">
          <a-tag :color="isAutoRefreshPaused ? 'default' : 'processing'">
            <template #icon>
              <reload-outlined :spin="!isAutoRefreshPaused" />
            </template>
            {{ isAutoRefreshPaused ? t('common.autoRefreshPaused') : t('common.autoRefreshing') }}
          </a-tag>
          <span class="time-text">{{ t('common.updateTime') }}: {{ lastRefreshTime }}</span>
        </span>
      </div>

      <div class="header-right">
        <a-space>
          <a-button @click="toggleFullscreen">
            <template #icon>
              <fullscreen-outlined v-if="!isFullscreen" />
              <fullscreen-exit-outlined v-else />
            </template>
            {{ isFullscreen ? t('common.exitFullscreen') : t('common.fullscreen') }}
          </a-button>
          <a-button @click="toggleAutoRefresh">
            {{ isAutoRefreshPaused ? t('common.startRefresh') : t('common.stopRefresh') }}
          </a-button>
          <a-button type="primary" @click="fetchAllData">
            <reload-outlined /> {{ t('common.refreshNow') }}
          </a-button>
        </a-space>
      </div>
    </div>

    <a-row v-if="!isFullscreen" :gutter="16" class="summary-row">
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

    <div v-else class="fullscreen-header">
      <span class="fs-title">{{ t('plc.monitorTitle') }}</span>
      <span class="fs-time">{{ t('common.updateTime') }}: {{ lastRefreshTime }}</span>
      <a-space>
        <a-button size="small" ghost @click="toggleAutoRefresh" class="fs-btn">
          {{ isAutoRefreshPaused ? t('common.startRefresh') : t('common.stopRefresh') }}
        </a-button>
        <a-button size="small" ghost @click="toggleFullscreen" class="fs-btn">
          {{ t('common.exitFullscreen') }}
        </a-button>
      </a-space>
    </div>

    <div class="matrix-container">
      <div class="matrix-scroll-wrapper">
        <table class="matrix-table">
          <thead>
            <tr>
              <th class="sticky-col first-col">{{ t('plc.deviceSignal') }}</th>
              <th v-for="col in uniqueSignalNames" :key="col" class="signal-header" :class="getSignalHeaderClass(col)">
                {{ col }}
                <div class="writer-tag" v-if="getSignalWriter(col)">
                  {{ getSignalWriter(col) }}
                </div>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="device in devices" :key="device.id">
              <td class="sticky-col device-row-header">
                <div class="device-cell-content">
                  <div class="device-name-row">
                    <span class="device-name">{{ device.remark || device.ipAddress }}</span>
                  </div>
                  <div class="device-status-row">
                    <a-badge :status="device.isEnabled ? 'success' : 'default'" />
                    <span class="ip-text">{{ device.ipAddress }}</span>
                  </div>
                  <div class="device-diagnostic-row">
                    <a-tag :color="getDeviceHealthColor(device)">
                      {{ getDeviceHealthText(device) }}
                    </a-tag>
                    <span class="device-meta">{{ t('plc.signalCount') }}: {{ device.signalCount ?? getDeviceSignals(device).length }}</span>
                  </div>
                  <div class="device-diagnostic-row">
                    <span class="device-meta">{{ t('plc.lastSignalUpdate') }}: {{ getDeviceLastUpdateText(device) }}</span>
                  </div>
                </div>
              </td>
              <td v-for="col in uniqueSignalNames" :key="`${device.id}-${col}`" class="signal-cell">
                <a-popover
                  :title="t('plc.signalControl')"
                  trigger="click"
                  placement="bottom"
                  v-if="getSignal(device, col)"
                >
                  <template #content>
                    <div class="signal-control-popover">
                      <p><strong>{{ t('plc.signalName') }}:</strong> {{ col }}</p>
                      <p><strong>{{ t('plc.currentValue') }}:</strong> {{ getSignalValue(device, col) }}</p>
                      <p><strong>{{ t('plc.writePermission') }}:</strong> {{ getSignal(device, col)?.writer || t('common.unknown') }}</p>
                      <p><strong>{{ t('plc.lastSignalUpdate') }}:</strong> {{ formatTime(getSignal(device, col)?.lastUpdateTime) }}</p>
                      <a-divider style="margin: 8px 0" />
                      <a-space v-if="getSignal(device, col)?.writer !== 'PLC'">
                        <a-popconfirm :title="t('plc.confirmSetTrue')" @confirm="triggerSignal(getSignal(device, col)!.id, true)">
                          <a-button type="primary" size="small">{{ t('plc.setTrue') }}</a-button>
                        </a-popconfirm>
                        <a-popconfirm :title="t('plc.confirmSetFalse')" @confirm="triggerSignal(getSignal(device, col)!.id, false)">
                          <a-button danger size="small">{{ t('plc.setFalse') }}</a-button>
                        </a-popconfirm>
                      </a-space>
                      <a-alert v-else :message="t('plc.plcWriterWarning')" type="warning" show-icon style="padding: 4px 8px" />
                    </div>
                  </template>
                  <div class="cell-content" :class="getSignalClass(device, col)">
                    {{ getSignalValue(device, col) }}
                  </div>
                </a-popover>
                <div v-else class="cell-content status-none">-</div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { message, theme } from 'ant-design-vue';
import { ReloadOutlined, FullscreenOutlined, FullscreenExitOutlined } from '@ant-design/icons-vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
const { useToken } = theme;
const { token } = useToken();
const staleThresholdSeconds = 15;

interface PlcDevice {
  id: string;
  ipAddress: string;
  isEnabled?: boolean;
  remark?: string;
  moduleAddress?: string;
  signalCount?: number;
  lastSignalUpdateTime?: string | null;
}

interface PlcSignalStatus {
  id: number;
  name: string;
  address: string;
  dataType: string;
  value: any;
  plcDeviceId: string;
  dbBlock?: string;
  writer?: string;
  lastUpdateTime?: string | null;
}

const devices = ref<PlcDevice[]>([]);
const allSignals = ref<PlcSignalStatus[]>([]);
const isAutoRefreshPaused = ref(false);
const lastRefreshTime = ref<string>('');
const autoRefreshTimer = ref<number | null>(null);
const isFullscreen = ref(false);

const API_MANAGEMENT = '/api/plcsignal';
const API_STATUS = '/api/plc-signal-status';

const normalizeDevice = (raw: any): PlcDevice => ({
  id: String(raw?.id ?? raw?.Id ?? raw?.ipAddress ?? raw?.IpAddress ?? ''),
  ipAddress: raw?.ipAddress ?? raw?.IpAddress ?? '',
  isEnabled: raw?.isEnabled ?? raw?.IsEnabled ?? false,
  remark: raw?.remark ?? raw?.Remark ?? '',
  moduleAddress: raw?.moduleAddress ?? raw?.ModuleAddress ?? '',
  signalCount: raw?.signalCount ?? raw?.SignalCount ?? 0,
  lastSignalUpdateTime: raw?.lastSignalUpdateTime ?? raw?.LastSignalUpdateTime ?? null
});

const normalizeSignal = (raw: any): PlcSignalStatus => ({
  id: raw?.id ?? raw?.Id ?? 0,
  name: raw?.name ?? raw?.Name ?? '',
  address: raw?.address ?? raw?.Address ?? '',
  dataType: raw?.dataType ?? raw?.DataType ?? '',
  value: raw?.value ?? raw?.Value,
  plcDeviceId: raw?.plcDeviceId ?? raw?.PlcDeviceId ?? '',
  dbBlock: raw?.dbBlock ?? raw?.DbBlock ?? '',
  writer: raw?.writer ?? raw?.Writer ?? '',
  lastUpdateTime: raw?.lastUpdateTime ?? raw?.LastUpdateTime ?? null
});

const uniqueSignalNames = computed(() => {
  const names = new Set<string>();
  allSignals.value.forEach((s) => {
    if (s.name) names.add(s.name);
  });
  return Array.from(names).sort();
});

const enabledDeviceCount = computed(() => devices.value.filter((d) => d.isEnabled).length);
const healthyDeviceCount = computed(() => devices.value.filter((d) => getDeviceHealthStatus(d) === 'healthy').length);
const staleDeviceCount = computed(() => devices.value.filter((d) => getDeviceHealthStatus(d) === 'stale').length);

const getDeviceSignals = (device: PlcDevice) => {
  return allSignals.value.filter(
    (s) => s.plcDeviceId === device.ipAddress && (s.dbBlock || '') === (device.moduleAddress || '')
  );
};

const getDeviceLastUpdate = (device: PlcDevice) => {
  if (device.lastSignalUpdateTime) {
    return new Date(device.lastSignalUpdateTime);
  }

  const signalTimes = getDeviceSignals(device)
    .map((signal) => signal.lastUpdateTime)
    .filter(Boolean)
    .map((time) => new Date(time as string).getTime())
    .filter((time) => !Number.isNaN(time));

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

const formatTime = (value?: string | Date | null) => {
  if (!value) return t('common.unknown');
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return t('common.unknown');
  return date.toLocaleString();
};

const getDeviceLastUpdateText = (device: PlcDevice) => {
  const lastUpdate = getDeviceLastUpdate(device);
  if (!lastUpdate) return t('common.unknown');

  const ageSeconds = Math.max(0, Math.floor((Date.now() - lastUpdate.getTime()) / 1000));
  return `${formatTime(lastUpdate)} (${ageSeconds}${t('common.seconds', { n: '' }).replace('{n}', '').trim()})`;
};

const fetchAllData = async () => {
  try {
    const [devRes, sigRes] = await Promise.all([fetch(API_MANAGEMENT), fetch(`${API_STATUS}/signals`)]);
    const devData = await devRes.json();
    const sigData = await sigRes.json();

    if (devData.success) {
      devices.value = (devData.data || []).map(normalizeDevice);
    }

    if (sigData.success) {
      allSignals.value = (sigData.data || []).map(normalizeSignal);
    }

    lastRefreshTime.value = new Date().toLocaleTimeString();
  } catch (error) {
    console.error('Fetch error:', error);
  }
};

const toggleAutoRefresh = () => {
  if (isAutoRefreshPaused.value) {
    isAutoRefreshPaused.value = false;
    startAutoRefresh();
  } else {
    isAutoRefreshPaused.value = true;
    stopAutoRefresh();
  }
};

const startAutoRefresh = () => {
  stopAutoRefresh();
  fetchAllData();
  autoRefreshTimer.value = window.setInterval(fetchAllData, 3000);
};

const stopAutoRefresh = () => {
  if (autoRefreshTimer.value) {
    clearInterval(autoRefreshTimer.value);
    autoRefreshTimer.value = null;
  }
};

const toggleFullscreen = () => {
  isFullscreen.value = !isFullscreen.value;
  const elem = document.documentElement;
  if (isFullscreen.value) {
    elem.requestFullscreen?.().catch(() => {});
  } else {
    document.exitFullscreen?.().catch(() => {});
  }
};

const triggerSignal = async (signalId: number, value: boolean) => {
  try {
    const res = await fetch(`${API_STATUS}/trigger`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ signalId, value })
    });
    const data = await res.json();
    if (data.success) {
      message.success(t('plc.operateSuccess', { value: value ? '1' : '0' }));
      const signal = allSignals.value.find((s) => s.id === signalId);
      if (signal) {
        signal.value = value;
      }
    } else {
      message.error(data.message || t('common.fail'));
    }
  } catch (_error) {
    message.error(t('common.fail'));
  }
};

const getSignal = (device: PlcDevice, signalName: string) => {
  return allSignals.value.find(
    (s) => s.plcDeviceId === device.ipAddress && s.name === signalName && (s.dbBlock || '') === (device.moduleAddress || '')
  );
};

const getSignalWriter = (signalName: string) => {
  const signal = allSignals.value.find((s) => s.name === signalName);
  return signal?.writer;
};

const getSignalHeaderClass = (signalName: string) => {
  const writer = getSignalWriter(signalName);
  if (writer === 'PLC') return 'header-plc';
  if (writer === 'AGV') return 'header-agv';
  return 'header-other';
};

const getSignalValue = (device: PlcDevice, signalName: string) => {
  const signal = getSignal(device, signalName);
  if (!signal) return '-';
  if (signal.dataType === 'Bool') {
    return signal.value === true || signal.value === '1' || signal.value === 1 ? 'True' : 'False';
  }
  return signal.value;
};

const getSignalClass = (device: PlcDevice, signalName: string) => {
  const signal = getSignal(device, signalName);
  if (!signal) return 'status-none';

  if (signal.dataType === 'Bool') {
    const val = signal.value === true || signal.value === '1' || signal.value === 1;
    return val ? 'status-true' : 'status-false';
  }
  return 'status-info';
};

onMounted(() => {
  startAutoRefresh();
});

onUnmounted(() => {
  stopAutoRefresh();
});
</script>

<style scoped>
.plc-signal-display {
  height: 100%;
  display: flex;
  flex-direction: column;
  background-color: v-bind('token.colorBgLayout');
  padding: 16px;
}

.view-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  background: v-bind('token.colorBgContainer');
  padding: 16px;
  border-radius: 4px;
}

.summary-row {
  margin-bottom: 16px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.header-left h3 {
  margin: 0;
  font-weight: 600;
}

.refresh-info {
  display: flex;
  align-items: center;
  gap: 8px;
}

.time-text {
  color: #999;
  font-size: 12px;
}

.matrix-container {
  flex: 1;
  min-height: 0;
  background: v-bind('token.colorBgContainer');
  border-radius: 4px;
  overflow: hidden;
  position: relative;
  transition: all 0.3s;
}

.fullscreen-mode {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  z-index: 1000;
  padding: 0;
  background: #fff;
}

.fullscreen-mode .matrix-container {
  height: calc(100vh - 40px);
  border-radius: 0;
}

.fullscreen-header {
  height: 40px;
  background: #001529;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
}

.fs-title {
  font-weight: bold;
  font-size: 16px;
}

.fs-time {
  font-size: 12px;
  color: rgba(255,255,255,0.7);
}

.fs-btn {
  color: #fff;
  border-color: rgba(255,255,255,0.5);
}

.fs-btn:hover {
  color: #1890ff;
  border-color: #1890ff;
}

.matrix-scroll-wrapper {
  height: 100%;
  overflow-x: auto;
  overflow-y: auto;
  scrollbar-gutter: stable both-edges;
}

.matrix-table {
  width: max-content;
  min-width: 100%;
  border-collapse: separate;
  border-spacing: 0;
}

.matrix-table th,
.matrix-table td {
  border: 1px solid v-bind('token.colorBorderSecondary');
  padding: 2px 3px;
  text-align: center;
  position: relative;
  font-size: 11px;
  background-color: v-bind('token.colorBgContainer');
  color: v-bind('token.colorText');
}

.matrix-table thead th {
  background: v-bind('token.colorFillQuaternary');
  font-weight: 600;
  position: sticky;
  top: 0;
  z-index: 10;
  box-shadow: 0 1px 0 v-bind('token.colorBorderSecondary');
  white-space: normal;
  vertical-align: bottom;
  padding: 3px 2px;
  line-height: 1.1;
  height: auto;
}

.signal-header {
  width: 52px;
  min-width: 52px;
  max-width: 52px;
  font-size: 10px;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: bottom;
  padding: 3px 2px;
  word-break: break-all;
}

.writer-tag {
  font-size: 9px;
  font-weight: normal;
  border-radius: 2px;
  padding: 0 1px;
  display: inline-block;
  margin-top: 2px;
  line-height: 1.1;
}

.header-plc {
  border-bottom: 3px solid #faad14 !important;
}

.header-plc .writer-tag {
  background-color: #fff7e6;
  color: #faad14;
  border: 1px solid #faad14;
}

.header-agv {
  border-bottom: 3px solid #1890ff !important;
}

.header-agv .writer-tag {
  background-color: #e6f7ff;
  color: #1890ff;
  border: 1px solid #1890ff;
}

.header-other {
  border-bottom: 3px solid v-bind('token.colorBorder') !important;
}

.header-other .writer-tag {
  background-color: v-bind('token.colorFillTertiary');
  color: v-bind('token.colorTextSecondary');
  border: 1px solid v-bind('token.colorBorder');
}

.first-col {
  width: 160px;
  min-width: 160px;
  max-width: 160px;
  left: 0;
  z-index: 20 !important;
  background: #fafafa;
}

.device-row-header {
  position: sticky;
  left: 0;
  background: #fff;
  z-index: 15;
  box-shadow: 1px 0 0 #f0f0f0;
  text-align: left;
}

.device-cell-content {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  padding: 3px;
  gap: 3px;
  background-color: v-bind('token.colorBgContainer');
}

.device-name-row {
  font-weight: 500;
  text-align: left;
  line-height: 1.2;
  color: v-bind('token.colorText');
}

.device-status-row,
.device-diagnostic-row {
  display: flex;
  align-items: center;
  gap: 3px;
  font-size: 10px;
  color: v-bind('token.colorTextSecondary');
  flex-wrap: wrap;
}

.device-meta {
  color: v-bind('token.colorTextSecondary');
}

.signal-cell {
  width: 52px;
  min-width: 52px;
  max-width: 52px;
  vertical-align: middle;
  cursor: pointer;
}

.signal-cell:hover {
  background-color: #f5f5f5;
}

.signal-control-popover {
  min-width: 220px;
}

.cell-content {
  display: block;
  padding: 1px 2px;
  border-radius: 2px;
  font-size: 10px;
  width: 100%;
  transition: all 0.3s;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.status-true {
  background-color: #1890ff;
  color: white;
  font-weight: bold;
  box-shadow: 0 0 4px rgba(24, 144, 255, 0.4);
}

.status-false {
  background-color: #52c41a;
  color: white;
  opacity: 0.8;
}

.status-info {
  background-color: #f0f0f0;
  color: #666;
}

.status-none {
  color: #ccc;
}
</style>


