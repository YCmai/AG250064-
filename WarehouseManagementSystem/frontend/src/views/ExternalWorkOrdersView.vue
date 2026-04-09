<template>
  <div class="external-data-page">
    <h1>外部工单查询</h1>

    <a-card class="search-card" :bodyStyle="{ padding: '16px' }">
      <a-form layout="inline">
        <a-form-item label="工单号">
          <a-input v-model:value="searchForm.orderNumber" allow-clear />
        </a-form-item>
        <a-form-item label="产品编码">
          <a-input v-model:value="searchForm.materialNumber" allow-clear />
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
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'msgType'">
            <a-tag :color="record.msgType === '1' ? 'success' : 'default'">
              {{ record.msgType === '1' ? '生效' : '失效' }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'processStatus'">
            <a-tag :color="record.processStatus === 0 ? 'processing' : 'success'">
              {{ record.processStatus === 0 ? '未处理' : '已处理' }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'createTime'">
            {{ formatDate(record.createTime) }}
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
import externalApiService, { WorkOrderRecord } from '@/services/externalApi'

const { t } = useI18n()
const loading = ref(false)
const data = ref<WorkOrderRecord[]>([])

const searchForm = reactive({
  orderNumber: '',
  materialNumber: '',
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
  { title: 'ID', dataIndex: 'id', width: 80 },
  { title: '工单号', dataIndex: 'orderNumber', width: 160 },
  { title: '产品编码', dataIndex: 'materialNumber', width: 180 },
  { title: '产品名称', dataIndex: 'materialName', width: 200 },
  { title: '消息类型', key: 'msgType', width: 100 },
  { title: '处理状态', key: 'processStatus', width: 120 },
  { title: t('common.createTime'), key: 'createTime', width: 180 },
  { title: t('common.remark'), dataIndex: 'remarks', ellipsis: true },
])

const fetchData = async () => {
  loading.value = true
  try {
    const response = await externalApiService.getWorkOrders(
      pagination.current,
      pagination.pageSize,
      searchForm.orderNumber || undefined,
      searchForm.materialNumber || undefined
    )

    if (response.success && response.data) {
      data.value = response.data.items || []
      pagination.total = response.data.total || 0
    } else {
      message.error(response.message || '获取工单数据失败')
    }
  } catch (error: any) {
    message.error(error.message || '获取工单数据失败')
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  pagination.current = 1
  fetchData()
}

const resetSearch = () => {
  searchForm.orderNumber = ''
  searchForm.materialNumber = ''
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
</style>
