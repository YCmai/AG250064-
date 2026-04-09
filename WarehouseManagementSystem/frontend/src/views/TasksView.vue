<template>
  <div class="tasks-container">
    <h1>{{ $t('task.title') }}</h1>
    <a-space style="margin-bottom: 16px">
      <a-date-picker
        v-model:value="startDate"
        :placeholder="$t('task.startDate')"
      />
      <a-date-picker
        v-model:value="endDate"
        :placeholder="$t('task.endDate')"
      />
      <a-button type="primary" @click="handleFilter">{{ $t('task.filter') }}</a-button>
      <a-button type="primary" @click="handleCreateTask">
        <template #icon>
          <PlusOutlined />
        </template>
        {{ $t('task.add') }}
      </a-button>
      <a-button type="default" @click="handleExport">
        <template #icon>
          <DownloadOutlined />
        </template>
        {{ $t('task.export') }}
      </a-button>
    </a-space>
    <a-table
      :columns="columns"
      :data-source="taskStore.tasks"
      :loading="taskStore.isLoading"
      :pagination="{
        current: taskStore.page,
        pageSize: taskStore.pageSize,
        total: taskStore.total,
        onChange: (page, size) => {
          taskStore.setPage(page)
          taskStore.setPageSize(size)
          fetchTasks()
        },
        onShowSizeChange: (current, size) => {
          taskStore.setPage(1)
          taskStore.setPageSize(size)
          fetchTasks()
        },
        showSizeChanger: true,
        pageSizeOptions: ['20', '50', '100'],
        showTotal: (total, range) => `${range[0]}-${range[1]} / ${total}`,
      }"
      :row-key="(record) => record.id"
      :on-row="(record) => ({ onClick: () => handleRowClick(record) })"
      style="cursor: pointer"
      :scroll="{ x: 1200, y: 'calc(100vh - 260px)' }"
      size="middle"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'taskType'">
          {{ getTaskTypeInfo(record.taskType, settingStore.systemType) }}
        </template>
        <template v-else-if="column.key === 'taskStatus'">
          <a-tag :color="getStatusInfo(record.taskStatus, settingStore.systemType).color">
            {{ getStatusInfo(record.taskStatus, settingStore.systemType).text }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'creatTime'">
          {{ record.creatTime || '-' }}
        </template>
        <template v-else-if="column.key === 'endTime'">
          {{ record.endTime || '-' }}
        </template>
        <template v-else-if="column.key === 'robotCode'">
          {{ record.robotCode || '-' }}
        </template>
        <template v-else-if="column.key === 'runTaskId'">
          {{ record.runTaskId || '-' }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button
              type="primary"
              size="small"
              danger
              @click.stop="handleCancelTask(record.id)"
              :disabled="!canCancel(record.taskStatus)"
            >
              {{ $t('task.cancel') }}
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import dayjs, { Dayjs } from 'dayjs'
import { useRouter } from 'vue-router'
import { useTaskStore } from '@/stores/task'
import { useSettingStore } from '@/stores/setting'
import taskService from '@/services/task'
import { PlusOutlined, DownloadOutlined } from '@ant-design/icons-vue'
import { message, Modal } from 'ant-design-vue'
import * as XLSX from 'xlsx'
import { useI18n } from 'vue-i18n'
import { getStatusInfo } from '@/constants/taskStatus'
import { getTaskTypeInfo } from '@/constants/taskType'

const { t } = useI18n()
const router = useRouter()
const taskStore = useTaskStore()
const settingStore = useSettingStore()
const startDate = ref<Dayjs | null>(null)
const endDate = ref<Dayjs | null>(null)

const canCancel = (status: number) => {
  if (settingStore.systemType === 'NDC') {
    // NDC: Cannot cancel if Finished (11) or already Canceled/Error (>= 30)
    // Note: 32, 53 are also >= 30
    return status < 30 && status !== 11;
  } else {
    // Heartbeat: Can only cancel Waiting(0) or Working(1)
    return status === 0 || status === 1;
  }
}

const columns = computed(() => [
  {
    title: t('task.type'),
    dataIndex: 'taskType',
    key: 'taskType',
    width: 100,
  },
  {
    title: t('task.source'),
    dataIndex: 'sourcePosition',
    key: 'sourcePosition',
    width: 120,
  },
  {
    title: t('task.target'),
    dataIndex: 'targetPosition',
    key: 'targetPosition',
    width: 120,
  },
  {
    title: t('task.status'),
    dataIndex: 'taskStatus',
    key: 'taskStatus',
    width: 100,
  },
  {
    title: t('task.agvId'),
    dataIndex: 'robotCode',
    key: 'robotCode',
    width: 100,
  },
  {
    title: t('task.taskId'),
    dataIndex: 'runTaskId',
    key: 'runTaskId',
    width: 100,
  },
  {
    title: t('task.createTime'),
    dataIndex: 'creatTime',
    key: 'creatTime',
    width: 160,
  },
  {
    title: t('task.completeTime'),
    dataIndex: 'endTime',
    key: 'endTime',
    width: 160,
  },
  {
    title: t('common.operation'),
    key: 'action',
    width: 100,
    fixed: 'right',
  },
])

/* Removed local getTaskStatusLabel and getTaskStatusColor in favor of getStatusInfo */

onMounted(() => {
  fetchTasks()
})

const fetchTasks = async () => {
  taskStore.setLoading(true)
  try {
    const response = await taskService.getTasks(
      taskStore.page,
      taskStore.pageSize,
      startDate.value ? startDate.value.format('YYYY-MM-DD') : undefined,
      endDate.value ? endDate.value.format('YYYY-MM-DD') : undefined
    )
    if (response.success && response.data) {
      taskStore.setTasks(response.data.items, response.data.total)
    } else {
      message.error(response.message || t('common.fail'))
    }
  } catch (error: any) {
    message.error(error.message || t('common.fail'))
  } finally {
    taskStore.setLoading(false)
  }
}

const handleFilter = () => {
  taskStore.setPage(1)
  fetchTasks()
}

const handleCancelTask = (id: number) => {
  Modal.confirm({
    title: t('task.confirmCancel'),
    content: t('task.confirmCancelContent'),
    okText: t('common.confirm'),
    cancelText: t('common.cancel'),
    onOk: async () => {
      try {
        const response = await taskService.cancelTask(id)
        if (response.success) {
          message.success(t('task.cancelSuccess'))
          fetchTasks()
        } else {
          message.error(response.message || t('common.fail'))
        }
      } catch (error: any) {
        message.error(error.message || t('common.fail'))
      }
    },
  })
}

const handleCreateTask = () => {
  router.push('/tasks/create')
}

const handleRowClick = (record: any) => {
  router.push(`/tasks/${record.id}`)
}

const handleExport = () => {
  if (taskStore.tasks.length === 0) {
    message.warning(t('task.noDataToExport'))
    return
  }

  // 准备导出数据
  const exportData = taskStore.tasks.map(task => ({
    [t('task.type')]: task.taskType || '-',
    [t('task.source')]: task.sourcePosition || '-',
    [t('task.target')]: task.targetPosition || '-',
    [t('task.status')]: getStatusInfo(task.taskStatus, settingStore.systemType).text,
    [t('task.agvId')]: task.robotCode || '-',
    [t('task.taskId')]: task.runTaskId || '-',
    [t('task.createTime')]: task.creatTime || '-',
    [t('task.completeTime')]: task.endTime || '-',
  }))

  // 创建工作表
  const ws = XLSX.utils.json_to_sheet(exportData)
  
  // 设置列宽
  ws['!cols'] = [
    { wch: 12 }, // 任务类型
    { wch: 20 }, // 起始点
    { wch: 20 }, // 终点
    { wch: 12 }, // 任务状态
    { wch: 12 }, // AGV编号
    { wch: 15 }, // 任务ID
    { wch: 20 }, // 创建时间
    { wch: 20 }, // 完成时间
  ]

  // 创建工作簿
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, t('task.sheetName'))

  // 生成文件名
  const fileName = `${t('task.sheetName')}_${new Date().toISOString().slice(0, 10)}.xlsx`
  
  // 导出文件
  XLSX.writeFile(wb, fileName)
  message.success(t('task.exportSuccess'))
}
</script>

<style scoped>
.tasks-container {
  width: 100%;
}

.tasks-container h1 {
  margin-bottom: 24px;
  font-size: 24px;
  font-weight: 600;
}
</style>
