<template>
  <div class="task-detail-container">
    <a-card v-if="task" :title="`任务详情 - ${task.requestCode}`">
      <a-descriptions :column="2" bordered>
        <a-descriptions-item label="任务编号">
          {{ task.requestCode }}
        </a-descriptions-item>
        <a-descriptions-item label="任务状态">
          <a-tag :color="getStatusInfo(task.taskStatus, settingStore.systemType).color">
            {{ getStatusInfo(task.taskStatus, settingStore.systemType).text }}
          </a-tag>
        </a-descriptions-item>
        <a-descriptions-item label="任务类型">
          {{ getTaskTypeInfo(task.taskType, settingStore.systemType) }}
        </a-descriptions-item>
        <a-descriptions-item label="源位置">
          {{ task.sourcePosition }}
        </a-descriptions-item>
        <a-descriptions-item label="目标位置">
          {{ task.targetPosition }}
        </a-descriptions-item>
        <a-descriptions-item label="创建时间">
          {{ task.createdTime }}
        </a-descriptions-item>
        <a-descriptions-item label="操作">
          <a-space>
            <a-button
              type="primary"
              danger
              size="small"
              @click="handleCancelTask"
              :disabled="!canCancel"
            >
              取消任务
            </a-button>
            <a-button size="small" @click="handleBack">返回</a-button>
          </a-space>
        </a-descriptions-item>
      </a-descriptions>
    </a-card>
    <a-spin v-else />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import taskService, { Task } from '@/services/task'
import { message, Modal } from 'ant-design-vue'
import { getTaskTypeInfo } from '@/constants/taskType'
import { getStatusInfo } from '@/constants/taskStatus'
import { useSettingStore } from '@/stores/setting'

const router = useRouter()
const route = useRoute()
const settingStore = useSettingStore()
const task = ref<Task | null>(null)
const isLoading = ref(false)

const canCancel = computed(() => {
  if (!task.value) return false
  const status = task.value.taskStatus
  if (settingStore.systemType === 'NDC') {
    // NDC: Cannot cancel if Finished (11) or already Canceled/Error (>= 30)
    return status < 30 && status !== 11
  } else {
    // Heartbeat: Can only cancel Waiting(0) or Working(1)
    return status === 0 || status === 1
  }
})

const handleCancelTask = () => {
  if (!task.value) return
  
  Modal.confirm({
    title: '确认取消任务',
    content: `确定要取消任务 ${task.value.requestCode} 吗？`,
    okText: '确定',
    cancelText: '取消',
    onOk: async () => {
      if (!task.value) return
      try {
        const response = await taskService.cancelTask(task.value.id)
        if (response.success) {
          message.success('任务取消成功')
          fetchTaskDetail()
        } else {
          message.error(response.message || '任务取消失败')
        }
      } catch (error: any) {
        message.error(error.message || '任务取消失败')
      }
    }
  })
}

const handleBack = () => {
  router.back()
}

onMounted(() => {
  fetchTaskDetail()
})

const fetchTaskDetail = async () => {
  const taskId = route.params.id as string
  if (!taskId) {
    message.error('任务ID不存在')
    router.back()
    return
  }

  isLoading.value = true
  try {
    const response = await taskService.getTaskById(parseInt(taskId))
    if (response.success && response.data) {
      task.value = response.data
    } else {
      message.error(response.message || '获取任务详情失败')
      router.back()
    }
  } catch (error: any) {
    message.error(error.message || '获取任务详情失败')
    router.back()
  } finally {
    isLoading.value = false
  }
}
</script>

<style scoped>
.task-detail-container {
  width: 100%;
  max-width: 800px;
  margin: 0 auto;
}
</style>
