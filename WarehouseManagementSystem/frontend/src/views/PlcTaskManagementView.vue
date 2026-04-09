<template>
  <div class="plc-task-management">
    <a-card class="mb-3" :bodyStyle="{ padding: '16px' }">
      <a-form layout="inline">
        <a-form-item :label="t('plc.device')">
          <a-select
            v-model:value="searchForm.plcRemark"
            :placeholder="t('plc.allDevices')"
            style="width: 200px"
            allow-clear
            show-search
          >
            <a-select-option v-for="item in plcRemarks" :key="item" :value="item">{{ item }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('plc.dbBlock')">
          <a-select
            v-model:value="searchForm.plcTypeDb"
            :placeholder="t('plc.allDbBlocks')"
            style="width: 150px"
            allow-clear
            show-search
          >
            <a-select-option v-for="item in plcTypeDbs" :key="item" :value="item">{{ item }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="handleSearch">
            <search-outlined /> {{ t('common.search') }}
          </a-button>
          <a-button style="margin-left: 8px" @click="resetSearch">
            {{ t('common.reset') }}
          </a-button>
        </a-form-item>
        <a-form-item style="margin-left: auto;">
          <a-space>
            <span v-if="autoRefresh">
              <loading-outlined /> {{ t('common.autoRefreshing') }}
            </span>
            <span v-else>{{ t('common.autoRefreshPaused') }}</span>
            <a-switch v-model:checked="autoRefresh" size="small" />
            <a-select v-model:value="refreshInterval" size="small" style="width: 100px">
              <a-select-option :value="5000">{{ t('common.seconds', { n: 5 }) }}</a-select-option>
              <a-select-option :value="10000">{{ t('common.seconds', { n: 10 }) }}</a-select-option>
              <a-select-option :value="30000">{{ t('common.seconds', { n: 30 }) }}</a-select-option>
              <a-select-option :value="60000">{{ t('common.minutes', { n: 1 }) }}</a-select-option>
            </a-select>
          </a-space>
        </a-form-item>
      </a-form>
    </a-card>

    <a-row :gutter="16" class="mb-3 summary-row">
      <a-col :xs="24" :sm="12" :md="6">
        <a-card size="small">
          <a-statistic :title="t('plc.totalTasks')" :value="data.length" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card size="small">
          <a-statistic :title="t('plc.pendingTasks')" :value="pendingTaskCount" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card size="small">
          <a-statistic :title="t('plc.sentTasks')" :value="sentTaskCount" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card size="small">
          <a-statistic :title="t('plc.deviceDisabledTasks')" :value="deviceDisabledTaskCount" />
        </a-card>
      </a-col>
    </a-row>

    <a-card :bodyStyle="{ padding: '0' }">
      <a-table
        :columns="columns"
        :data-source="data"
        :loading="loading"
        :pagination="pagination"
        @change="handleTableChange"
        row-key="id"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="getStatusColor(record.status)">
              {{ getStatusText(record.status) }}
            </a-tag>
          </template>

          <template v-else-if="column.key === 'isSend'">
            <a-tag :color="record.isSend ? 'success' : 'warning'">
              {{ record.isSend ? t('plc.sent') : t('plc.notSent') }}
            </a-tag>
          </template>

          <template v-else-if="column.key === 'deviceEnabled'">
            <a-tag :color="record.deviceEnabled ? 'success' : 'default'">
              {{ record.deviceEnabled ? t('plc.deviceEnabled') : t('plc.deviceDisabled') }}
            </a-tag>
          </template>

          <template v-else-if="column.key === 'diagnosis'">
            <a-tag :color="getDiagnosisColor(record)">
              {{ getDiagnosisText(record) }}
            </a-tag>
          </template>

          <template v-else-if="column.key === 'queueAge'">
            {{ getQueueAgeText(record) }}
          </template>

          <template v-else-if="column.key === 'createTime'">
            {{ formatDate(record.createTime) }}
          </template>

          <template v-else-if="column.key === 'updateTime'">
            {{ formatDate(record.updateTime) }}
          </template>

          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="showDetails(record)">{{ t('common.detail') }}</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="detailsVisible"
      :title="t('api.taskDetail')"
      :footer="null"
      width="800px"
    >
      <div v-if="selectedTask">
        <a-descriptions bordered size="small" :column="2">
          <a-descriptions-item :label="t('plc.taskCode')">{{ selectedTask.orderCode }}</a-descriptions-item>
          <a-descriptions-item :label="t('plc.agv')">{{ selectedTask.agvNo || '-' }}</a-descriptions-item>
          <a-descriptions-item :label="t('plc.device')">{{ selectedTask.plcRemark || '-' }}</a-descriptions-item>
          <a-descriptions-item :label="t('plc.dbBlock')">{{ selectedTask.plcTypeDb || '-' }}</a-descriptions-item>
          <a-descriptions-item :label="t('plc.deviceStatus')">
            <a-tag :color="selectedTask.deviceEnabled ? 'success' : 'default'">
              {{ selectedTask.deviceEnabled ? t('plc.deviceEnabled') : t('plc.deviceDisabled') }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item :label="t('plc.taskDiagnosis')">
            <a-tag :color="getDiagnosisColor(selectedTask)">
              {{ getDiagnosisText(selectedTask) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item :label="t('api.status')">
            <a-tag :color="getStatusColor(selectedTask.status)">
              {{ getStatusText(selectedTask.status) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item :label="t('plc.sendStatus')">
            <a-tag :color="selectedTask.isSend ? 'success' : 'warning'">
              {{ selectedTask.isSend ? t('plc.sent') : t('plc.notSent') }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item :label="t('plc.queueAge')">{{ getQueueAgeText(selectedTask) }}</a-descriptions-item>
          <a-descriptions-item :label="t('common.updateTime')">{{ formatDate(selectedTask.updateTime) }}</a-descriptions-item>
          <a-descriptions-item :label="t('common.createTime')">{{ formatDate(selectedTask.createTime) }}</a-descriptions-item>
          <a-descriptions-item :label="t('plc.signalInfo')" :span="2">{{ selectedTask.signal || '-' }}</a-descriptions-item>
          <a-descriptions-item :label="t('common.remark')" :span="2">{{ selectedTask.remark || '-' }}</a-descriptions-item>
        </a-descriptions>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted, watch, computed } from 'vue';
import { message } from 'ant-design-vue';
import { SearchOutlined, LoadingOutlined } from '@ant-design/icons-vue';
import dayjs from 'dayjs';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
const delayedThresholdMinutes = 1;

interface AutoPlcTask {
  id: number;
  orderCode: string;
  agvNo?: string;
  status: number;
  isSend: boolean;
  signal?: string;
  createTime: string;
  updateTime: string;
  remark: string;
  plcRemark?: string;
  plcTypeDb?: string;
  deviceEnabled?: boolean;
}

const loading = ref(false);
const data = ref<AutoPlcTask[]>([]);
const detailsVisible = ref(false);
const selectedTask = ref<AutoPlcTask | null>(null);
const plcRemarks = ref<string[]>([]);
const plcTypeDbs = ref<string[]>([]);
const autoRefresh = ref(true);
const refreshInterval = ref(5000);
let timer: number | null = null;

const searchForm = reactive<{ plcRemark?: string; plcTypeDb?: string }>({
  plcRemark: undefined,
  plcTypeDb: undefined
});

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t('common.totalItems', { n: total })
});

const normalizeTask = (raw: any): AutoPlcTask => ({
  id: raw?.id ?? raw?.Id ?? 0,
  orderCode: raw?.orderCode ?? raw?.OrderCode ?? '',
  agvNo: raw?.agvNo ?? raw?.AgvNo ?? '',
  status: raw?.status ?? raw?.Status ?? 0,
  isSend: raw?.isSend ?? raw?.IsSend ?? false,
  signal: raw?.signal ?? raw?.Signal ?? '',
  createTime: raw?.createTime ?? raw?.CreateTime ?? '',
  updateTime: raw?.updateTime ?? raw?.UpdateTime ?? '',
  remark: raw?.remark ?? raw?.Remark ?? '',
  plcRemark: raw?.plcRemark ?? raw?.PlcRemark ?? '',
  plcTypeDb: raw?.plcTypeDb ?? raw?.PlcTypeDb ?? '',
  deviceEnabled: raw?.deviceEnabled ?? raw?.DeviceEnabled ?? false
});

const pendingTaskCount = computed(() => data.value.filter((item) => !item.isSend).length);
const sentTaskCount = computed(() => data.value.filter((item) => item.isSend).length);
const deviceDisabledTaskCount = computed(() => data.value.filter((item) => item.deviceEnabled === false).length);

const columns = computed(() => [
  { title: t('plc.taskCode'), dataIndex: 'orderCode', width: 150 },
  { title: t('plc.agv'), dataIndex: 'agvNo', width: 100 },
  { title: t('api.status'), key: 'status', width: 120 },
  { title: t('plc.sendStatus'), key: 'isSend', width: 100 },
  { title: t('plc.deviceStatus'), key: 'deviceEnabled', width: 120 },
  { title: t('plc.taskDiagnosis'), key: 'diagnosis', width: 150 },
  { title: t('plc.queueAge'), key: 'queueAge', width: 140 },
  { title: t('plc.signalInfo'), dataIndex: 'signal', ellipsis: true },
  { title: t('plc.device'), dataIndex: 'plcRemark', width: 150 },
  { title: t('common.remark'), dataIndex: 'remark', ellipsis: true },
  { title: t('common.createTime'), key: 'createTime', width: 180 },
  { title: t('common.updateTime'), key: 'updateTime', width: 180 },
  { title: t('common.operation'), key: 'action', width: 90, align: 'center' }
]);

const loadFilterOptions = async () => {
  try {
    const [resRemarks, resDbs] = await Promise.all([
      fetch('/api/auto-plc-task/plc-types').then((r) => r.json()),
      fetch('/api/auto-plc-task/plc-type-db').then((r) => r.json())
    ]);

    if (resRemarks.success) plcRemarks.value = resRemarks.data || [];
    if (resDbs.success) plcTypeDbs.value = resDbs.data || [];
  } catch (error) {
    console.error('Failed to load filter options', error);
  }
};

const fetchData = async () => {
  loading.value = true;
  try {
    const params = new URLSearchParams({
      pageIndex: pagination.current.toString(),
      pageSize: pagination.pageSize.toString()
    });

    if (searchForm.plcRemark) params.append('plcRemark', searchForm.plcRemark);
    if (searchForm.plcTypeDb) params.append('plcTypeDb', searchForm.plcTypeDb);

    const res = await fetch(`/api/auto-plc-task/paged-tasks?${params.toString()}`);
    const result = await res.json();

    if (result.success) {
      data.value = (result.data.items || []).map(normalizeTask);
      pagination.total = result.data.totalCount || 0;
    } else {
      message.error(result.message || t('api.fetchFail'));
    }
  } catch (error) {
    console.error(error);
    message.error(t('api.fetchFail'));
  } finally {
    loading.value = false;
  }
};

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const resetSearch = () => {
  searchForm.plcRemark = undefined;
  searchForm.plcTypeDb = undefined;
  handleSearch();
};

const handleTableChange = (pag: any) => {
  pagination.current = pag.current;
  pagination.pageSize = pag.pageSize;
  fetchData();
};

const showDetails = (record: AutoPlcTask) => {
  selectedTask.value = record;
  detailsVisible.value = true;
};

const formatDate = (dateStr?: string) => {
  if (!dateStr) return '-';
  return dayjs(dateStr).isValid() ? dayjs(dateStr).format('YYYY-MM-DD HH:mm:ss') : '-';
};

const getQueueAgeMinutes = (record: AutoPlcTask) => {
  if (!record.createTime) return null;
  const created = dayjs(record.createTime);
  if (!created.isValid()) return null;
  return Math.max(0, dayjs().diff(created, 'minute', true));
};

const getQueueAgeText = (record: AutoPlcTask) => {
  const minutes = getQueueAgeMinutes(record);
  if (minutes === null) return '-';
  if (minutes < 1) {
    return t('plc.lessThanOneMinute');
  }
  return t('plc.minutesAgo', { n: Math.floor(minutes) });
};

const getDiagnosisText = (record: AutoPlcTask) => {
  if (record.deviceEnabled === false) return t('plc.taskDeviceDisabled');
  if (!record.isSend) {
    const minutes = getQueueAgeMinutes(record) ?? 0;
    return minutes >= delayedThresholdMinutes ? t('plc.taskDelayed') : t('plc.taskQueued');
  }
  return t('plc.taskDispatched');
};

const getDiagnosisColor = (record: AutoPlcTask) => {
  if (record.deviceEnabled === false) return 'default';
  if (!record.isSend) {
    const minutes = getQueueAgeMinutes(record) ?? 0;
    return minutes >= delayedThresholdMinutes ? 'warning' : 'processing';
  }
  return 'success';
};

const getStatusText = (status: number) => {
  switch (status) {
    case 0: return t('plc.read');
    case 1: return t('plc.writeBool');
    case 2: return t('plc.resetBool');
    case 3: return t('plc.writeInt');
    case 4: return t('plc.resetInt');
    case 5: return t('plc.writeString');
    case 6: return t('plc.resetString');
    case 99: return t('plc.connectFail');
    default: return `${t('task.unknown')}(${status})`;
  }
};

const getStatusColor = (status: number) => {
  switch (status) {
    case 0: return 'blue';
    case 1: return 'green';
    case 2: return 'orange';
    case 3: return 'cyan';
    case 4: return 'purple';
    case 5: return 'geekblue';
    case 6: return 'magenta';
    case 99: return 'red';
    default: return 'default';
  }
};

const startAutoRefresh = () => {
  stopAutoRefresh();
  if (autoRefresh.value) {
    timer = window.setInterval(() => {
      if (!loading.value) {
        fetchData();
      }
    }, refreshInterval.value);
  }
};

const stopAutoRefresh = () => {
  if (timer) {
    clearInterval(timer);
    timer = null;
  }
};

watch(autoRefresh, (val) => {
  if (val) startAutoRefresh();
  else stopAutoRefresh();
});

watch(refreshInterval, () => {
  if (autoRefresh.value) startAutoRefresh();
});

onMounted(() => {
  loadFilterOptions();
  fetchData();
  startAutoRefresh();
});

onUnmounted(() => {
  stopAutoRefresh();
});
</script>

<style scoped>
.plc-task-management {
  padding: 16px;
}

.summary-row {
  margin-bottom: 16px;
}
</style>
