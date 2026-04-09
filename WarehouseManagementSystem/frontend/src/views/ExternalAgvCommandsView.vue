<template>
  <div class="external-data-page">
    <h1>AGV指令查询</h1>

    <a-card class="search-card" :bodyStyle="{ padding: '16px' }">
      <a-form layout="inline">
        <a-form-item label="任务号">
          <a-input v-model:value="searchForm.taskCode" allow-clear />
        </a-form-item>
        <a-form-item label="任务组号">
          <a-input v-model:value="searchForm.taskGroupNo" allow-clear />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="handleSearch">{{ t('common.search') }}</a-button>
          <a-button style="margin-left: 8px" @click="resetSearch">{{ t('common.reset') }}</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <a-card :bodyStyle="{ padding: '0' }">
      <a-table
        :columns="columns"
        :data-source="data"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        size="middle"
        :scroll="{ x: 1500 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'creatTime'">
            {{ formatDate(record.creatTime) }}
          </template>
          <template v-else-if="column.key === 'remarks'">
            <div class="ellipsis-cell">{{ record.remarks || '-' }}</div>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import dayjs from 'dayjs'
import { message } from 'ant-design-vue'
import { useI18n } from 'vue-i18n'
import externalApiService, { AgvCommandRecord } from '@/services/externalApi'

const { t } = useI18n()
const loading = ref(false)
const data = ref<AgvCommandRecord[]>([])

const searchForm = reactive({
  taskCode: '',
  taskGroupNo: '',
})

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ['20', '50', '100'],
  showTotal: (total: number) => t('common.totalItems', { n: total }),
})

const columns = computed(() => [
  { title: 'ID', dataIndex: 'id', width: 80, fixed: 'left' },
  { title: '任务号', dataIndex: 'taskCode', width: 180 },
  { title: '请求号', dataIndex: 'requestCode', width: 200 },
  { title: '任务组号', dataIndex: 'taskGroupNo', width: 180 },
  { title: '任务类型', dataIndex: 'taskType', width: 100 },
  { title: '任务状态', dataIndex: 'taskStatus', width: 100 },
  { title: '执行优先级', dataIndex: 'priority', width: 100 },
  { title: '用户优先级', dataIndex: 'userPriority', width: 110 },
  { title: '起点', dataIndex: 'sourcePosition', width: 140 },
  { title: '终点', dataIndex: 'targetPosition', width: 140 },
  { title: '托盘号', dataIndex: 'palletNo', width: 140 },
  { title: 'Bin编号', dataIndex: 'binNumber', width: 140 },
  { title: 'AGV编号', dataIndex: 'robotCode', width: 120 },
  { title: t('common.createTime'), key: 'creatTime', width: 180 },
  { title: t('common.remark'), key: 'remarks', width: 260 },
])

const fetchData = async () => {
  loading.value = true
  try {
    const response = await externalApiService.getAgvCommands(
      pagination.current,
      pagination.pageSize,
      searchForm.taskCode || undefined,
      searchForm.taskGroupNo || undefined
    )

    if (response.success && response.data) {
      data.value = response.data.items || []
      pagination.total = response.data.total || 0
    } else {
      message.error(response.message || '获取AGV指令数据失败')
    }
  } catch (error: any) {
    message.error(error.message || '获取AGV指令数据失败')
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  pagination.current = 1
  fetchData()
}

const resetSearch = () => {
  searchForm.taskCode = ''
  searchForm.taskGroupNo = ''
  handleSearch()
}

const handleTableChange = (pag: any) => {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  fetchData()
}

const formatDate = (value?: string) => {
  if (!value) return '-'
  return dayjs(value).format('YYYY-MM-DD HH:mm:ss')
}

onMounted(fetchData)
</script>

<style scoped>
.external-data-page h1 {
  margin-bottom: 24px;
  font-size: 24px;
  font-weight: 600;
}

.search-card {
  margin-bottom: 16px;
}

.ellipsis-cell {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
