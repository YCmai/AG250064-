<template>
  <div class="integration-data-container">
    <h1>{{ $t('integrationData.title') }}</h1>

    <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
      <a-tab-pane key="workOrders" :tab="$t('integrationData.workOrders')">
        <a-space style="margin-bottom: 16px">
          <a-input
            v-model:value="workOrderFilter.orderNumber"
            :placeholder="$t('integrationData.orderNumberPlaceholder')"
            allow-clear
            style="width: 260px"
            @pressEnter="fetchWorkOrders"
          />
          <a-button type="primary" @click="fetchWorkOrders">{{ $t('common.search') }}</a-button>
          <a-button @click="resetWorkOrderFilter">{{ $t('common.reset') }}</a-button>
        </a-space>

        <a-table
          :columns="workOrderColumns"
          :data-source="workOrders"
          :loading="loading"
          :pagination="workOrderPagination"
          @change="handleWorkOrderTableChange"
          row-key="id"
          size="middle"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'createTime'">{{ formatDate(record.createTime) }}</template>
            <template v-else-if="column.key === 'msgType'">
              <a-tag :color="record.msgType === '1' ? 'success' : 'warning'">
                {{ record.msgType === '1' ? '1-生效' : '2-失效' }}
              </a-tag>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="inbox" :tab="$t('integrationData.commandInbox')">
        <a-space style="margin-bottom: 16px">
          <a-input
            v-model:value="inboxFilter.taskNumber"
            :placeholder="$t('integrationData.taskNumberPlaceholder')"
            allow-clear
            style="width: 260px"
            @pressEnter="fetchInbox"
          />
          <a-button type="primary" @click="fetchInbox">{{ $t('common.search') }}</a-button>
          <a-button @click="resetInboxFilter">{{ $t('common.reset') }}</a-button>
        </a-space>

        <a-table
          :columns="inboxColumns"
          :data-source="inboxRows"
          :loading="loading"
          :pagination="inboxPagination"
          @change="handleInboxTableChange"
          row-key="id"
          size="middle"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'processStatus'">
              <a-tag :color="getProcessStatusColor(record.processStatus)">
                {{ getProcessStatusText(record.processStatus) }}
              </a-tag>
            </template>
            <template v-else-if="column.key === 'createTime'">{{ formatDate(record.createTime) }}</template>
            <template v-else-if="column.key === 'updateTime'">{{ formatDate(record.updateTime) }}</template>
            <template v-else-if="column.key === 'processTime'">{{ formatDate(record.processTime) }}</template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="items" :tab="$t('integrationData.commandInboxItems')">
        <a-space style="margin-bottom: 16px">
          <a-input-number
            v-model:value="itemFilter.inboxId"
            :placeholder="$t('integrationData.inboxIdPlaceholder')"
            :min="1"
            style="width: 180px"
          />
          <a-input
            v-model:value="itemFilter.taskNumber"
            :placeholder="$t('integrationData.taskNumberPlaceholder')"
            allow-clear
            style="width: 260px"
            @pressEnter="fetchItems"
          />
          <a-button type="primary" @click="fetchItems">{{ $t('common.search') }}</a-button>
          <a-button @click="resetItemFilter">{{ $t('common.reset') }}</a-button>
        </a-space>

        <a-table
          :columns="itemColumns"
          :data-source="itemRows"
          :loading="loading"
          :pagination="itemPagination"
          @change="handleItemTableChange"
          row-key="id"
          size="middle"
          :scroll="{ x: 1200 }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'createTime'">{{ formatDate(record.createTime) }}</template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="outboundQueue" :tab="$t('integrationData.outboundQueue')">
        <a-space style="margin-bottom: 16px" wrap>
          <a-input
            v-model:value="outboundFilter.taskNumber"
            :placeholder="$t('integrationData.taskNumberPlaceholder')"
            allow-clear
            style="width: 220px"
            @pressEnter="fetchOutboundQueue"
          />
          <a-select
            v-model:value="outboundFilter.eventType"
            :placeholder="$t('integrationData.eventType')"
            allow-clear
            style="width: 170px"
          >
            <a-select-option :value="1">{{ $t('integrationData.eventType1') }}</a-select-option>
            <a-select-option :value="2">{{ $t('integrationData.eventType2') }}</a-select-option>
            <a-select-option :value="3">{{ $t('integrationData.eventType3') }}</a-select-option>
          </a-select>
          <a-select
            v-model:value="outboundFilter.processStatus"
            :placeholder="$t('integrationData.processStatus')"
            allow-clear
            style="width: 170px"
          >
            <a-select-option :value="0">{{ $t('integrationData.pending') }}</a-select-option>
            <a-select-option :value="1">{{ $t('integrationData.processed') }}</a-select-option>
            <a-select-option :value="2">{{ $t('integrationData.failedRetry') }}</a-select-option>
            <a-select-option :value="3">{{ $t('integrationData.failedFinal') }}</a-select-option>
          </a-select>
          <a-button type="primary" @click="fetchOutboundQueue">{{ $t('common.search') }}</a-button>
          <a-button @click="resetOutboundFilter">{{ $t('common.reset') }}</a-button>
          <a-button type="primary" @click="openCreateOutboundModal">{{ $t('common.add') }}</a-button>
        </a-space>

        <a-table
          :columns="outboundColumns"
          :data-source="outboundRows"
          :loading="loading"
          :pagination="outboundPagination"
          @change="handleOutboundTableChange"
          row-key="id"
          size="middle"
          :scroll="{ x: 1900 }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'eventType'">{{ getEventTypeText(record.eventType) }}</template>
            <template v-else-if="column.key === 'processStatus'">
              <a-tag :color="getProcessStatusColor(record.processStatus)">
                {{ getOutboundProcessStatusText(record.processStatus) }}
              </a-tag>
            </template>
            <template v-else-if="column.key === 'createTime'">{{ formatDate(record.createTime) }}</template>
            <template v-else-if="column.key === 'updateTime'">{{ formatDate(record.updateTime) }}</template>
            <template v-else-if="column.key === 'nextRetryTime'">{{ formatDate(record.nextRetryTime) }}</template>
            <template v-else-if="column.key === 'processTime'">{{ formatDate(record.processTime) }}</template>
            <template v-else-if="column.key === 'operation'">
              <a-space>
                <a-button type="link" size="small" @click="openEditOutboundModal(record)">{{ $t('common.edit') }}</a-button>
                <a-popconfirm
                  :title="$t('integrationData.deleteConfirm')"
                  :ok-text="$t('common.confirm')"
                  :cancel-text="$t('common.cancel')"
                  @confirm="deleteOutboundRecord(record.id)"
                >
                  <a-button type="link" danger size="small">{{ $t('common.delete') }}</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-tab-pane>
    </a-tabs>

    <a-modal
      v-model:open="outboundModalVisible"
      :title="outboundModalMode === 'create' ? $t('integrationData.addOutbound') : $t('integrationData.editOutbound')"
      :confirm-loading="outboundSaving"
      @ok="submitOutboundForm"
      @cancel="closeOutboundModal"
      width="820px"
      destroy-on-close
    >
      <a-form layout="vertical">
        <a-row :gutter="12">
          <a-col :span="12">
            <a-form-item :label="$t('integrationData.eventType')" required>
              <a-select v-model:value="outboundForm.eventType">
                <a-select-option :value="1">{{ $t('integrationData.eventType1') }}</a-select-option>
                <a-select-option :value="2">{{ $t('integrationData.eventType2') }}</a-select-option>
                <a-select-option :value="3">{{ $t('integrationData.eventType3') }}</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="$t('integrationData.processStatus')" required>
              <a-select v-model:value="outboundForm.processStatus">
                <a-select-option :value="0">{{ $t('integrationData.pending') }}</a-select-option>
                <a-select-option :value="1">{{ $t('integrationData.processed') }}</a-select-option>
                <a-select-option :value="2">{{ $t('integrationData.failedRetry') }}</a-select-option>
                <a-select-option :value="3">{{ $t('integrationData.failedFinal') }}</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="12">
          <a-col :span="12">
            <a-form-item :label="$t('integrationData.taskNumber')" required>
              <a-input v-model:value="outboundForm.taskNumber" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="$t('integrationData.businessKey')" required>
              <a-input v-model:value="outboundForm.businessKey" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="12">
          <a-col :span="12">
            <a-form-item :label="$t('integrationData.retryCount')">
              <a-input-number v-model:value="outboundForm.retryCount" :min="0" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="$t('integrationData.lastError')">
              <a-input v-model:value="outboundForm.lastError" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item :label="$t('integrationData.requestBody')" required>
          <a-textarea v-model:value="outboundForm.requestBody" :rows="6" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import integrationDataService, {
  AgvCommandInboxItemRow,
  AgvCommandInboxRow,
  AgvOutboundQueueRow,
  AgvOutboundQueueUpsertRequest,
  WorkOrderRow
} from '@/services/integrationData'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const activeTab = ref('workOrders')
const loading = ref(false)

const workOrders = ref<WorkOrderRow[]>([])
const inboxRows = ref<AgvCommandInboxRow[]>([])
const itemRows = ref<AgvCommandInboxItemRow[]>([])
const outboundRows = ref<AgvOutboundQueueRow[]>([])

const workOrderFilter = reactive({ orderNumber: '' })
const inboxFilter = reactive({ taskNumber: '' })
const itemFilter = reactive<{ inboxId?: number; taskNumber: string }>({ inboxId: undefined, taskNumber: '' })
const outboundFilter = reactive<{ taskNumber: string; eventType?: number; processStatus?: number }>({
  taskNumber: '',
  eventType: undefined,
  processStatus: undefined
})

const workOrderPagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t('common.totalItems', { n: total })
})

const inboxPagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t('common.totalItems', { n: total })
})

const itemPagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t('common.totalItems', { n: total })
})

const outboundPagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t('common.totalItems', { n: total })
})

const workOrderColumns = computed(() => [
  { title: 'ID', dataIndex: 'id', width: 90 },
  { title: t('integrationData.orderNumber'), dataIndex: 'orderNumber', width: 180 },
  { title: t('integrationData.materialNumber'), dataIndex: 'materialNumber', width: 220 },
  { title: t('integrationData.materialName'), dataIndex: 'materialName', width: 200 },
  { title: t('integrationData.msgType'), key: 'msgType', width: 100 },
  { title: t('integrationData.processStatus'), dataIndex: 'processStatus', width: 120 },
  { title: t('common.createTime'), key: 'createTime', width: 180 },
  { title: t('common.remark'), dataIndex: 'remarks' }
])

const inboxColumns = computed(() => [
  { title: 'ID', dataIndex: 'id', width: 90 },
  { title: t('integrationData.taskNumber'), dataIndex: 'taskNumber', width: 180 },
  { title: t('integrationData.priority'), dataIndex: 'priority', width: 100 },
  { title: t('integrationData.processStatus'), key: 'processStatus', width: 120 },
  { title: t('common.createTime'), key: 'createTime', width: 180 },
  { title: t('common.updateTime'), key: 'updateTime', width: 180 },
  { title: t('integrationData.processTime'), key: 'processTime', width: 180 },
  { title: t('integrationData.errorMsg'), dataIndex: 'errorMsg' }
])

const itemColumns = computed(() => [
  { title: 'ID', dataIndex: 'id', width: 90 },
  { title: t('integrationData.inboxId'), dataIndex: 'inboxId', width: 100 },
  { title: t('integrationData.taskNumber'), dataIndex: 'taskNumber', width: 180 },
  { title: t('integrationData.seq'), dataIndex: 'seq', width: 80 },
  { title: t('integrationData.taskType'), dataIndex: 'taskType', width: 100 },
  { title: t('integrationData.fromStation'), dataIndex: 'fromStation', width: 160 },
  { title: t('integrationData.toStation'), dataIndex: 'toStation', width: 160 },
  { title: t('integrationData.palletNumber'), dataIndex: 'palletNumber', width: 160 },
  { title: t('integrationData.binNumber'), dataIndex: 'binNumber', width: 160 },
  { title: t('common.createTime'), key: 'createTime', width: 180 }
])

const outboundColumns = computed(() => [
  { title: 'ID', dataIndex: 'id', width: 80, fixed: 'left' },
  { title: t('integrationData.eventType'), key: 'eventType', width: 140 },
  { title: t('integrationData.taskNumber'), dataIndex: 'taskNumber', width: 180 },
  { title: t('integrationData.businessKey'), dataIndex: 'businessKey', width: 230 },
  { title: t('integrationData.processStatus'), key: 'processStatus', width: 130 },
  { title: t('integrationData.retryCount'), dataIndex: 'retryCount', width: 110 },
  { title: t('integrationData.lastError'), dataIndex: 'lastError', width: 220, ellipsis: true },
  { title: t('integrationData.nextRetryTime'), key: 'nextRetryTime', width: 180 },
  { title: t('common.createTime'), key: 'createTime', width: 180 },
  { title: t('integrationData.processTime'), key: 'processTime', width: 180 },
  { title: t('common.updateTime'), key: 'updateTime', width: 180 },
  { title: t('integrationData.requestBody'), dataIndex: 'requestBody', width: 260, ellipsis: true },
  { title: t('common.operation'), key: 'operation', width: 140, fixed: 'right' }
])

const outboundModalVisible = ref(false)
const outboundModalMode = ref<'create' | 'edit'>('create')
const outboundEditingId = ref<number | null>(null)
const outboundSaving = ref(false)

const outboundForm = reactive<AgvOutboundQueueUpsertRequest>({
  eventType: 1,
  taskNumber: '',
  businessKey: '',
  requestBody: '',
  processStatus: 0,
  retryCount: 0,
  lastError: '',
  nextRetryTime: undefined,
  processTime: undefined
})

const fetchWorkOrders = async () => {
  loading.value = true
  try {
    const res = await integrationDataService.getWorkOrders(
      workOrderPagination.current,
      workOrderPagination.pageSize,
      workOrderFilter.orderNumber || undefined
    )
    if (res.success && res.data) {
      workOrders.value = res.data.items || []
      workOrderPagination.total = res.data.total || 0
    } else {
      message.error(res.message || t('common.fail'))
    }
  } catch {
    message.error(t('common.fail'))
  } finally {
    loading.value = false
  }
}

const fetchInbox = async () => {
  loading.value = true
  try {
    const res = await integrationDataService.getAgvCommandInbox(
      inboxPagination.current,
      inboxPagination.pageSize,
      inboxFilter.taskNumber || undefined
    )
    if (res.success && res.data) {
      inboxRows.value = res.data.items || []
      inboxPagination.total = res.data.total || 0
    } else {
      message.error(res.message || t('common.fail'))
    }
  } catch {
    message.error(t('common.fail'))
  } finally {
    loading.value = false
  }
}

const fetchItems = async () => {
  loading.value = true
  try {
    const res = await integrationDataService.getAgvCommandInboxItems(
      itemPagination.current,
      itemPagination.pageSize,
      itemFilter.inboxId,
      itemFilter.taskNumber || undefined
    )
    if (res.success && res.data) {
      itemRows.value = res.data.items || []
      itemPagination.total = res.data.total || 0
    } else {
      message.error(res.message || t('common.fail'))
    }
  } catch {
    message.error(t('common.fail'))
  } finally {
    loading.value = false
  }
}

const fetchOutboundQueue = async () => {
  loading.value = true
  try {
    const res = await integrationDataService.getAgvOutboundQueue(
      outboundPagination.current,
      outboundPagination.pageSize,
      outboundFilter.taskNumber || undefined,
      outboundFilter.eventType,
      outboundFilter.processStatus
    )
    if (res.success && res.data) {
      outboundRows.value = res.data.items || []
      outboundPagination.total = res.data.total || 0
    } else {
      message.error(res.message || t('common.fail'))
    }
  } catch {
    message.error(t('common.fail'))
  } finally {
    loading.value = false
  }
}

const handleWorkOrderTableChange = (pagination: any) => {
  workOrderPagination.current = pagination.current
  workOrderPagination.pageSize = pagination.pageSize
  fetchWorkOrders()
}

const handleInboxTableChange = (pagination: any) => {
  inboxPagination.current = pagination.current
  inboxPagination.pageSize = pagination.pageSize
  fetchInbox()
}

const handleItemTableChange = (pagination: any) => {
  itemPagination.current = pagination.current
  itemPagination.pageSize = pagination.pageSize
  fetchItems()
}

const handleOutboundTableChange = (pagination: any) => {
  outboundPagination.current = pagination.current
  outboundPagination.pageSize = pagination.pageSize
  fetchOutboundQueue()
}

const resetWorkOrderFilter = () => {
  workOrderFilter.orderNumber = ''
  workOrderPagination.current = 1
  fetchWorkOrders()
}

const resetInboxFilter = () => {
  inboxFilter.taskNumber = ''
  inboxPagination.current = 1
  fetchInbox()
}

const resetItemFilter = () => {
  itemFilter.inboxId = undefined
  itemFilter.taskNumber = ''
  itemPagination.current = 1
  fetchItems()
}

const resetOutboundFilter = () => {
  outboundFilter.taskNumber = ''
  outboundFilter.eventType = undefined
  outboundFilter.processStatus = undefined
  outboundPagination.current = 1
  fetchOutboundQueue()
}

const handleTabChange = (tab: string) => {
  if (tab === 'workOrders') fetchWorkOrders()
  if (tab === 'inbox') fetchInbox()
  if (tab === 'items') fetchItems()
  if (tab === 'outboundQueue') fetchOutboundQueue()
}

const formatDate = (value?: string) => {
  if (!value) return '-'
  return dayjs(value).format('YYYY-MM-DD HH:mm:ss')
}

const getProcessStatusText = (status: number) => {
  if (status === 0) return t('integrationData.pending')
  if (status === 1) return t('integrationData.processed')
  if (status === 2) return t('integrationData.failed')
  return `${status}`
}

const getOutboundProcessStatusText = (status: number) => {
  if (status === 0) return t('integrationData.pending')
  if (status === 1) return t('integrationData.processed')
  if (status === 2) return t('integrationData.failedRetry')
  if (status === 3) return t('integrationData.failedFinal')
  return `${status}`
}

const getProcessStatusColor = (status: number) => {
  if (status === 0) return 'processing'
  if (status === 1) return 'success'
  if (status === 2) return 'error'
  if (status === 3) return 'volcano'
  return 'default'
}

const getEventTypeText = (eventType: number) => {
  if (eventType === 1) return t('integrationData.eventType1')
  if (eventType === 2) return t('integrationData.eventType2')
  if (eventType === 3) return t('integrationData.eventType3')
  return `${eventType}`
}

const resetOutboundForm = () => {
  outboundEditingId.value = null
  outboundForm.eventType = 1
  outboundForm.taskNumber = ''
  outboundForm.businessKey = ''
  outboundForm.requestBody = ''
  outboundForm.processStatus = 0
  outboundForm.retryCount = 0
  outboundForm.lastError = ''
  outboundForm.nextRetryTime = undefined
  outboundForm.processTime = undefined
}

const openCreateOutboundModal = () => {
  outboundModalMode.value = 'create'
  resetOutboundForm()
  outboundModalVisible.value = true
}

const openEditOutboundModal = (record: AgvOutboundQueueRow) => {
  outboundModalMode.value = 'edit'
  outboundEditingId.value = record.id
  outboundForm.eventType = record.eventType
  outboundForm.taskNumber = record.taskNumber
  outboundForm.businessKey = record.businessKey
  outboundForm.requestBody = record.requestBody
  outboundForm.processStatus = record.processStatus
  outboundForm.retryCount = record.retryCount
  outboundForm.lastError = record.lastError || ''
  outboundForm.nextRetryTime = record.nextRetryTime
  outboundForm.processTime = record.processTime
  outboundModalVisible.value = true
}

const closeOutboundModal = () => {
  outboundModalVisible.value = false
  outboundSaving.value = false
}

const validateOutboundForm = () => {
  if (!outboundForm.taskNumber?.trim()) return t('integrationData.taskNumberRequired')
  if (!outboundForm.businessKey?.trim()) return t('integrationData.businessKeyRequired')
  if (!outboundForm.requestBody?.trim()) return t('integrationData.requestBodyRequired')
  return ''
}

const submitOutboundForm = async () => {
  const error = validateOutboundForm()
  if (error) {
    message.warning(error)
    return
  }

  outboundSaving.value = true
  try {
    const payload: AgvOutboundQueueUpsertRequest = {
      eventType: outboundForm.eventType,
      taskNumber: outboundForm.taskNumber.trim(),
      businessKey: outboundForm.businessKey.trim(),
      requestBody: outboundForm.requestBody.trim(),
      processStatus: outboundForm.processStatus,
      retryCount: outboundForm.retryCount || 0,
      lastError: outboundForm.lastError || '',
      nextRetryTime: outboundForm.nextRetryTime,
      processTime: outboundForm.processTime
    }

    const res = outboundModalMode.value === 'create'
      ? await integrationDataService.createAgvOutboundQueue(payload)
      : await integrationDataService.updateAgvOutboundQueue(outboundEditingId.value as number, payload)

    if (res.success) {
      message.success(res.message || t('common.success'))
      closeOutboundModal()
      fetchOutboundQueue()
      return
    }

    message.error(res.message || t('common.fail'))
  } catch {
    message.error(t('common.fail'))
  } finally {
    outboundSaving.value = false
  }
}

const deleteOutboundRecord = async (id: number) => {
  try {
    const res = await integrationDataService.deleteAgvOutboundQueue(id)
    if (res.success) {
      message.success(res.message || t('common.success'))
      fetchOutboundQueue()
      return
    }
    message.error(res.message || t('common.fail'))
  } catch {
    message.error(t('common.fail'))
  }
}

onMounted(() => {
  fetchWorkOrders()
})
</script>

<style scoped>
.integration-data-container {
  padding: 16px;
}
</style>
