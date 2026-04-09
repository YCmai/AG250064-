<template>
  <div class="api-management">
    <!-- Search Bar -->
    <!-- <a-card class="mb-3" :bodyStyle="{ padding: '16px' }">
      <a-form layout="inline">
        <a-form-item label="任务编号">
          <a-input v-model:value="searchForm.taskCode" placeholder="请输入任务编号" allow-clear />
        </a-form-item>
        <a-form-item label="任务类型">
          <a-input v-model:value="searchForm.taskType" placeholder="任务类型ID" allow-clear style="width: 120px" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="handleSearch">
            <search-outlined /> 查询
          </a-button>
          <a-button style="margin-left: 8px" @click="resetSearch">
            重置
          </a-button>
        </a-form-item>
      </a-form>
    </a-card> -->

    <!-- Data Table -->
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
          <template v-if="column.key === 'excute'">
            <a-tag :color="getStatusColor(record.excute)">
              {{ getStatusText(record.excute) }}
            </a-tag>
          </template>
          
          <template v-if="column.key === 'createTime'">
            {{ formatDate(record.createTime) }}
          </template>
          
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="showDetails(record)">{{ t('common.detail') }}</a-button>
              <a-popconfirm
                :title="t('location.deleteConfirm')"
                @confirm="deleteTask(record.id)"
                :ok-text="t('common.confirm')"
                :cancel-text="t('common.cancel')"
              >
                <a-button type="link" danger size="small">{{ t('common.delete') }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- Details Modal -->
    <a-modal
      v-model:open="detailsVisible"
      :title="t('api.taskDetail')"
      :footer="null"
      width="800px"
    >
      <div v-if="selectedTask">
        <a-descriptions bordered size="small" :column="2">
          <a-descriptions-item label="ID">{{ selectedTask.id }}</a-descriptions-item>
          <!-- <a-descriptions-item label="任务编号">{{ selectedTask.taskCode }}</a-descriptions-item>
          <a-descriptions-item label="任务类型">{{ selectedTask.taskType }}</a-descriptions-item> -->
          <a-descriptions-item :label="t('api.status')">
            <a-tag :color="getStatusColor(selectedTask.excute)">
              {{ getStatusText(selectedTask.excute) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item :label="t('api.createTime')">{{ formatDate(selectedTask.createTime) }}</a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">{{ t('api.messageContent') }}</a-divider>
        <div class="code-block">
          <pre>{{ tryFormatJson(selectedTask.message) }}</pre>
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue';
import { message } from 'ant-design-vue';
import { SearchOutlined } from '@ant-design/icons-vue';
import dayjs from 'dayjs';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

// Types
interface ApiTask {
  id: number;
  taskCode: string;
  taskType: number;
  excute: boolean;
  message: string;
  createTime: string;
}

// State
const loading = ref(false);
const data = ref<ApiTask[]>([]);
const detailsVisible = ref(false);
const selectedTask = ref<ApiTask | null>(null);

const searchForm = reactive({
  taskCode: '',
  taskType: ''
});

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t('dashboard.selectedCount', { n: total }) // Rough reuse or new key
});

// Columns
const columns = computed(() => [
  { title: 'ID', dataIndex: 'id', width: 80 },
  { title: t('api.taskCode'), dataIndex: 'taskCode' },
  { title: t('api.taskType'), dataIndex: 'taskType', width: 80 },
  { title: t('api.message'), dataIndex: 'message', ellipsis: true },
  { title: t('api.status'), key: 'excute', width: 100 },
  { title: t('api.createTime'), key: 'createTime', width: 180 },
  { title: t('common.operation'), key: 'action', width: 150, align: 'center' }
]);

// Methods
const fetchData = async () => {
  loading.value = true;
  try {
    const params = new URLSearchParams({
      pageIndex: pagination.current.toString(),
      pageSize: pagination.pageSize.toString()
    });
    
    if (searchForm.taskCode) params.append('taskCode', searchForm.taskCode);
    if (searchForm.taskType) params.append('taskType', searchForm.taskType);

    const res = await fetch(`/api/external-api-task/paged-tasks?${params.toString()}`);
    const result = await res.json();
    
    if (result.success) {
      // Map data to ensure camelCase
      data.value = (result.data.items || result.data.Items || []).map((item: any) => ({
        id: item.id || item.ID,
        taskCode: item.taskCode || item.TaskCode,
        taskType: item.taskType || item.TaskType,
        excute: item.excute !== undefined ? item.excute : item.Excute,
        message: item.message || item.Message,
        createTime: item.createTime || item.CreateTime
      }));
      pagination.total = result.data.total || result.data.Total;
     } else {
      message.error(result.message || t('api.fetchFail'));
    }
  } catch (error) {
    message.error(t('common.fail'));
  } finally {
    loading.value = false;
  }
};

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const resetSearch = () => {
  searchForm.taskCode = '';
  searchForm.taskType = '';
  handleSearch();
};

const handleTableChange = (pag: any) => {
  pagination.current = pag.current;
  pagination.pageSize = pag.pageSize;
  fetchData();
};

const showDetails = (record: ApiTask) => {
  selectedTask.value = record;
  detailsVisible.value = true;
};

const deleteTask = async (id: number) => {
  try {
    const res = await fetch(`/api/external-api-task/${id}`, { method: 'DELETE' });
    const result = await res.json();
    if (result.success) {
      message.success(t('common.success'));
      fetchData();
    } else {
      message.error(result.message || t('common.fail'));
    }
  } catch (error) {
    message.error(t('common.fail'));
  }
};

// Helpers
const formatDate = (dateStr: string) => {
  if (!dateStr) return '-';
  return dayjs(dateStr).format('YYYY-MM-DD HH:mm:ss');
};

const getStatusText = (excute: boolean) => {
  return excute ? t('api.executed') : t('api.notExecuted');
};

const getStatusColor = (excute: boolean) => {
  return excute ? 'success' : 'default';
};

const tryFormatJson = (str: string) => {
  if (!str) return '-';
  try {
    return JSON.stringify(JSON.parse(str), null, 2);
  } catch (e) {
    return str;
  }
};

// Lifecycle
onMounted(() => {
  fetchData();
});
</script>

<style scoped>
.api-management {
  padding: 16px;
}
.code-block {
  background: #f5f5f5;
  padding: 12px;
  border-radius: 4px;
  max-height: 300px;
  overflow: auto;
  font-family: monospace;
  font-size: 12px;
}
.mt-3 {
  margin-top: 16px;
}
</style>
