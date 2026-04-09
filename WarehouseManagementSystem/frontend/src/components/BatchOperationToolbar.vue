<template>
  <div v-if="selectedIds.length > 0" class="batch-toolbar">
    <a-alert
      :message="`已选择 ${selectedIds.length} 项`"
      type="info"
      show-icon
      closable
      @close="handleClearSelection"
    />
    <a-space style="margin-top: 12px">
      <a-button
        type="primary"
        danger
        @click="handleBatchClearMaterial"
        :loading="isClearingMaterial"
      >
        批量清空物料
      </a-button>
      <a-button
        type="primary"
        @click="handleBatchToggleLock"
        :loading="isTogglingLock"
      >
        批量{{ allLocked ? '解锁' : '锁定' }}
      </a-button>
      <a-button @click="handleClearSelection">取消选择</a-button>
    </a-space>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Location } from '@/services/location'
import locationService from '@/services/location'
import { message, Modal } from 'ant-design-vue'

interface Props {
  selectedIds: number[]
  locations: Location[]
}

interface Emits {
  (e: 'clear-selection'): void
  (e: 'refresh'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const isClearingMaterial = ref(false)
const isTogglingLock = ref(false)

const allLocked = computed(() => {
  if (props.selectedIds.length === 0) return false
  return props.locations
    .filter((l) => props.selectedIds.includes(l.id))
    .every((l) => l.lock)
})

const handleClearSelection = () => {
  emit('clear-selection')
}

const handleBatchClearMaterial = () => {
  Modal.confirm({
    title: '确认批量清空物料',
    content: `确定要清空选中的 ${props.selectedIds.length} 个储位的物料吗？`,
    okText: '确定',
    cancelText: '取消',
    onOk: async () => {
      isClearingMaterial.value = true
      try {
        const response = await locationService.batchClearMaterial(props.selectedIds)
        if (response.success && response.data) {
          message.success(
            `成功清空 ${response.data.successCount} 个储位，失败 ${response.data.failCount} 个`
          )
          emit('refresh')
          emit('clear-selection')
        } else {
          message.error(response.message || '批量清空物料失败')
        }
      } catch (error: any) {
        message.error(error.message || '批量清空物料失败')
      } finally {
        isClearingMaterial.value = false
      }
    },
  })
}

const handleBatchToggleLock = () => {
  const action = allLocked.value ? '解锁' : '锁定'
  Modal.confirm({
    title: `确认批量${action}`,
    content: `确定要批量${action}选中的 ${props.selectedIds.length} 个储位吗？`,
    okText: '确定',
    cancelText: '取消',
    onOk: async () => {
      isTogglingLock.value = true
      try {
        const response = await locationService.batchToggleLock(
          props.selectedIds,
          !allLocked.value
        )
        if (response.success && response.data) {
          message.success(
            `成功${action} ${response.data.successCount} 个储位，失败 ${response.data.failCount} 个`
          )
          emit('refresh')
          emit('clear-selection')
        } else {
          message.error(response.message || `批量${action}失败`)
        }
      } catch (error: any) {
        message.error(error.message || `批量${action}失败`)
      } finally {
        isTogglingLock.value = false
      }
    },
  })
}
</script>

<style scoped>
.batch-toolbar {
  margin-bottom: 16px;
  padding: 12px;
  background: #fafafa;
  border-radius: 4px;
}
</style>
